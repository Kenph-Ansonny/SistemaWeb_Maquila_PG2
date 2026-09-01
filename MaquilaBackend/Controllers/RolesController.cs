using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public RolesController(MaquilaDbContext context)
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

    // 1. Obtener lista de roles con conteo de usuarios
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RolListDto>>> GetRoles()
    {
        var roles = await _context.Roles
            .Include(r => r.UsuarioRoles)
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

    // 2. Obtener matriz completa de permisos para un rol específico
    [HttpGet("{idRol}/permisos")]
    public async Task<ActionResult<IEnumerable<PermisoModuloItemDto>>> GetPermisosPorRol(int idRol)
    {
        var modulos = await _context.Modulos.ToListAsync();
        var permisosAsignados = await _context.PermisosRol
            .Where(p => p.IdRol == idRol)
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

    // 3. Crear nuevo Rol
    [HttpPost]
    public async Task<IActionResult> CrearRol([FromBody] CrearRolDto dto)
    {
        if (await _context.Roles.AnyAsync(r => r.NombreRol.ToLower() == dto.NombreRol.Trim().ToLower()))
            return BadRequest(new { message = "Ya existe un rol con ese nombre." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoRol = new Rol
            {
                NombreRol = dto.NombreRol.Trim(),
                Descripcion = dto.Descripcion?.Trim(),
                EstadoRol = true
            };

            _context.Roles.Add(nuevoRol);
            await _context.SaveChangesAsync();

            // Inicializar permisos en falso para todos los módulos
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

            // Bitácora
            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 1,
                Accion = "INSERT",
                TablaAfectada = "Roles",
                IdRegistro = nuevoRol.IdRol.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(new { nuevoRol.NombreRol, nuevoRol.Descripcion }),
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

    // 4. Modificar información básica del Rol
    [HttpPut("{idRol}")]
    public async Task<IActionResult> EditarRol(int idRol, [FromBody] EditarRolDto dto)
    {
        var rol = await _context.Roles.FindAsync(idRol);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        var valoresAnteriores = new { rol.NombreRol, rol.Descripcion };

        rol.NombreRol = dto.NombreRol.Trim();
        rol.Descripcion = dto.Descripcion?.Trim();

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 1,
            Accion = "UPDATE",
            TablaAfectada = "Roles",
            IdRegistro = idRol.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
            ValoresNuevos = JsonSerializer.Serialize(new { dto.NombreRol, dto.Descripcion }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Información del rol actualizada." });
    }

    // 5. Guardar/Actualizar la matriz de permisos
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

            // Bitácora
            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 1,
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

    // 6. Activar / Desactivar Rol
    [HttpPatch("{idRol}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int idRol)
    {
        if (idRol == 1)
            return BadRequest(new { message = "El rol Administrador no puede desactivarse." });

        var rol = await _context.Roles.FindAsync(idRol);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        rol.EstadoRol = !rol.EstadoRol;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 1,
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