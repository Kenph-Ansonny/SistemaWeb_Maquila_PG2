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

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private const int ID_MODULO_CLIENTES = 6; 

    public ClientesController(MaquilaDbContext context)
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
    [RequierePermiso("CLIENTES", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ClienteListDto>>> GetClientes()
    {
        var clientes = await _context.Clientes
            .AsNoTracking()
            .Select(c => new ClienteListDto(
                c.IdCliente,
                c.NombreCliente,
                c.TelefonoCliente,
                c.DireccionCliente,
                c.EstadoCliente,
                c.Pedidos.Count
            ))
            .ToListAsync();

        return Ok(clientes);
    }

    [HttpGet("opciones")]
    [RequierePermiso("CLIENTES", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ClienteOptionDto>>> GetClientesOpciones()
    {
        var clientes = await _context.Clientes
            .Where(c => c.EstadoCliente)
            .AsNoTracking()
            .OrderBy(c => c.NombreCliente)
            .Select(c => new ClienteOptionDto(
                c.IdCliente,
                c.NombreCliente
            ))
            .ToListAsync();

        return Ok(clientes);
    }

    [HttpGet("{id}")]
    [RequierePermiso("CLIENTES", AccionPermiso.Consultar)]
    public async Task<ActionResult<ClienteListDto>> GetClientePorId(int id)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .Select(c => new ClienteListDto(
                c.IdCliente,
                c.NombreCliente,
                c.TelefonoCliente,
                c.DireccionCliente,
                c.EstadoCliente,
                c.Pedidos.Count
            ))
            .FirstOrDefaultAsync(c => c.IdCliente == id);

        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado." });

        return Ok(cliente);
    }

    [HttpPost]
    [RequierePermiso("CLIENTES", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearCliente([FromBody] GuardarClienteDto dto)
    {
        var nombreLimpio = dto.NombreCliente?.Trim() ?? string.Empty;
        var telefonoLimpio = dto.TelefonoCliente?.Trim();
        var direccionLimpia = dto.DireccionCliente?.Trim();

        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,150}$"))
            return BadRequest(new { message = "El nombre del cliente contiene caracteres inválidos o longitud incorrecta (mínimo 3, máximo 150 caracteres)." });

        if (!string.IsNullOrWhiteSpace(telefonoLimpio) && !Regex.IsMatch(telefonoLimpio, @"^[0-9\+\-\s\(\)]{7,20}$"))
            return BadRequest(new { message = "El formato del teléfono no es válido (admite números, +, guiones, espacios y paréntesis, entre 7 y 20 caracteres)." });

        if (!string.IsNullOrWhiteSpace(direccionLimpia) && !Regex.IsMatch(direccionLimpia, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,250}$"))
            return BadRequest(new { message = "La dirección contiene caracteres no permitidos o excede 250 caracteres." });

        if (await _context.Clientes.AnyAsync(c => c.NombreCliente.ToLower() == nombreLimpio.ToLower()))
            return BadRequest(new { message = "Ya existe un cliente registrado con ese nombre comercial." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoCliente = new Cliente
            {
                NombreCliente = nombreLimpio,
                TelefonoCliente = string.IsNullOrWhiteSpace(telefonoLimpio) ? null : telefonoLimpio,
                DireccionCliente = string.IsNullOrWhiteSpace(direccionLimpia) ? null : direccionLimpia,
                EstadoCliente = true
            };

            _context.Clientes.Add(nuevoCliente);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = ID_MODULO_CLIENTES,
                Accion = "INSERT",
                TablaAfectada = "Clientes",
                IdRegistro = nuevoCliente.IdCliente.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Cliente registrado exitosamente.", idCliente = nuevoCliente.IdCliente });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al guardar: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    [RequierePermiso("CLIENTES", AccionPermiso.Modificar)]
    public async Task<IActionResult> EditarCliente(int id, [FromBody] GuardarClienteDto dto)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado." });

        var nombreLimpio = dto.NombreCliente?.Trim() ?? string.Empty;
        var telefonoLimpio = dto.TelefonoCliente?.Trim();
        var direccionLimpia = dto.DireccionCliente?.Trim();

        if (!Regex.IsMatch(nombreLimpio, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{3,150}$"))
            return BadRequest(new { message = "El nombre del cliente contiene caracteres inválidos (mínimo 3, máximo 150 caracteres)." });

        if (!string.IsNullOrWhiteSpace(telefonoLimpio) && !Regex.IsMatch(telefonoLimpio, @"^[0-9\+\-\s\(\)]{7,20}$"))
            return BadRequest(new { message = "El formato del teléfono no es válido." });

        if (!string.IsNullOrWhiteSpace(direccionLimpia) && !Regex.IsMatch(direccionLimpia, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,250}$"))
            return BadRequest(new { message = "La dirección contiene caracteres no permitidos." });

        if (await _context.Clientes.AnyAsync(c => c.NombreCliente.ToLower() == nombreLimpio.ToLower() && c.IdCliente != id))
            return BadRequest(new { message = "Ya existe otro cliente registrado con este nombre." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new
            {
                cliente.NombreCliente,
                cliente.TelefonoCliente,
                cliente.DireccionCliente
            };

            cliente.NombreCliente = nombreLimpio;
            cliente.TelefonoCliente = string.IsNullOrWhiteSpace(telefonoLimpio) ? null : telefonoLimpio;
            cliente.DireccionCliente = string.IsNullOrWhiteSpace(direccionLimpia) ? null : direccionLimpia;

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = ID_MODULO_CLIENTES,
                Accion = "UPDATE",
                TablaAfectada = "Clientes",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Cliente actualizado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar: " + ex.Message });
        }
    }

    [HttpPatch("{id}/toggle-estado")]
    [RequierePermiso("CLIENTES", AccionPermiso.Eliminar)]
    public async Task<IActionResult> ToggleEstado(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado." });

        cliente.EstadoCliente = !cliente.EstadoCliente;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = ID_MODULO_CLIENTES,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Clientes",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoCliente = !cliente.EstadoCliente }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoCliente = cliente.EstadoCliente }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = cliente.EstadoCliente ? "Cliente activado exitosamente." : "Cliente desactivado." });
    }
}