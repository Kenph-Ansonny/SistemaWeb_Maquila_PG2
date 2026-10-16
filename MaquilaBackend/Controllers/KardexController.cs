using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using MaquilaBackend.Services;
using MaquilaBackend.Security;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using MaquilaBackend.Helpers;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KardexController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private readonly IKardexService _kardexService;
    private const int ID_MODULO_KARDEX = 10;

    public KardexController(MaquilaDbContext context, IKardexService kardexService)
    {
        _context = context;
        _kardexService = kardexService;
    }

    private int ObtenerUsuarioIdSesion()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int userId))
            throw new UnauthorizedAccessException("No se pudo identificar al usuario autenticado.");
        return userId;
    }

    // GET: api/kardex
    [HttpGet]
    [RequierePermiso("KARDEX", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<KardexMovimientoListDto>>> GetMovimientos(
        [FromQuery] int? idArticulo = null,
        [FromQuery] int? idAlmacen = null,
        [FromQuery] string? tipoMovimiento = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? busqueda = null)
    {
        var query = _context.KardexMovimientos
            .Include(k => k.Articulo)
                .ThenInclude(a => a.UnidadMedida)
            .Include(k => k.Almacen)
            .Include(k => k.Usuario)
            .AsNoTracking()
            .AsQueryable();

        if (idArticulo.HasValue && idArticulo.Value > 0)
            query = query.Where(k => k.IdArticulo == idArticulo.Value);

        if (idAlmacen.HasValue && idAlmacen.Value > 0)
            query = query.Where(k => k.IdAlmacen == idAlmacen.Value);

        if (!string.IsNullOrWhiteSpace(tipoMovimiento))
            query = query.Where(k => k.TipoMovimiento == tipoMovimiento.Trim());

        if (fechaDesde.HasValue)
            query = query.Where(k => k.FechaMovimiento >= fechaDesde.Value.Date);

        if (fechaHasta.HasValue)
            query = query.Where(k => k.FechaMovimiento <= fechaHasta.Value.Date.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(k =>
                k.Articulo.NombreArticulo.ToLower().Contains(term) ||
                k.Articulo.CodigoArticulo.ToLower().Contains(term) ||
                k.TipoDocumentoReferencia.ToLower().Contains(term));
        }

        var resultado = await query
            .OrderByDescending(k => k.FechaMovimiento)
            .Select(k => new KardexMovimientoListDto(
                k.IdKardex,
                k.IdArticulo,
                k.Articulo.CodigoArticulo,
                k.Articulo.NombreArticulo,
                k.Articulo.TipoArticulo,
                k.Articulo.UnidadMedida.CodigoMedida,
                k.IdAlmacen,
                k.Almacen.NombreAlmacen,
                k.TipoMovimiento,
                k.TipoDocumentoReferencia,
                k.IdDocumentoReferencia,
                k.Cantidad,
                k.StockAnterior,
                k.StockPosterior,
                k.CostoUnitario,
                Math.Round(Math.Abs(k.Cantidad) * k.CostoUnitario, 2),
                k.IdUsuario,
                k.Usuario.NombreUsuario,
                k.FechaMovimiento
            ))
            .ToListAsync();

        return Ok(resultado);
    }

    // GET: api/kardex/articulos/existencias
    [HttpGet("articulos/existencias")]
    [RequierePermiso("KARDEX", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<ArticuloExistenciaKardexDto>>> GetExistenciasConsolidadas(
        [FromQuery] string? tipoArticulo = null)
    {
        var query = _context.Articulos
            .Include(a => a.UnidadMedida)
            .Include(a => a.Existencias)
                .ThenInclude(e => e.Almacen)
            .Where(a => a.EstadoArticulo)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tipoArticulo))
            query = query.Where(a => a.TipoArticulo == tipoArticulo.Trim());

        var lista = await query
            .OrderBy(a => a.NombreArticulo)
            .Select(a => new ArticuloExistenciaKardexDto(
                a.IdArticulo,
                a.CodigoArticulo,
                a.NombreArticulo,
                a.TipoArticulo,
                a.UnidadMedida.CodigoMedida,
                a.CostoPromedio,
                a.Existencias.Sum(e => e.StockActual),
                a.StockMinimo,
                a.Existencias.Select(e => new AlmacenStockDto(
                    e.IdAlmacen,
                    e.Almacen.NombreAlmacen,
                    e.StockActual
                )).ToList()
            ))
            .ToListAsync();

        return Ok(lista);
    }

    // POST: api/kardex/ajuste
    [HttpPost("ajuste")]
    [RequierePermiso("KARDEX", AccionPermiso.Insertar)]
    public async Task<IActionResult> RegistrarAjusteInventario([FromBody] RegistrarAjusteInventarioDto dto)
    {
        if (dto.CantidadAjuste == 0)
            return BadRequest(new { message = "La cantidad del ajuste no puede ser cero." });

        if (string.IsNullOrWhiteSpace(dto.MotivoAjuste))
            return BadRequest(new { message = "Debes indicar el motivo técnico del ajuste de inventario." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var movimiento = await _kardexService.RegistrarMovimientoAsync(
                idArticulo: dto.IdArticulo,
                idAlmacen: dto.IdAlmacen,
                tipoMovimiento: "AJUSTE_INVENTARIO",
                tipoDocumentoReferencia: "AJUSTE_MANUAL",
                idDocumentoReferencia: 0,
                cantidad: dto.CantidadAjuste,
                costoUnitario: dto.CostoUnitario ?? 0m,
                idUsuario: idUsuario
            );

            // Registrar en Bitácora
            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_KARDEX,
                Accion = "INSERT",
                TablaAfectada = "Kardex_Movimientos",
                IdRegistro = movimiento.IdKardex.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Ajuste de inventario registrado con éxito en Kardex.", idKardex = movimiento.IdKardex });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }
}