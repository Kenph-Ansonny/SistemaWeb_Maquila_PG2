using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(MaquilaDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var input = dto.Identificador?.Trim() ?? string.Empty;
        var passwordInput = dto.Password?.Trim() ?? string.Empty;

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == input.ToLower() || u.NombreUsuario.ToLower() == input.ToLower());

        if (usuario == null)
            return Unauthorized(new { message = "El usuario o correo no existe en el sistema." });

        if (!usuario.EstadoUsuario)
            return Unauthorized(new { message = "Tu cuenta se encuentra inactiva. Contacta al administrador." });

        // Bloqueo temporal por intentos fallidos (usa campos que ya tenías en el modelo, antes sin uso)
        if (usuario.FechaBloqueo.HasValue && usuario.FechaBloqueo.Value > DateTime.UtcNow)
            return Unauthorized(new { message = "Tu cuenta está bloqueada temporalmente por múltiples intentos fallidos. Intenta más tarde." });

        bool passwordValida;
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
            if (usuario.IntentosFallidos >= 5)
                usuario.FechaBloqueo = DateTime.UtcNow.AddMinutes(15);

            await _context.SaveChangesAsync();
            return Unauthorized(new { message = "Contraseña incorrecta." });
        }

        usuario.IntentosFallidos = 0;
        usuario.FechaBloqueo = null;
        usuario.FechaUltimoAcceso = DateTime.UtcNow;

        var logAcceso = new Bitacora
        {
            IdUsuario = usuario.IdUsuario,
            IdModulo = 1, // Módulo USUARIOS / SEGURIDAD
            Accion = "LOGIN",
            TablaAfectada = "Usuarios",
            IdRegistro = usuario.IdUsuario.ToString(),
            ValoresAnteriores = null,
            ValoresNuevos = JsonSerializer.Serialize(new
            {
                Evento = "Inicio de sesión exitoso",
                Usuario = usuario.NombreUsuario,
                Fecha = DateTime.UtcNow
            }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(logAcceso);

        await _context.SaveChangesAsync();

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
        var token = GenerarToken(usuario, rolNombre);

        return Ok(new LoginResponseDto(
            usuario.IdUsuario,
            usuario.NombreUsuario,
            usuario.Correo,
            rolNombre,
            permisos,
            token
        ));
    }

    /// <summary>
    /// Permite al frontend validar/refrescar los datos de sesión (por ejemplo, al recargar la página)
    /// usando únicamente el token, sin depender de lo que haya en localStorage.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out int idUsuario))
            return Unauthorized();

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        if (usuario == null || !usuario.EstadoUsuario)
            return Unauthorized();

        return Ok(new { usuario.IdUsuario, usuario.NombreUsuario, usuario.Correo });
    }

    private string GenerarToken(Usuario usuario, string rolNombre)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, rolNombre)
        };

        var expireMinutes = double.Parse(jwtSection["ExpireMinutes"] ?? "480");

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}