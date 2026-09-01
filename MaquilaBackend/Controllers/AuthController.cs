using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;

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

        // 1. Buscar coincidencia por correo o nombre_usuario
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == input.ToLower() || u.NombreUsuario.ToLower() == input.ToLower());

        if (usuario == null)
            return Unauthorized(new { message = "El usuario o correo no existe en el sistema." });

        if (!usuario.EstadoUsuario)
            return Unauthorized(new { message = "Tu cuenta se encuentra inactiva. Contacta al administrador." });

        // 2. Verificación con BCrypt
        bool passwordValida = false;
        try
        {
            passwordValida = BCrypt.Net.BCrypt.Verify(passwordInput, usuario.PasswordHash);
        }
        catch (Exception)
        {
            passwordValida = false;
        }

        if (!passwordValida)
        {
            usuario.IntentosFallidos++;
            await _context.SaveChangesAsync();
            return Unauthorized(new { message = "Contraseña incorrecta." });
        }

        // 3. Reiniciar intentos fallidos
        usuario.IntentosFallidos = 0;
        usuario.FechaUltimoAcceso = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var rolPrincipal = usuario.UsuarioRoles.FirstOrDefault()?.Rol.NombreRol.ToLower() ?? "admin";

        string rolFrontend = "admin";
        if (rolPrincipal.Contains("conta")) rolFrontend = "contador";
        else if (rolPrincipal.Contains("inventario") || rolPrincipal.Contains("operador")) rolFrontend = "inventario";
        else if (rolPrincipal.Contains("pedido")) rolFrontend = "pedidos";

        return Ok(new LoginResponseDto(
            usuario.IdUsuario,
            usuario.NombreUsuario,
            usuario.Correo,
            rolFrontend,
            "session-active"
        ));
    }
}