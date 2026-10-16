using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using MaquilaBackend.Helpers;
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
    private const int MODULO_SEGURIDAD_ID = 1;

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

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(passwordInput))
            return Unauthorized(new { message = "Ingresa tu usuario/correo y contraseña." });

        //búsqueda directa sin Include pesado
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == input || u.NombreUsuario == input);

        // Si el usuario no existe, se rechaza de inmediato
        if (usuario == null)
            return Unauthorized(new { message = "El usuario o correo no existe en el sistema." });

        if (!usuario.EstadoUsuario)
            return Unauthorized(new { message = "Tu cuenta se encuentra inactiva. Contacta al administrador." });

        //Comprobar si la cuenta está bloqueada temporalmente
        if (usuario.FechaBloqueo.HasValue && usuario.FechaBloqueo.Value > DateTime.UtcNow)
        {
            var restante = usuario.FechaBloqueo.Value - DateTime.UtcNow;
            var segundos = Math.Max(1, (int)restante.TotalSeconds);
            var textoTiempo = segundos > 60 ? $"{Math.Ceiling(segundos / 60.0)} minuto(s)" : $"{segundos} segundo(s)";

            return StatusCode(429, new
            {
                message = $"5 intentos alcanzados. Cuenta bloqueada temporalmente por seguridad. Espera {textoTiempo}.",
                segundosRestantes = segundos
            });
        }

        //Verificación de contraseña con BCrypt
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

            if (usuario.IntentosFallidos == 5)
            {
                usuario.FechaBloqueo = DateTime.UtcNow.AddMinutes(1);
            }
            else if (usuario.IntentosFallidos > 5)
            {
                usuario.FechaBloqueo = DateTime.UtcNow.AddMinutes(3);
            }

            await _context.SaveChangesAsync();

            if (usuario.FechaBloqueo.HasValue)
            {
                int segs = (int)(usuario.FechaBloqueo.Value - DateTime.UtcNow).TotalSeconds;
                return StatusCode(429, new
                {
                    message = "5 intentos alcanzados. Espera 1 minuto para volver a intentar.",
                    segundosRestantes = segs
                });
            }

            int restantes = Math.Max(0, 5 - usuario.IntentosFallidos);
            return Unauthorized(new
            {
                message = $"Contraseña incorrecta. Te quedan {restantes} intento(s) antes del bloqueo temporal."
            });
        }

        // Credenciales válidas: reiniciar contadores
        usuario.IntentosFallidos = 0;
        usuario.FechaBloqueo = null;
        usuario.FechaUltimoAcceso = DateTime.UtcNow;

        // Cargar roles y permisos únicamente cuando el login fue exitoso
        var rolesIds = await _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == usuario.IdUsuario)
            .Select(ur => ur.IdRol)
            .ToListAsync();

        var rolEntidad = await _context.UsuarioRoles
            .Include(ur => ur.Rol)
            .Where(ur => ur.IdUsuario == usuario.IdUsuario)
            .Select(ur => ur.Rol.NombreRol)
            .FirstOrDefaultAsync();

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

        var rolNombre = rolEntidad ?? "Usuario";
        var token = GenerarToken(usuario, rolNombre, dto.Recordarme);

        // bitacora
        _context.Bitacora.Add(new Bitacora
        {
            IdUsuario = usuario.IdUsuario,
            IdModulo = MODULO_SEGURIDAD_ID,
            Accion = "LOGIN",
            TablaAfectada = "Usuarios",
            IdRegistro = usuario.IdUsuario.ToString(),
            ValoresAnteriores = null,
            ValoresNuevos = JsonSerializer.Serialize(new { Evento = "Login exitoso", Usuario = usuario.NombreUsuario }),
            DireccionIp = HttpContext.ObtenerIpCliente(),
            FechaRegistro = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new LoginResponseDto(
            usuario.IdUsuario,
            usuario.NombreUsuario,
            usuario.Correo,
            rolNombre,
            permisos,
            token
        ));
    }

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

    private string GenerarToken(Usuario usuario, string rolNombre, bool recordarme)
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

        double expireMinutes = recordarme ? (7 * 24 * 60) : double.Parse(jwtSection["ExpireMinutes"] ?? "480");

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