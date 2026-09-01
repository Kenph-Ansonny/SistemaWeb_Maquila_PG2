using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;

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

    // 1. Consultar listado de usuarios
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
                u.UsuarioRoles.Select(r => r.Rol.NombreRol).ToList(),
                u.UsuarioRoles.Select(r => r.Rol.IdRol).ToList()
            ))
            .ToListAsync();

        return Ok(usuarios);
    }

    // 2. Insertar nuevo usuario (Sin rol forzado)
    [HttpPost]
    public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Correo == dto.Correo || u.NombreUsuario == dto.NombreUsuario))
            return BadRequest(new { message = "El usuario o correo ya está registrado." });

        var nuevoUsuario = new Usuario
        {
            NombreUsuario = dto.NombreUsuario.Trim(),
            Correo = dto.Correo.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            EstadoUsuario = true,
            FechaCreacion = DateTime.UtcNow
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

        return Ok(new { message = "Usuario creado exitosamente." });
    }

    // 3. Modificar usuario y roles
    [HttpPut("{id}")]
    public async Task<IActionResult> EditarUsuario(int id, [FromBody] EditarUsuarioDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null)
            return NotFound(new { message = "Usuario no encontrado." });

        usuario.NombreUsuario = dto.NombreUsuario.Trim();
        usuario.Correo = dto.Correo.Trim();

        // Solo se hashea y actualiza la clave si se proporcionó una nueva
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        // Actualizar asignación de roles
        _context.UsuarioRoles.RemoveRange(usuario.UsuarioRoles);
        if (dto.RolesIds != null)
        {
            foreach (var rolId in dto.RolesIds)
            {
                usuario.UsuarioRoles.Add(new UsuarioRol { IdUsuario = id, IdRol = rolId });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Usuario actualizado correctamente." });
    }

    // 4. Activar / Desactivar (Baja Lógica)
    [HttpPatch("{id}/toggle-estado")]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { message = "Usuario no encontrado." });

        usuario.EstadoUsuario = !usuario.EstadoUsuario;
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = usuario.EstadoUsuario ? "Usuario activado." : "Usuario desactivado.", 
            nuevoEstado = usuario.EstadoUsuario 
        });
    }

    // 5. Obtener lista de roles disponibles
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