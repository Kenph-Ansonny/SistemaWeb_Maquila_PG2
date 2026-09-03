using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlmacenesController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public AlmacenesController(MaquilaDbContext context)
    {
        _context = context;
    }

    private int ObtenerUsuarioIdSesion()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var val) && int.TryParse(val, out int userId))
        {
            return userId;
        }
        return 1;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlmacenListDto>>> GetAlmacenes()
    {
        var almacenes = await _context.Almacenes
            .Include(a => a.Existencias)
            .AsNoTracking()
            .Select(a => new AlmacenListDto(
                a.IdAlmacen,
                a.NombreAlmacen,
                a.EstadoAlmacen,
                a.Existencias.Count(e => e.StockActual > 0),
                a.Existencias.Sum(e => e.StockActual)
            ))
            .ToListAsync();

        return Ok(almacenes);
    }

    [HttpGet("{id}/inventario")]
    public async Task<ActionResult<IEnumerable<AlmacenStockDetalleDto>>> GetInventarioPorAlmacen(int id)
    {
        var inventario = await _context.Existencias
            .Where(e => e.IdAlmacen == id && e.StockActual > 0)
            .Include(e => e.Articulo)
                .ThenInclude(art => art.UnidadMedida)
            .AsNoTracking()
            .Select(e => new AlmacenStockDetalleDto(
                e.Articulo.IdArticulo,
                e.Articulo.CodigoArticulo,
                e.Articulo.NombreArticulo,
                e.Articulo.TipoArticulo,
                e.Articulo.UnidadMedida.CodigoMedida,
                e.StockActual
            ))
            .ToListAsync();

        return Ok(inventario);
    }

    [HttpPost]
    public async Task<IActionResult> CrearAlmacen([FromBody] GuardarAlmacenDto dto)
    {
        var nombreLimpio = dto.NombreAlmacen?.Trim() ?? string.Empty;

        // Validación Regex: Letras (con tildes), números, espacios, guiones, puntos y barras (3 a 80 chars)
        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,80}$"))
            return BadRequest(new { message = "El nombre del almacén contiene caracteres inválidos o longitud incorrecta (mínimo 3, máximo 80)." });

        if (await _context.Almacenes.AnyAsync(a => a.NombreAlmacen.ToLower() == nombreLimpio.ToLower()))
            return BadRequest(new { message = "Ya existe un almacén o bodega con este nombre." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoAlmacen = new Almacen
            {
                NombreAlmacen = nombreLimpio,
                EstadoAlmacen = true
            };

            _context.Almacenes.Add(nuevoAlmacen);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 11, // ALMACENES
                Accion = "INSERT",
                TablaAfectada = "Almacenes",
                IdRegistro = nuevoAlmacen.IdAlmacen.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Almacén registrado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear almacén: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> EditarAlmacen(int id, [FromBody] GuardarAlmacenDto dto)
    {
        var almacen = await _context.Almacenes.FindAsync(id);
        if (almacen == null) return NotFound(new { message = "Almacén no encontrado." });

        var nombreLimpio = dto.NombreAlmacen?.Trim() ?? string.Empty;

        // Validación Regex
        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,80}$"))
            return BadRequest(new { message = "El nombre del almacén contiene caracteres inválidos o longitud incorrecta." });

        if (await _context.Almacenes.AnyAsync(a => a.NombreAlmacen.ToLower() == nombreLimpio.ToLower() && a.IdAlmacen != id))
            return BadRequest(new { message = "Ya existe otro almacén registrado con este nombre." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var anterior = new { almacen.NombreAlmacen };
            almacen.NombreAlmacen = nombreLimpio;

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 11,
                Accion = "UPDATE",
                TablaAfectada = "Almacenes",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(anterior),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Almacén actualizado correctamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al modificar almacén: " + ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var almacen = await _context.Almacenes
            .Include(a => a.Existencias)
            .FirstOrDefaultAsync(a => a.IdAlmacen == id);

        if (almacen == null) return NotFound(new { message = "Almacén no encontrado." });

        // Regla de Integridad Física: No desactivar bodega con existencias positivas
        if (almacen.EstadoAlmacen && almacen.Existencias.Any(e => e.StockActual > 0))
        {
            return BadRequest(new { 
                message = "No se puede desactivar este almacén porque mantiene existencias físicas activas." 
            });
        }

        almacen.EstadoAlmacen = !almacen.EstadoAlmacen;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 11,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Almacenes",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoAlmacen = !almacen.EstadoAlmacen }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoAlmacen = almacen.EstadoAlmacen }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = almacen.EstadoAlmacen ? "Almacén activado para operaciones." : "Almacén inhabilitado." });
    }
}