using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public RolesController(MaquilaDbContext context)
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
    public async Task<ActionResult<IEnumerable<RolListDto>>> GetRoles()
    {
        var roles = await _context.Roles
            .Include(r => r.UsuarioRoles)
            .AsNoTracking()
            .Select(r => new RolListDto(
                r.IdRol,
                r.NombreRol,
                r.Descripcion,
                r.EstadoRol,
                r.UsuarioRoles.Count
            ))
            .ToListAsync();

        return Ok(roles);
    }

    [HttpGet("{idRol}/permisos")]
    public async Task<ActionResult<IEnumerable<PermisoModuloItemDto>>> GetPermisosPorRol(int idRol)
    {
        var modulos = await _context.Modulos.AsNoTracking().ToListAsync();
        var permisosAsignados = await _context.PermisosRol
            .Where(p => p.IdRol == idRol)
            .AsNoTracking()
            .ToListAsync();

        var resultado = modulos.Select(m => {
            var asignado = permisosAsignados.FirstOrDefault(p => p.IdModulo == m.IdModulo);
            return new PermisoModuloItemDto(
                m.IdModulo,
                m.CodigoModulo,
                m.NombreModulo,
                asignado?.PuedeConsultar ?? false,
                asignado?.PuedeInsertar ?? false,
                asignado?.PuedeModificar ?? false,
                asignado?.PuedeEliminar ?? false
            );
        }).ToList();

        return Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> CrearRol([FromBody] CrearRolDto dto)
    {
        var nombreLimpio = dto.NombreRol?.Trim() ?? string.Empty;
        var descLimpia = dto.Descripcion?.Trim();

        // Validaciones Regex
        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,50}$"))
            return BadRequest(new { message = "El nombre del rol contiene caracteres inválidos o longitud incorrecta (mínimo 3, máximo 50)." });

        if (!string.IsNullOrWhiteSpace(descLimpia) && !Regex.IsMatch(descLimpia, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,200}$"))
            return BadRequest(new { message = "La descripción contiene símbolos no permitidos o supera los 200 caracteres." });

        if (await _context.Roles.AnyAsync(r => r.NombreRol.ToLower() == nombreLimpio.ToLower()))
            return BadRequest(new { message = "Ya existe un rol con ese nombre." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoRol = new Rol
            {
                NombreRol = nombreLimpio,
                Descripcion = descLimpia,
                EstadoRol = true
            };

            _context.Roles.Add(nuevoRol);
            await _context.SaveChangesAsync();

            var modulos = await _context.Modulos.ToListAsync();
            foreach (var mod in modulos)
            {
                _context.PermisosRol.Add(new PermisoRol
                {
                    IdRol = nuevoRol.IdRol,
                    IdModulo = mod.IdModulo,
                    PuedeConsultar = false,
                    PuedeInsertar = false,
                    PuedeModificar = false,
                    PuedeEliminar = false
                });
            }
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 13, // ROLES
                Accion = "INSERT",
                TablaAfectada = "Roles",
                IdRegistro = nuevoRol.IdRol.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Rol creado exitosamente.", idRol = nuevoRol.IdRol });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear rol: " + ex.Message });
        }
    }

    [HttpPut("{idRol}")]
    public async Task<IActionResult> EditarRol(int idRol, [FromBody] EditarRolDto dto)
    {
        var rol = await _context.Roles.FindAsync(idRol);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        var nombreLimpio = dto.NombreRol?.Trim() ?? string.Empty;
        var descLimpia = dto.Descripcion?.Trim();

        // Validaciones Regex
        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,50}$"))
            return BadRequest(new { message = "El nombre del rol contiene caracteres inválidos." });

        if (!string.IsNullOrWhiteSpace(descLimpia) && !Regex.IsMatch(descLimpia, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,200}$"))
            return BadRequest(new { message = "La descripción contiene símbolos no permitidos." });

        if (await _context.Roles.AnyAsync(r => r.NombreRol.ToLower() == nombreLimpio.ToLower() && r.IdRol != idRol))
            return BadRequest(new { message = "Ya existe otro rol con ese nombre." });

        var valoresAnteriores = new { rol.NombreRol, rol.Descripcion };
        rol.NombreRol = nombreLimpio;
        rol.Descripcion = descLimpia;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 13,
            Accion = "UPDATE",
            TablaAfectada = "Roles",
            IdRegistro = idRol.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
            ValoresNuevos = JsonSerializer.Serialize(dto),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Información del rol actualizada." });
    }

    [HttpPut("{idRol}/permisos")]
    public async Task<IActionResult> GuardarPermisos(int idRol, [FromBody] List<PermisoModuloItemDto> permisosDto)
    {
        var rol = await _context.Roles.FindAsync(idRol);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var permisosActuales = await _context.PermisosRol.Where(p => p.IdRol == idRol).ToListAsync();
            _context.PermisosRol.RemoveRange(permisosActuales);
            await _context.SaveChangesAsync();

            foreach (var p in permisosDto)
            {
                _context.PermisosRol.Add(new PermisoRol
                {
                    IdRol = idRol,
                    IdModulo = p.IdModulo,
                    PuedeConsultar = p.PuedeConsultar,
                    PuedeInsertar = p.PuedeInsertar,
                    PuedeModificar = p.PuedeModificar,
                    PuedeEliminar = p.PuedeEliminar
                });
            }
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 13,
                Accion = "UPDATE_PERMISOS",
                TablaAfectada = "Permisos_Rol",
                IdRegistro = idRol.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(permisosDto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Permisos del rol actualizados exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar permisos: " + ex.Message });
        }
    }

    [HttpPatch("{idRol}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int idRol)
    {
        if (idRol == 1)
            return BadRequest(new { message = "El rol Administrador no puede ser desactivado." });

        var rol = await _context.Roles.FindAsync(idRol);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        rol.EstadoRol = !rol.EstadoRol;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 13,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Roles",
            IdRegistro = idRol.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoRol = !rol.EstadoRol }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoRol = rol.EstadoRol }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();
        return Ok(new { message = rol.EstadoRol ? "Rol activado." : "Rol desactivado." });
    }
}