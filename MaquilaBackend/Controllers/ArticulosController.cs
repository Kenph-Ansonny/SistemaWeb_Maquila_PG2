using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using MaquilaBackend.Security;
using System.Security.Claims;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArticulosController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public ArticulosController(MaquilaDbContext context)
    {
        _context = context;
    }

        private int ObtenerUsuarioIdSesion()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int userId))
            throw new UnauthorizedAccessException("No se pudo identificar al usuario autenticado.");
        return userId;
    }

    [HttpGet]
    [RequierePermiso("ARTICULOS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ArticuloListDto>>> GetArticulos()
    {
        var articulos = await _context.Articulos
            .Include(a => a.UnidadMedida)
            .Include(a => a.DetallePrenda)
            .Include(a => a.Existencias)
                .ThenInclude(e => e.Almacen)
            .AsNoTracking()
            .Select(a => new ArticuloListDto(
                a.IdArticulo,
                a.CodigoArticulo,
                a.NombreArticulo,
                a.TipoArticulo,
                a.IdUnidadBaseMedida,
                a.UnidadMedida.CodigoMedida,
                a.UnidadMedida.NombreMedida,
                a.StockMinimo,
                a.Existencias.Sum(e => e.StockActual),
                a.CostoPromedio,
                a.EstadoArticulo,
                a.DetallePrenda != null ? a.DetallePrenda.Talla : null,
                a.DetallePrenda != null ? a.DetallePrenda.Color : null,
                a.Existencias.Select(e => new ExistenciaBodegaDto(
                    e.IdAlmacen,
                    e.Almacen.NombreAlmacen,
                    e.StockActual
                )).ToList()
            ))
            .ToListAsync();

        return Ok(articulos);
    }

    [HttpGet("unidades-medida")]
    [RequierePermiso("ARTICULOS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<UnidadMedidaOptionDto>>> GetUnidadesMedida()
    {
        var unidades = await _context.UnidadesMedida
            .AsNoTracking()
            .Select(u => new UnidadMedidaOptionDto(
                u.IdUnidadMedida,
                u.CodigoMedida,
                u.NombreMedida,
                u.TipoMedida
            ))
            .ToListAsync();

        return Ok(unidades);
    }

    [HttpPost]
    [RequierePermiso("ARTICULOS", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearArticulo([FromBody] GuardarArticuloDto dto)
    {
        var codigoLimpio = dto.CodigoArticulo?.Trim().ToUpper() ?? string.Empty;
        var nombreLimpio = dto.NombreArticulo?.Trim() ?? string.Empty;

        // Validaciones Regex de Formato
        if (!Regex.IsMatch(codigoLimpio, @"^[A-Z0-9_-]{2,30}$"))
            return BadRequest(new { message = "El código solo admite letras mayúsculas, números y guiones (- _), sin espacios ni símbolos." });

        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,150}$"))
            return BadRequest(new { message = "El nombre del artículo contiene caracteres inválidos (mínimo 3, máximo 150 caracteres)." });

        if (dto.TipoArticulo == "PrendaTerminada")
        {
            if (!string.IsNullOrWhiteSpace(dto.Talla) && !Regex.IsMatch(dto.Talla.Trim(), @"^[a-zA-Z0-9\s\-\/]{1,10}$"))
                return BadRequest(new { message = "La talla ingresada contiene caracteres no válidos." });

            if (!string.IsNullOrWhiteSpace(dto.Color) && !Regex.IsMatch(dto.Color.Trim(), @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\/]{1,30}$"))
                return BadRequest(new { message = "El color ingresado contiene caracteres no válidos." });
        }

        if (await _context.Articulos.AnyAsync(a => a.CodigoArticulo == codigoLimpio))
            return BadRequest(new { message = "Ya existe un artículo o tela con este código." });

        if (!await _context.UnidadesMedida.AnyAsync(u => u.IdUnidadMedida == dto.IdUnidadBaseMedida))
            return BadRequest(new { message = "La unidad de medida seleccionada no existe." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoArticulo = new Articulo
            {
                CodigoArticulo = codigoLimpio,
                NombreArticulo = nombreLimpio,
                TipoArticulo = dto.TipoArticulo,
                IdUnidadBaseMedida = dto.IdUnidadBaseMedida,
                StockMinimo = Math.Max(0, dto.StockMinimo),
                CostoPromedio = Math.Max(0, dto.CostoPromedio),
                EstadoArticulo = true
            };

            _context.Articulos.Add(nuevoArticulo);
            await _context.SaveChangesAsync();

            if (dto.TipoArticulo == "PrendaTerminada" && (!string.IsNullOrWhiteSpace(dto.Talla) || !string.IsNullOrWhiteSpace(dto.Color)))
            {
                var detalle = new ProductoTerminadoDetalle
                {
                    IdArticulo = nuevoArticulo.IdArticulo,
                    Talla = dto.Talla?.Trim(),
                    Color = dto.Color?.Trim()
                };
                _context.ProductoTerminadoDetalles.Add(detalle);
                await _context.SaveChangesAsync();
            }

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 4, // ARTICULOS
                Accion = "INSERT",
                TablaAfectada = "Articulos",
                IdRegistro = nuevoArticulo.IdArticulo.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Artículo registrado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al guardar: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    [RequierePermiso("ARTICULOS", AccionPermiso.Modificar)]
    public async Task<IActionResult> EditarArticulo(int id, [FromBody] GuardarArticuloDto dto)
    {
        var articulo = await _context.Articulos
            .Include(a => a.DetallePrenda)
            .FirstOrDefaultAsync(a => a.IdArticulo == id);

        if (articulo == null) return NotFound(new { message = "Artículo no encontrado." });

        var codigoLimpio = dto.CodigoArticulo?.Trim().ToUpper() ?? string.Empty;
        var nombreLimpio = dto.NombreArticulo?.Trim() ?? string.Empty;

        // Validaciones Regex de Formato
        if (!Regex.IsMatch(codigoLimpio, @"^[A-Z0-9_-]{2,30}$"))
            return BadRequest(new { message = "El código solo admite letras mayúsculas, números y guiones (- _)." });

        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,150}$"))
            return BadRequest(new { message = "El nombre del artículo contiene caracteres inválidos." });

        if (dto.TipoArticulo == "PrendaTerminada")
        {
            if (!string.IsNullOrWhiteSpace(dto.Talla) && !Regex.IsMatch(dto.Talla.Trim(), @"^[a-zA-Z0-9\s\-\/]{1,10}$"))
                return BadRequest(new { message = "La talla ingresada contiene caracteres no válidos." });

            if (!string.IsNullOrWhiteSpace(dto.Color) && !Regex.IsMatch(dto.Color.Trim(), @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\/]{1,30}$"))
                return BadRequest(new { message = "El color ingresado contiene caracteres no válidos." });
        }

        if (await _context.Articulos.AnyAsync(a => a.CodigoArticulo == codigoLimpio && a.IdArticulo != id))
            return BadRequest(new { message = "El código ya pertenece a otro artículo registrado." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new
            {
                articulo.CodigoArticulo,
                articulo.NombreArticulo,
                articulo.TipoArticulo,
                articulo.IdUnidadBaseMedida,
                articulo.StockMinimo,
                articulo.CostoPromedio,
                articulo.DetallePrenda?.Talla,
                articulo.DetallePrenda?.Color
            };

            articulo.CodigoArticulo = codigoLimpio;
            articulo.NombreArticulo = nombreLimpio;
            articulo.TipoArticulo = dto.TipoArticulo;
            articulo.IdUnidadBaseMedida = dto.IdUnidadBaseMedida;
            articulo.StockMinimo = Math.Max(0, dto.StockMinimo);
            articulo.CostoPromedio = Math.Max(0, dto.CostoPromedio);

            if (dto.TipoArticulo == "PrendaTerminada")
            {
                if (articulo.DetallePrenda != null)
                {
                    articulo.DetallePrenda.Talla = dto.Talla?.Trim();
                    articulo.DetallePrenda.Color = dto.Color?.Trim();
                }
                else if (!string.IsNullOrWhiteSpace(dto.Talla) || !string.IsNullOrWhiteSpace(dto.Color))
                {
                    _context.ProductoTerminadoDetalles.Add(new ProductoTerminadoDetalle
                    {
                        IdArticulo = id,
                        Talla = dto.Talla?.Trim(),
                        Color = dto.Color?.Trim()
                    });
                }
            }

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 4,
                Accion = "UPDATE",
                TablaAfectada = "Articulos",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Artículo actualizado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar: " + ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-estado")]
    [RequierePermiso("ARTICULOS", AccionPermiso.Eliminar)]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var articulo = await _context.Articulos.FindAsync(id);
        if (articulo == null) return NotFound(new { message = "Artículo no encontrado." });

        articulo.EstadoArticulo = !articulo.EstadoArticulo;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 4,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Articulos",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoArticulo = !articulo.EstadoArticulo }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoArticulo = articulo.EstadoArticulo }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = articulo.EstadoArticulo ? "Artículo activado." : "Artículo desactivado." });
    }
}