using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public UsuariosController(MaquilaDbContext context)
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
    public async Task<ActionResult<IEnumerable<UsuarioListDto>>> GetUsuarios()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .Select(u => new UsuarioListDto(
                u.IdUsuario,
                u.NombreUsuario,
                u.Correo,
                u.EstadoUsuario,
                u.FechaCreacion,
                u.FechaUltimoAcceso,
                u.FechaBloqueo,
                u.UsuarioRoles.Select(r => r.Rol.NombreRol).ToList(),
                u.UsuarioRoles.Select(r => r.Rol.IdRol).ToList()
            ))
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpPost]
    public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Correo == dto.Correo || u.NombreUsuario == dto.NombreUsuario))
            return BadRequest(new { message = "El usuario o correo ya está registrado." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoUsuario = new Usuario
            {
                NombreUsuario = dto.NombreUsuario.Trim(),
                Correo = dto.Correo.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                EstadoUsuario = true,
                FechaCreacion = DateTime.UtcNow,
                IntentosFallidos = 0,
                FechaBloqueo = null
            };

            if (dto.RolesIds != null && dto.RolesIds.Count > 0)
            {
                foreach (var rolId in dto.RolesIds)
                {
                    nuevoUsuario.UsuarioRoles.Add(new UsuarioRol { IdRol = rolId });
                }
            }

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 1,
                Accion = "INSERT",
                TablaAfectada = "Usuarios",
                IdRegistro = nuevoUsuario.IdUsuario.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(new { nuevoUsuario.NombreUsuario, nuevoUsuario.Correo, dto.RolesIds }),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Usuario registrado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al guardar: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> EditarUsuario(int id, [FromBody] EditarUsuarioDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null)
            return NotFound(new { message = "Usuario no encontrado." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new { usuario.NombreUsuario, usuario.Correo };

            usuario.NombreUsuario = dto.NombreUsuario.Trim();
            usuario.Correo = dto.Correo.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            _context.UsuarioRoles.RemoveRange(usuario.UsuarioRoles);
            if (dto.RolesIds != null)
            {
                foreach (var rolId in dto.RolesIds)
                {
                    usuario.UsuarioRoles.Add(new UsuarioRol { IdUsuario = id, IdRol = rolId });
                }
            }

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 1,
                Accion = "UPDATE",
                TablaAfectada = "Usuarios",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(new { dto.NombreUsuario, dto.Correo, dto.RolesIds }),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Usuario actualizado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar: " + ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { message = "Usuario no encontrado." });

        // Alternar estado
        usuario.EstadoUsuario = !usuario.EstadoUsuario;

        // Actualizar FechaBloqueo según el nuevo estado
        if (!usuario.EstadoUsuario)
        {
            usuario.FechaBloqueo = DateTime.UtcNow; // Se bloqueó / desactivó
        }
        else
        {
            usuario.FechaBloqueo = null; // Se desbloqueó / reactivó
            usuario.IntentosFallidos = 0;
        }

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 1,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Usuarios",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoUsuario = !usuario.EstadoUsuario }),
            ValoresNuevos = JsonSerializer.Serialize(new { 
                EstadoUsuario = usuario.EstadoUsuario, 
                FechaBloqueo = usuario.FechaBloqueo 
            }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();
        return Ok(new { message = usuario.EstadoUsuario ? "Usuario activado." : "Usuario desactivado y bloqueado." });
    }

    [HttpGet("roles-disponibles")]
    public async Task<IActionResult> GetRolesDisponibles()
    {
        var roles = await _context.Roles
            .Where(r => r.EstadoRol)
            .Select(r => new { r.IdRol, r.NombreRol, r.Descripcion })
            .ToListAsync();

        return Ok(roles);
    }
}