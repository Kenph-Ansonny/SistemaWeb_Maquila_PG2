using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnidadesController : ControllerBase
{
    private readonly MaquilaDbContext _context;

    public UnidadesController(MaquilaDbContext context)
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

    // Endpoins Unidades de Medida

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnidadMedidaDto>>> GetUnidades()
    {
        var unidades = await _context.UnidadesMedida
            .Include(u => u.Articulos)
            .AsNoTracking()
            .Select(u => new UnidadMedidaDto(
                u.IdUnidadMedida,
                u.CodigoMedida,
                u.NombreMedida,
                u.TipoMedida,
                u.Articulos.Count
            ))
            .ToListAsync();

        return Ok(unidades);
    }

    [HttpPost]
    public async Task<IActionResult> CrearUnidad([FromBody] GuardarUnidadMedidaDto dto)
    {
        var codigo = dto.CodigoMedida?.Trim().ToUpper() ?? string.Empty;
        var nombre = dto.NombreMedida?.Trim() ?? string.Empty;

        if (!Regex.IsMatch(codigo, @"^[A-Z0-9_-]{1,10}$"))
            return BadRequest(new { message = "El código solo admite de 1 a 10 caracteres alfanuméricos en mayúsculas sin espacios." });

        if (!Regex.IsMatch(nombre, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{2,50}$"))
            return BadRequest(new { message = "El nombre de la unidad contiene caracteres no permitidos." });

        var tiposValidos = new[] { "Longitud", "Masa", "Unidad", "Volumen" };
        if (!tiposValidos.Contains(dto.TipoMedida))
            return BadRequest(new { message = "Tipo de medida no reconocido por el sistema." });

        if (await _context.UnidadesMedida.AnyAsync(u => u.CodigoMedida == codigo))
            return BadRequest(new { message = "Ya existe una unidad registrada con ese código." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevaUnidad = new UnidadMedida
            {
                CodigoMedida = codigo,
                NombreMedida = nombre,
                TipoMedida = dto.TipoMedida
            };

            _context.UnidadesMedida.Add(nuevaUnidad);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 12, // UNIDADES
                Accion = "INSERT",
                TablaAfectada = "Unidades_Medida",
                IdRegistro = nuevaUnidad.IdUnidadMedida.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Unidad de medida creada exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear unidad: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> EditarUnidad(int id, [FromBody] GuardarUnidadMedidaDto dto)
    {
        var unidad = await _context.UnidadesMedida.FindAsync(id);
        if (unidad == null) return NotFound(new { message = "Unidad no encontrada." });

        var codigo = dto.CodigoMedida?.Trim().ToUpper() ?? string.Empty;
        var nombre = dto.NombreMedida?.Trim() ?? string.Empty;

        if (!Regex.IsMatch(codigo, @"^[A-Z0-9_-]{1,10}$"))
            return BadRequest(new { message = "El código solo admite de 1 a 10 caracteres alfanuméricos." });

        if (!Regex.IsMatch(nombre, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]{2,50}$"))
            return BadRequest(new { message = "El nombre de la unidad contiene caracteres no permitidos." });

        if (await _context.UnidadesMedida.AnyAsync(u => u.CodigoMedida == codigo && u.IdUnidadMedida != id))
            return BadRequest(new { message = "El código ya pertenece a otra unidad." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var anterior = new { unidad.CodigoMedida, unidad.NombreMedida, unidad.TipoMedida };
            unidad.CodigoMedida = codigo;
            unidad.NombreMedida = nombre;
            unidad.TipoMedida = dto.TipoMedida;

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 12,
                Accion = "UPDATE",
                TablaAfectada = "Unidades_Medida",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(anterior),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { message = "Unidad de medida modificada exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar: " + ex.Message });
        }
    }

    // Endpoins Conversiones

    [HttpGet("conversiones")]
    public async Task<ActionResult<IEnumerable<UnidadConversionListDto>>> GetConversiones()
    {
        var conversiones = await _context.UnidadConversiones
            .Include(uc => uc.UnidadOrigen)
            .Include(uc => uc.UnidadDestino)
            .AsNoTracking()
            .Select(uc => new UnidadConversionListDto(
                uc.IdUnidadOrigen,
                uc.UnidadOrigen.CodigoMedida,
                uc.UnidadOrigen.NombreMedida,
                uc.IdUnidadDestino,
                uc.UnidadDestino.CodigoMedida,
                uc.UnidadDestino.NombreMedida,
                uc.UnidadOrigen.TipoMedida,
                uc.FactorConversion
            ))
            .ToListAsync();

        return Ok(conversiones);
    }

    [HttpPost("conversiones")]
    public async Task<IActionResult> GuardarConversion([FromBody] GuardarConversionDto dto)
    {
        if (dto.IdUnidadOrigen == dto.IdUnidadDestino)
            return BadRequest(new { message = "La unidad origen y destino no pueden ser la misma." });

        if (dto.FactorConversion <= 0)
            return BadRequest(new { message = "El factor de conversión debe ser estrictamente mayor a 0." });

        var uOrigen = await _context.UnidadesMedida.FindAsync(dto.IdUnidadOrigen);
        var uDestino = await _context.UnidadesMedida.FindAsync(dto.IdUnidadDestino);

        if (uOrigen == null || uDestino == null)
            return NotFound(new { message = "Una o ambas unidades no existen en el sistema." });

        if (uOrigen.TipoMedida != uDestino.TipoMedida)
            return BadRequest(new { message = $"Incompatibilidad de dimensiones: No se puede convertir {uOrigen.TipoMedida} a {uDestino.TipoMedida}." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Búsqueda de conversión directa
            var conversionDirecta = await _context.UnidadConversiones
                .FirstOrDefaultAsync(c => c.IdUnidadOrigen == dto.IdUnidadOrigen && c.IdUnidadDestino == dto.IdUnidadDestino);

            if (conversionDirecta == null)
            {
                _context.UnidadConversiones.Add(new UnidadConversion
                {
                    IdUnidadOrigen = dto.IdUnidadOrigen,
                    IdUnidadDestino = dto.IdUnidadDestino,
                    FactorConversion = dto.FactorConversion
                });
            }
            else
            {
                conversionDirecta.FactorConversion = dto.FactorConversion;
            }

            // Registro automático de la relación inversa calculada: 1 / factor
            decimal factorInverso = Math.Round(1m / dto.FactorConversion, 6);
            var conversionInversa = await _context.UnidadConversiones
                .FirstOrDefaultAsync(c => c.IdUnidadOrigen == dto.IdUnidadDestino && c.IdUnidadDestino == dto.IdUnidadOrigen);

            if (conversionInversa == null)
            {
                _context.UnidadConversiones.Add(new UnidadConversion
                {
                    IdUnidadOrigen = dto.IdUnidadDestino,
                    IdUnidadDestino = dto.IdUnidadOrigen,
                    FactorConversion = factorInverso
                });
            }
            else
            {
                conversionInversa.FactorConversion = factorInverso;
            }

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = 12,
                Accion = "SET_CONVERSION",
                TablaAfectada = "Unidad_Conversiones",
                IdRegistro = $"{dto.IdUnidadOrigen}->{dto.IdUnidadDestino}",
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(new { dto.FactorConversion, FactorInverso = factorInverso }),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Factor de conversión y su recíproco guardados exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al registrar la conversión: " + ex.Message });
        }
    }

    [HttpDelete("conversiones/{origenId}/{destinoId}")]
    public async Task<IActionResult> EliminarConversion(int origenId, int destinoId)
    {
        var conv = await _context.UnidadConversiones
            .FirstOrDefaultAsync(c => c.IdUnidadOrigen == origenId && c.IdUnidadDestino == destinoId);

        if (conv == null) return NotFound(new { message = "Conversión no encontrada." });

        _context.UnidadConversiones.Remove(conv);

        // Remover recíproco si existe
        var reciproco = await _context.UnidadConversiones
            .FirstOrDefaultAsync(c => c.IdUnidadOrigen == destinoId && c.IdUnidadDestino == origenId);
        if (reciproco != null) _context.UnidadConversiones.Remove(reciproco);

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = 12,
            Accion = "DELETE",
            TablaAfectada = "Unidad_Conversiones",
            IdRegistro = $"{origenId}->{destinoId}",
            ValoresAnteriores = JsonSerializer.Serialize(new { conv.FactorConversion }),
            ValoresNuevos = null,
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Relación de conversión eliminada correctamente." });
    }
}