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
public class UsuariosController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public UsuariosController(MaquilaDbContext context)
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
    public async Task<ActionResult<IEnumerable<UsuarioListDto>>> GetUsuarios()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .AsNoTracking()
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
        var usuarioLimpio = dto.NombreUsuario?.Trim() ?? string.Empty;
        var correoLimpio = dto.Correo?.Trim().ToLower() ?? string.Empty;

        // Validaciones Regex de Formato
        if (!Regex.IsMatch(usuarioLimpio, @"^[a-zA-Z0-9_]{3,30}$"))
            return BadRequest(new { message = "El nombre de usuario solo admite letras, números y guión bajo (3 a 30 caracteres)." });

        if (!Regex.IsMatch(correoLimpio, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { message = "El formato de correo electrónico no es válido." });

        // Validación de Contraseña Compleja: Mínimo 8, 1 mayúscula, 1 minúscula, 1 número, 1 símbolo
        if (string.IsNullOrWhiteSpace(dto.Password) || 
            !Regex.IsMatch(dto.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*#?&])[A-Za-z\d@$!%*#?&]{8,}$"))
        {
            return BadRequest(new { message = "La contraseña debe tener mínimo 8 caracteres, incluir mayúscula, minúscula, número y un carácter especial (@$!%*#?&)." });
        }

        if (await _context.Usuarios.AnyAsync(u => u.Correo.ToLower() == correoLimpio || u.NombreUsuario.ToLower() == usuarioLimpio.ToLower()))
            return BadRequest(new { message = "El nombre de usuario o correo ya se encuentra registrado." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoUsuario = new Usuario
            {
                NombreUsuario = usuarioLimpio,
                Correo = correoLimpio,
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
                IdModulo = 1, // USUARIOS
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

        var usuarioLimpio = dto.NombreUsuario?.Trim() ?? string.Empty;
        var correoLimpio = dto.Correo?.Trim().ToLower() ?? string.Empty;

        // Validaciones Regex de Formato
        if (!Regex.IsMatch(usuarioLimpio, @"^[a-zA-Z0-9_]{3,30}$"))
            return BadRequest(new { message = "El nombre de usuario contiene caracteres no válidos." });

        if (!Regex.IsMatch(correoLimpio, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return BadRequest(new { message = "El formato de correo no es válido." });

        if (await _context.Usuarios.AnyAsync(u => (u.Correo.ToLower() == correoLimpio || u.NombreUsuario.ToLower() == usuarioLimpio.ToLower()) && u.IdUsuario != id))
            return BadRequest(new { message = "El nombre de usuario o correo ya pertenece a otra cuenta." });

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            if (!Regex.IsMatch(dto.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*#?&])[A-Za-z\d@$!%*#?&]{8,}$"))
                return BadRequest(new { message = "La nueva contraseña no cumple con los requisitos de seguridad establecidos." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new { usuario.NombreUsuario, usuario.Correo };

            usuario.NombreUsuario = usuarioLimpio;
            usuario.Correo = correoLimpio;

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

        if (id == 1 && usuario.EstadoUsuario)
            return BadRequest(new { message = "La cuenta principal de administrador no puede ser desactivada." });

        usuario.EstadoUsuario = !usuario.EstadoUsuario;

        if (!usuario.EstadoUsuario)
        {
            usuario.FechaBloqueo = DateTime.UtcNow;
        }
        else
        {
            usuario.FechaBloqueo = null;
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
            .AsNoTracking()
            .Select(r => new { r.IdRol, r.NombreRol, r.Descripcion })
            .ToListAsync();

        return Ok(roles);
    }
}