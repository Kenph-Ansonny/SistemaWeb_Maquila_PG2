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
using MaquilaBackend.Helpers;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProveedoresController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private const int ID_MODULO_PROVEEDORES = 2; // Modulo 'PROVEEDORES' en tabla Modulos

    public ProveedoresController(MaquilaDbContext context)
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

    // GET: api/proveedores
    [HttpGet]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ProveedorListDto>>> GetProveedores(
        [FromQuery] bool? soloActivos = null,
        [FromQuery] string? busqueda = null)
    {
        var query = _context.Proveedores
            .AsNoTracking()
            .AsQueryable();

        if (soloActivos.HasValue)
            query = query.Where(p => p.EstadoProveedor == soloActivos.Value);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(p =>
                p.NombreProveedor.ToLower().Contains(term) ||
                (p.ContactoPrincipal != null && p.ContactoPrincipal.ToLower().Contains(term)) ||
                (p.Telefono != null && p.Telefono.ToLower().Contains(term)) ||
                (p.Direccion != null && p.Direccion.ToLower().Contains(term)));
        }

        var lista = await query
            .OrderByDescending(p => p.IdProveedor)
            .Select(p => new ProveedorListDto(
                p.IdProveedor,
                p.NombreProveedor,
                p.ContactoPrincipal,
                p.Telefono,
                p.Direccion,
                p.EstadoProveedor
            ))
            .ToListAsync();

        return Ok(lista);
    }

    // GET: api/proveedores/opciones
    [HttpGet("opciones")]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ProveedorOptionDto>>> GetOpciones()
    {
        var opciones = await _context.Proveedores
            .Where(p => p.EstadoProveedor)
            .OrderBy(p => p.NombreProveedor)
            .Select(p => new ProveedorOptionDto(p.IdProveedor, p.NombreProveedor))
            .ToListAsync();

        return Ok(opciones);
    }

    // GET: api/proveedores/{id}
    [HttpGet("{id}")]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Consultar)]
    public async Task<ActionResult<ProveedorListDto>> GetProveedorPorId(int id)
    {
        var proveedor = await _context.Proveedores
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProveedor == id);

        if (proveedor == null)
            return NotFound(new { message = "El proveedor solicitado no existe." });

        return Ok(new ProveedorListDto(
            proveedor.IdProveedor,
            proveedor.NombreProveedor,
            proveedor.ContactoPrincipal,
            proveedor.Telefono,
            proveedor.Direccion,
            proveedor.EstadoProveedor
        ));
    }

    // POST: api/proveedores
    [HttpPost]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearProveedor([FromBody] GuardarProveedorDto dto)
    {
        var nombreLimpio = dto.NombreProveedor?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nombreLimpio) || nombreLimpio.Length < 3 || nombreLimpio.Length > 150)
            return BadRequest(new { message = "El nombre o razón social del proveedor debe tener entre 3 y 150 caracteres." });

        if (await _context.Proveedores.AnyAsync(p => p.NombreProveedor.ToLower() == nombreLimpio.ToLower()))
            return BadRequest(new { message = $"Ya existe un proveedor registrado con el nombre '{nombreLimpio}'." });

        var contactoLimpio = dto.ContactoPrincipal?.Trim();
        if (!string.IsNullOrWhiteSpace(contactoLimpio) && contactoLimpio.Length > 100)
            return BadRequest(new { message = "El contacto principal no puede exceder 100 caracteres." });

        var telefonoLimpio = dto.Telefono?.Trim();
        if (!string.IsNullOrWhiteSpace(telefonoLimpio) && telefonoLimpio.Length > 30)
            return BadRequest(new { message = "El teléfono no puede exceder 30 caracteres." });

        var direccionLimpia = dto.Direccion?.Trim();
        if (!string.IsNullOrWhiteSpace(direccionLimpia) && direccionLimpia.Length > 200)
            return BadRequest(new { message = "La dirección no puede exceder 200 caracteres." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoProveedor = new Proveedor
            {
                NombreProveedor = nombreLimpio,
                ContactoPrincipal = string.IsNullOrWhiteSpace(contactoLimpio) ? null : contactoLimpio,
                Telefono = string.IsNullOrWhiteSpace(telefonoLimpio) ? null : telefonoLimpio,
                Direccion = string.IsNullOrWhiteSpace(direccionLimpia) ? null : direccionLimpia,
                EstadoProveedor = true
            };

            _context.Proveedores.Add(nuevoProveedor);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_PROVEEDORES,
                Accion = "INSERT",
                TablaAfectada = "Proveedores",
                IdRegistro = nuevoProveedor.IdProveedor.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Proveedor registrado exitosamente.", idProveedor = nuevoProveedor.IdProveedor });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al registrar el proveedor: " + ex.Message });
        }
    }

    // PUT: api/proveedores/{id}
    [HttpPut("{id}")]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Modificar)]
    public async Task<IActionResult> EditarProveedor(int id, [FromBody] GuardarProveedorDto dto)
    {
        var proveedor = await _context.Proveedores.FindAsync(id);
        if (proveedor == null)
            return NotFound(new { message = "Proveedor no encontrado." });

        var nombreLimpio = dto.NombreProveedor?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreLimpio) || nombreLimpio.Length < 3 || nombreLimpio.Length > 150)
            return BadRequest(new { message = "El nombre o razón social debe tener entre 3 y 150 caracteres." });

        if (await _context.Proveedores.AnyAsync(p => p.NombreProveedor.ToLower() == nombreLimpio.ToLower() && p.IdProveedor != id))
            return BadRequest(new { message = $"El nombre '{nombreLimpio}' ya pertenece a otro proveedor registrado." });

        var contactoLimpio = dto.ContactoPrincipal?.Trim();
        var telefonoLimpio = dto.Telefono?.Trim();
        var direccionLimpia = dto.Direccion?.Trim();

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new
            {
                proveedor.NombreProveedor,
                proveedor.ContactoPrincipal,
                proveedor.Telefono,
                proveedor.Direccion
            };

            proveedor.NombreProveedor = nombreLimpio;
            proveedor.ContactoPrincipal = string.IsNullOrWhiteSpace(contactoLimpio) ? null : contactoLimpio;
            proveedor.Telefono = string.IsNullOrWhiteSpace(telefonoLimpio) ? null : telefonoLimpio;
            proveedor.Direccion = string.IsNullOrWhiteSpace(direccionLimpia) ? null : direccionLimpia;

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_PROVEEDORES,
                Accion = "UPDATE",
                TablaAfectada = "Proveedores",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Proveedor actualizado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al actualizar el proveedor: " + ex.Message });
        }
    }

    // PATCH: api/proveedores/{id}/estado
    [HttpPatch("{id}/estado")]
    [RequierePermiso("PROVEEDORES", AccionPermiso.Eliminar)]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var proveedor = await _context.Proveedores.FindAsync(id);
        if (proveedor == null)
            return NotFound(new { message = "Proveedor no encontrado." });

        int idUsuario = ObtenerUsuarioIdSesion();
        var estadoAnterior = proveedor.EstadoProveedor;
        proveedor.EstadoProveedor = !proveedor.EstadoProveedor;

        var log = new Bitacora
        {
            IdUsuario = idUsuario,
            IdModulo = ID_MODULO_PROVEEDORES,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Proveedores",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoProveedor = estadoAnterior }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoProveedor = proveedor.EstadoProveedor }),
            DireccionIp = HttpContext.ObtenerIpCliente(),
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();

        string accionDesc = proveedor.EstadoProveedor ? "activado" : "desactivado";
        return Ok(new { message = $"Proveedor {accionDesc} exitosamente.", nuevoEstado = proveedor.EstadoProveedor });
    }
}