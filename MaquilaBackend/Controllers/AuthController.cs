using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public AuthController(MaquilaDbContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var input = dto.Identificador?.Trim() ?? string.Empty;
        var passwordInput = dto.Password?.Trim() ?? string.Empty;

        // 1. Buscar coincidencia
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == input.ToLower() || u.NombreUsuario.ToLower() == input.ToLower());

        if (usuario == null)
            return Unauthorized(new { message = "El usuario o correo no existe en el sistema." });

        if (!usuario.EstadoUsuario)
            return Unauthorized(new { message = "Tu cuenta se encuentra inactiva. Contacta al administrador." });

        // 2. Verificación de hash con BCrypt
        bool passwordValida = false;
        try
        {
            passwordValida = BCrypt.Net.BCrypt.Verify(passwordInput, usuario.PasswordHash);
        }
        catch
        {
            passwordValida = false;
        }

        if (!passwordValida)
        {
            usuario.IntentosFallidos++;
            await _context.SaveChangesAsync();
            return Unauthorized(new { message = "Contraseña incorrecta." });
        }

        // 3. Reiniciar intentos y actualizar última fecha de acceso
        usuario.IntentosFallidos = 0;
        usuario.FechaUltimoAcceso = DateTime.UtcNow;

        // 4. Registro en Bitácora: LOGIN EXITOSO
        var logAcceso = new Bitacora
        {
            IdUsuario = usuario.IdUsuario,
            IdModulo = 1, // Módulo USUARIOS / SEGURIDAD
            Accion = "LOGIN",
            TablaAfectada = "Usuarios",
            IdRegistro = usuario.IdUsuario.ToString(),
            ValoresAnteriores = null,
            ValoresNuevos = JsonSerializer.Serialize(new { 
                Evento = "Inicio de sesión exitoso", 
                Usuario = usuario.NombreUsuario,
                Fecha = DateTime.UtcNow 
            }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(logAcceso);

        await _context.SaveChangesAsync();

        // 5. Cargar permisos asociados
        var rolesIds = usuario.UsuarioRoles.Select(ur => ur.IdRol).ToList();

        var permisosDb = await _context.PermisosRol
            .Include(pr => pr.Modulo)
            .Where(pr => rolesIds.Contains(pr.IdRol))
            .AsNoTracking()
            .ToListAsync();

        var permisos = permisosDb
            .GroupBy(pr => new { pr.Modulo.CodigoModulo, pr.Modulo.NombreModulo })
            .Select(g => new PermisoModuloDto(
                g.Key.CodigoModulo,
                g.Key.NombreModulo,
                g.Any(p => p.PuedeConsultar),
                g.Any(p => p.PuedeInsertar),
                g.Any(p => p.PuedeModificar),
                g.Any(p => p.PuedeEliminar)
            ))
            .ToList();

        var rolNombre = usuario.UsuarioRoles.FirstOrDefault()?.Rol.NombreRol ?? "Usuario";

        return Ok(new LoginResponseDto(
            usuario.IdUsuario,
            usuario.NombreUsuario,
            usuario.Correo,
            rolNombre,
            permisos,
            "session-active"
        ));
    }
}