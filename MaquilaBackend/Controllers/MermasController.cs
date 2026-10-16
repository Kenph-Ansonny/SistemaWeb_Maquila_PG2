using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using MaquilaBackend.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using MaquilaBackend.Security;
using System.Security.Claims;
using MaquilaBackend.Helpers;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MermasController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private readonly IKardexService _kardexService;
    private const int ID_MODULO_MERMAS = 9;

    private static readonly string[] TIPOS_MERMA_VALIDOS = 
    { 
        "Retazo Aprovechable", 
        "Desperdicio Irrecuperable", 
        "Prenda Defectuosa" 
    };

    public MermasController(MaquilaDbContext context, IKardexService kardexService)
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

    [HttpGet]
    [RequierePermiso("MERMAS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<MermaListDto>>> GetMermas(
        [FromQuery] int? idOrden = null,
        [FromQuery] string? tipoMerma = null,
        [FromQuery] string? busqueda = null)
    {
        var query = _context.RegistroMermas
            .Include(m => m.OrdenProduccion)
                .ThenInclude(o => o.PedidoDetalle)
                    .ThenInclude(pd => pd.Pedido)
            .Include(m => m.OrdenProduccion)
                .ThenInclude(o => o.PedidoDetalle)
                    .ThenInclude(pd => pd.ArticuloPrenda)
            .Include(m => m.ArticuloInsumo)
                .ThenInclude(a => a.UnidadMedida)
            .Include(m => m.AlmacenDestino)
            .AsNoTracking()
            .AsQueryable();

        if (idOrden.HasValue && idOrden.Value > 0)
            query = query.Where(m => m.IdOrdenProduccion == idOrden.Value);

        if (!string.IsNullOrWhiteSpace(tipoMerma))
            query = query.Where(m => m.TipoMerma == tipoMerma.Trim());

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(m =>
                m.OrdenProduccion.PedidoDetalle.Pedido.NumeroPedido.ToLower().Contains(term) ||
                m.ArticuloInsumo.NombreArticulo.ToLower().Contains(term) ||
                m.ArticuloInsumo.CodigoArticulo.ToLower().Contains(term));
        }

        var lista = await query
            .OrderByDescending(m => m.FechaRegistro)
            .Select(m => new MermaListDto(
                m.IdMerma,
                m.IdOrdenProduccion,
                m.OrdenProduccion.PedidoDetalle.Pedido.NumeroPedido,
                m.OrdenProduccion.PedidoDetalle.ArticuloPrenda.NombreArticulo,
                m.IdArticuloInsumo,
                m.ArticuloInsumo.CodigoArticulo,
                m.ArticuloInsumo.NombreArticulo,
                m.ArticuloInsumo.UnidadMedida.CodigoMedida,
                m.CantidadMermaReal,
                m.TipoMerma,
                m.IdAlmacenDestino,
                m.AlmacenDestino != null ? m.AlmacenDestino.NombreAlmacen : null,
                m.Observaciones,
                m.FechaRegistro
            ))
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("orden/{idOrden}/insumos")]
    [RequierePermiso("MERMAS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<InsumoOrdenTeoricoDto>>> ObtenerInsumosDeOrden(int idOrden)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle)
                .ThenInclude(pd => pd.Receta)
                    .ThenInclude(r => r.Detalles)
                        .ThenInclude(rd => rd.ArticuloInsumo)
                            .ThenInclude(a => a.UnidadMedida)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

        if (orden == null)
            return NotFound(new { message = "La orden de producción indicada no existe." });

        var mermasPrevias = await _context.RegistroMermas
            .Where(m => m.IdOrdenProduccion == idOrden)
            .GroupBy(m => m.IdArticuloInsumo)
            .Select(g => new { IdArticuloInsumo = g.Key, TotalMerma = g.Sum(x => x.CantidadMermaReal) })
            .ToDictionaryAsync(x => x.IdArticuloInsumo, x => x.TotalMerma);

        var resultado = orden.PedidoDetalle.Receta.Detalles.Select(rd =>
        {
            decimal brutaTotal = rd.CantidadBruta * orden.CantidadProgramada;
            decimal netaTotal = rd.CantidadNeta * orden.CantidadProgramada;
            decimal mermaEstimadaTotal = Math.Max(0, brutaTotal - netaTotal);
            decimal mermaYaRegistrada = mermasPrevias.GetValueOrDefault(rd.IdArticuloInsumo, 0);

            return new InsumoOrdenTeoricoDto(
                rd.IdArticuloInsumo,
                rd.ArticuloInsumo.CodigoArticulo,
                rd.ArticuloInsumo.NombreArticulo,
                rd.ArticuloInsumo.UnidadMedida.CodigoMedida,
                brutaTotal,
                rd.PorcentajeMerma,
                mermaEstimadaTotal,
                mermaYaRegistrada
            );
        }).ToList();

        return Ok(resultado);
    }

    [HttpPost]
    [RequierePermiso("MERMAS", AccionPermiso.Insertar)]
    public async Task<IActionResult> RegistrarMerma([FromBody] GuardarMermaDto dto)
    {
        if (dto.CantidadMermaReal <= 0)
            return BadRequest(new { message = "La cantidad física de merma debe ser mayor a cero." });

        if (string.IsNullOrWhiteSpace(dto.TipoMerma) || !TIPOS_MERMA_VALIDOS.Contains(dto.TipoMerma.Trim()))
            return BadRequest(new { message = $"Tipo de merma no válido. Opciones: {string.Join(", ", TIPOS_MERMA_VALIDOS)}." });

        var tipoLimpio = dto.TipoMerma.Trim();

        if (tipoLimpio == "Retazo Aprovechable" && (!dto.IdAlmacenDestino.HasValue || dto.IdAlmacenDestino.Value <= 0))
            return BadRequest(new { message = "Para 'Retazo Aprovechable' debes indicar el almacén destino donde ingresará el sobrante." });

        if (tipoLimpio != "Retazo Aprovechable" && dto.IdAlmacenDestino.HasValue)
            dto = dto with { IdAlmacenDestino = null };

        var orden = await _context.OrdenesProduccion.FindAsync(dto.IdOrdenProduccion);
        if (orden == null)
            return BadRequest(new { message = "La orden de producción seleccionada no existe." });

        if (orden.EstadoOrden == "Iniciada")
            return BadRequest(new { message = "No se puede registrar merma en una orden que aún no ha iniciado proceso de corte o costura." });

        if (orden.EstadoOrden == "Terminada" || orden.EstadoOrden == "Cancelada")
            return BadRequest(new { message = $"La orden de producción se encuentra '{orden.EstadoOrden}' y no admite registros." });

        var insumo = await _context.Articulos.FindAsync(dto.IdArticuloInsumo);
        if (insumo == null || !insumo.EstadoArticulo)
            return BadRequest(new { message = "El artículo/insumo indicado no existe o se encuentra inactivo." });

        if (dto.IdAlmacenDestino.HasValue)
        {
            var almacen = await _context.Almacenes.FindAsync(dto.IdAlmacenDestino.Value);
            if (almacen == null || !almacen.EstadoAlmacen)
                return BadRequest(new { message = "El almacén destino seleccionado no existe o está inactivo." });
        }

        var obsLimpias = dto.Observaciones?.Trim();
        if (!string.IsNullOrWhiteSpace(obsLimpias) && !Regex.IsMatch(obsLimpias, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]{0,250}$"))
            return BadRequest(new { message = "Las observaciones contienen caracteres no permitidos o exceden 250 caracteres." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevaMerma = new RegistroMerma
            {
                IdOrdenProduccion = dto.IdOrdenProduccion,
                IdArticuloInsumo = dto.IdArticuloInsumo,
                CantidadMermaReal = Math.Round(dto.CantidadMermaReal, 4),
                TipoMerma = tipoLimpio,
                IdAlmacenDestino = dto.IdAlmacenDestino,
                Observaciones = obsLimpias,
                FechaRegistro = DateTime.UtcNow
            };

            _context.RegistroMermas.Add(nuevaMerma);
            await _context.SaveChangesAsync();

            // INTEGRACIÓN KARDEX: Si es retazo aprovechable, ingresa al almacén seleccionado
            if (tipoLimpio == "Retazo Aprovechable" && dto.IdAlmacenDestino.HasValue)
            {
                await _kardexService.RegistrarMovimientoAsync(
                    idArticulo: nuevaMerma.IdArticuloInsumo,
                    idAlmacen: dto.IdAlmacenDestino.Value,
                    tipoMovimiento: "MERMA_CORTE",
                    tipoDocumentoReferencia: "REGISTRO_MERMAS",
                    idDocumentoReferencia: nuevaMerma.IdMerma,
                    cantidad: nuevaMerma.CantidadMermaReal,
                    costoUnitario: insumo.CostoPromedio,
                    idUsuario: idUsuario
                );
            }

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_MERMAS,
                Accion = "INSERT",
                TablaAfectada = "Registro_Mermas",
                IdRegistro = nuevaMerma.IdMerma.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Merma física registrada exitosamente en Kardex.", idMerma = nuevaMerma.IdMerma });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al registrar la merma: " + ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [RequierePermiso("MERMAS", AccionPermiso.Eliminar)]
    public async Task<IActionResult> EliminarMerma(long id)
    {
        var merma = await _context.RegistroMermas
            .Include(m => m.OrdenProduccion)
            .Include(m => m.ArticuloInsumo)
            .FirstOrDefaultAsync(m => m.IdMerma == id);

        if (merma == null)
            return NotFound(new { message = "El registro de merma no existe." });

        if (merma.OrdenProduccion.EstadoOrden == "Terminada" || merma.OrdenProduccion.EstadoOrden == "Cancelada")
            return BadRequest(new { message = $"No se puede anular la merma porque la orden está en estado '{merma.OrdenProduccion.EstadoOrden}'." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // REVERSIÓN KARDEX: Si era retazo aprovechable, descuenta el stock previamente sumado
            if (merma.TipoMerma == "Retazo Aprovechable" && merma.IdAlmacenDestino.HasValue)
            {
                await _kardexService.RegistrarMovimientoAsync(
                    idArticulo: merma.IdArticuloInsumo,
                    idAlmacen: merma.IdAlmacenDestino.Value,
                    tipoMovimiento: "AJUSTE_INVENTARIO",
                    tipoDocumentoReferencia: "ANULACION_MERMA",
                    idDocumentoReferencia: merma.IdMerma,
                    cantidad: -merma.CantidadMermaReal, // Salida por anulación
                    costoUnitario: merma.ArticuloInsumo.CostoPromedio,
                    idUsuario: idUsuario
                );
            }

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = ID_MODULO_MERMAS,
                Accion = "DELETE",
                TablaAfectada = "Registro_Mermas",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new
                {
                    merma.IdOrdenProduccion,
                    merma.IdArticuloInsumo,
                    merma.CantidadMermaReal,
                    merma.TipoMerma,
                    merma.IdAlmacenDestino
                }),
                ValoresNuevos = null,
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            _context.RegistroMermas.Remove(merma);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Registro de merma anulado correctamente y stock revertido en Kardex." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al anular la merma: " + ex.Message });
        }
    }
}