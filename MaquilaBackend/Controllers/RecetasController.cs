using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecetasController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public RecetasController(MaquilaDbContext context)
    {
        _context = context;
    }

    private int ObtenerUsuarioIdSesion()
    {
        // Con [Authorize] activo, el middleware ya validó la firma del token
        // antes de llegar aquí; este claim no puede ser falsificado por el cliente.
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int userId))
            throw new UnauthorizedAccessException("No se pudo identificar al usuario autenticado a partir del token.");

        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecetas()
    {
        var recetas = await _context.Recetas
            .Include(r => r.ArticuloPrenda)
                .ThenInclude(ap => ap.DetallePrenda)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.ArticuloInsumo)
            .AsNoTracking()
            .Select(r => new RecetaListDto(
                r.IdReceta,
                r.NombreReceta,
                r.IdArticuloPrenda,
                r.ArticuloPrenda.CodigoArticulo,
                r.ArticuloPrenda.NombreArticulo,
                r.ArticuloPrenda.DetallePrenda != null ? r.ArticuloPrenda.DetallePrenda.Talla : null,
                r.ArticuloPrenda.DetallePrenda != null ? r.ArticuloPrenda.DetallePrenda.Color : null,
                r.DescripcionReceta,
                r.EstadoReceta,
                r.Detalles.Count,
                r.Detalles.Sum(d => d.CantidadBruta * d.ArticuloInsumo.CostoPromedio)
            ))
            .ToListAsync();

        return Ok(recetas);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecetaPorId(int id)
    {
        var r = await _context.Recetas
            .Include(r => r.ArticuloPrenda)
                .ThenInclude(ap => ap.DetallePrenda)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.ArticuloInsumo)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.UnidadConsumo)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdReceta == id);

        if (r == null) return NotFound(new { message = "Ficha técnica no encontrada." });

        var detalles = r.Detalles.Select(d => new RecetaDetalleDto(
            d.IdArticuloInsumo,
            d.ArticuloInsumo.CodigoArticulo,
            d.ArticuloInsumo.NombreArticulo,
            d.ArticuloInsumo.TipoArticulo,
            d.IdUnidadConsumo,
            d.UnidadConsumo.CodigoMedida,
            d.UnidadConsumo.NombreMedida,
            d.CantidadNeta,
            d.PorcentajeMerma,
            d.CantidadBruta,
            d.ArticuloInsumo.CostoPromedio,
            d.CantidadBruta * d.ArticuloInsumo.CostoPromedio
        )).ToList();

        var respuesta = new
        {
            r.IdReceta,
            r.NombreReceta,
            r.IdArticuloPrenda,
            r.ArticuloPrenda.CodigoArticulo,
            r.ArticuloPrenda.NombreArticulo,
            Talla = r.ArticuloPrenda.DetallePrenda?.Talla,
            Color = r.ArticuloPrenda.DetallePrenda?.Color,
            r.DescripcionReceta,
            r.EstadoReceta,
            CostoEstimadoTotal = detalles.Sum(d => d.Subtotal),
            Detalles = detalles
        };

        return Ok(respuesta);
    }

    [HttpPost]
    public async Task<IActionResult> CrearReceta([FromBody] GuardarRecetaDto dto)
    {
        var nombre = dto.NombreReceta?.Trim() ?? string.Empty;
        var desc = dto.DescripcionReceta?.Trim();

        if (!Regex.IsMatch(nombre, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,100}$"))
            return BadRequest(new { message = "El nombre de la receta contiene caracteres inválidos o longitud incorrecta (3 a 100 caracteres)." });

        if (!string.IsNullOrWhiteSpace(desc) && !Regex.IsMatch(desc, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,500}$"))
            return BadRequest(new { message = "La descripción contiene símbolos no admitidos." });

        var prenda = await _context.Articulos.FindAsync(dto.IdArticuloPrenda);
        if (prenda == null || prenda.TipoArticulo != "PrendaTerminada")
            return BadRequest(new { message = "El artículo principal seleccionado debe ser una PrendaTerminada." });

        var (esValido, mensajeError) = await ValidarDetallesAsync(dto.Detalles, dto.IdArticuloPrenda);
        if (!esValido)
            return BadRequest(new { message = mensajeError });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var receta = new Receta
            {
                IdArticuloPrenda = dto.IdArticuloPrenda,
                NombreReceta = nombre,
                DescripcionReceta = desc,
                EstadoReceta = true
            };

            foreach (var d in dto.Detalles)
            {
                decimal brutaCalculada = Math.Round(d.CantidadNeta * (1m + (d.PorcentajeMerma / 100m)), 4);

                receta.Detalles.Add(new RecetaDetalle
                {
                    IdArticuloInsumo = d.IdArticuloInsumo,
                    IdUnidadConsumo = d.IdUnidadConsumo,
                    CantidadNeta = d.CantidadNeta,
                    PorcentajeMerma = d.PorcentajeMerma,
                    CantidadBruta = brutaCalculada
                });
            }

            _context.Recetas.Add(receta);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 5,
                Accion = "INSERT",
                TablaAfectada = "Recetas",
                IdRegistro = receta.IdReceta.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Ficha técnica registrada exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear la ficha técnica: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> EditarReceta(int id, [FromBody] GuardarRecetaDto dto)
    {
        var receta = await _context.Recetas
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r => r.IdReceta == id);

        if (receta == null) return NotFound(new { message = "Ficha técnica no encontrada." });

        var nombre = dto.NombreReceta?.Trim() ?? string.Empty;
        var desc = dto.DescripcionReceta?.Trim();

        if (!Regex.IsMatch(nombre, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,100}$"))
            return BadRequest(new { message = "El nombre de la receta contiene caracteres inválidos." });

        if (!string.IsNullOrWhiteSpace(desc) && !Regex.IsMatch(desc, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,500}$"))
            return BadRequest(new { message = "La descripción contiene símbolos no admitidos." });

        var prenda = await _context.Articulos.FindAsync(dto.IdArticuloPrenda);
        if (prenda == null || prenda.TipoArticulo != "PrendaTerminada")
            return BadRequest(new { message = "El artículo principal seleccionado debe ser una PrendaTerminada." });

        var (esValido, mensajeError) = await ValidarDetallesAsync(dto.Detalles, dto.IdArticuloPrenda);
        if (!esValido)
            return BadRequest(new { message = mensajeError });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var anterior = new { receta.NombreReceta, receta.DescripcionReceta, CantidadInsumos = receta.Detalles.Count };

            receta.NombreReceta = nombre;
            receta.DescripcionReceta = desc;
            receta.IdArticuloPrenda = dto.IdArticuloPrenda;

            _context.RecetaDetalles.RemoveRange(receta.Detalles);

            foreach (var d in dto.Detalles)
            {
                decimal brutaCalculada = Math.Round(d.CantidadNeta * (1m + (d.PorcentajeMerma / 100m)), 4);

                receta.Detalles.Add(new RecetaDetalle
                {
                    IdReceta = id,
                    IdArticuloInsumo = d.IdArticuloInsumo,
                    IdUnidadConsumo = d.IdUnidadConsumo,
                    CantidadNeta = d.CantidadNeta,
                    PorcentajeMerma = d.PorcentajeMerma,
                    CantidadBruta = brutaCalculada
                });
            }

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 5,
                Accion = "UPDATE",
                TablaAfectada = "Recetas",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(anterior),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Ficha técnica actualizada exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar la ficha técnica: " + ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var receta = await _context.Recetas.FindAsync(id);
        if (receta == null) return NotFound(new { message = "Ficha técnica no encontrada." });

        receta.EstadoReceta = !receta.EstadoReceta;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 5,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Recetas",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoReceta = !receta.EstadoReceta }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoReceta = receta.EstadoReceta }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = receta.EstadoReceta ? "Ficha técnica activada." : "Ficha técnica inhabilitada." });
    }

    /// <summary>
    /// Valida la lista de detalles de una receta: existencia y tipo de artículo,
    /// existencia de unidades de consumo, duplicados y rangos numéricos.
    /// </summary>
    private async Task<(bool esValido, string? mensajeError)> ValidarDetallesAsync(
        List<GuardarRecetaDetalleDto> detalles, int idArticuloPrenda)
    {
        if (detalles == null || detalles.Count == 0)
            return (false, "Debe asignar al menos un insumo o tela a la ficha técnica.");

        var idsInsumo = detalles.Select(d => d.IdArticuloInsumo).ToList();

        if (idsInsumo.Distinct().Count() != idsInsumo.Count)
            return (false, "No se puede repetir el mismo insumo dentro de una misma ficha técnica.");

        if (idsInsumo.Contains(idArticuloPrenda))
            return (false, "La prenda terminada no puede figurar como insumo de su propia receta.");

        var articulosValidos = await _context.Articulos
            .Where(a => idsInsumo.Contains(a.IdArticulo))
            .ToDictionaryAsync(a => a.IdArticulo, a => a.TipoArticulo);

        foreach (var idInsumo in idsInsumo)
        {
            if (!articulosValidos.TryGetValue(idInsumo, out var tipo))
                return (false, $"El insumo con Id {idInsumo} no existe en el catálogo de artículos.");

            if (tipo == "PrendaTerminada")
                return (false, $"El artículo Id {idInsumo} es una prenda terminada y no puede usarse como insumo.");
        }

        var idsUnidad = detalles.Select(d => d.IdUnidadConsumo).Distinct().ToList();
        var unidadesExistentes = await _context.UnidadesMedida
            .Where(u => idsUnidad.Contains(u.IdUnidadMedida))
            .Select(u => u.IdUnidadMedida)
            .ToListAsync();

        if (unidadesExistentes.Count != idsUnidad.Count)
            return (false, "Una o más unidades de consumo seleccionadas no existen.");

        foreach (var d in detalles)
        {
            if (d.CantidadNeta <= 0)
                return (false, "La cantidad neta debe ser mayor a 0.");
            if (d.PorcentajeMerma < 0 || d.PorcentajeMerma > 100)
                return (false, "El porcentaje de merma debe estar comprendido entre 0% y 100%.");
        }

        return (true, null);
    }
}