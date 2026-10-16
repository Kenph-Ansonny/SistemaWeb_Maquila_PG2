using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using MaquilaBackend.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MaquilaBackend.Security;
using System.Security.Claims;
using MaquilaBackend.Helpers;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdenesProduccionController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private readonly IKardexService _kardexService;
    private const int MODULO_PRODUCCION_ID = 8;
    private static readonly string[] SecuenciaEstados = { "Iniciada", "En Corte", "En Costura", "Terminada" };

    public OrdenesProduccionController(MaquilaDbContext context, IKardexService kardexService)
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

    private async Task SincronizarEstadoPedido(int idPedido)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Detalles).ThenInclude(d => d.OrdenesProduccion)
            .FirstOrDefaultAsync(p => p.IdPedido == idPedido);

        if (pedido == null || pedido.EstadoPedido == "Cancelado" || pedido.EstadoPedido == "Entregado") return;

        var tieneOrdenesActivas = pedido.Detalles.Any(d => d.OrdenesProduccion.Any(o => o.EstadoOrden != "Cancelada"));

        string nuevoEstado;
        if (!tieneOrdenesActivas)
        {
            nuevoEstado = "Registrado";
        }
        else
        {
            var todoCompletado = pedido.Detalles.All(d =>
            {
                var activas = d.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada").ToList();
                return activas.Any() && activas.All(o => o.EstadoOrden == "Terminada") && activas.Sum(o => o.CantidadProgramada) >= d.Cantidad;
            });

            nuevoEstado = todoCompletado ? "Finalizado" : "En Produccion";
        }

        if (pedido.EstadoPedido != nuevoEstado)
        {
            var anterior = pedido.EstadoPedido;
            pedido.EstadoPedido = nuevoEstado;

            _context.Bitacora.Add(new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "SYNC_ESTADO_PEDIDO",
                TablaAfectada = "Pedidos",
                IdRegistro = idPedido.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { EstadoPedido = anterior }),
                ValoresNuevos = JsonSerializer.Serialize(new { EstadoPedido = nuevoEstado }),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
    }

    [HttpGet]
    [RequierePermiso("PRODUCCION", AccionPermiso.Consultar)]
    public async Task<ActionResult<OrdenesPaginadasDto>> GetOrdenes(
        [FromQuery] string? estado,
        [FromQuery] int? idPedido,
        [FromQuery] string? busqueda,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanioPagina = 10,
        [FromQuery] bool ordenAscendente = false)
    {
        if (pagina < 1) pagina = 1;
        if (tamanioPagina < 1 || tamanioPagina > 100) tamanioPagina = 10;

        var query = _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Pedido).ThenInclude(p => p.Cliente)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.ArticuloPrenda)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Receta)
            .AsNoTracking()
            .AsQueryable();

        var estadosValidos = SecuenciaEstados.Append("Cancelada");

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!estadosValidos.Contains(estado))
                return BadRequest(new { message = "El estado proporcionado no es válido." });
            query = query.Where(o => o.EstadoOrden == estado);
        }

        if (idPedido.HasValue)
            query = query.Where(o => o.PedidoDetalle.IdPedido == idPedido.Value);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var texto = busqueda.Trim().ToLower();
            query = query.Where(o =>
                o.PedidoDetalle.Pedido.NumeroPedido.ToLower().Contains(texto) ||
                o.PedidoDetalle.Pedido.Cliente.NombreCliente.ToLower().Contains(texto) ||
                o.PedidoDetalle.ArticuloPrenda.NombreArticulo.ToLower().Contains(texto));
        }

        if (fechaDesde.HasValue)
            query = query.Where(o => o.FechaInicio >= fechaDesde.Value.Date);

        if (fechaHasta.HasValue)
            query = query.Where(o => o.FechaInicio < fechaHasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();

        var queryBase = _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Pedido).ThenInclude(p => p.Cliente)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.ArticuloPrenda)
            .AsNoTracking()
            .AsQueryable();

        if (idPedido.HasValue)
            queryBase = queryBase.Where(o => o.PedidoDetalle.IdPedido == idPedido.Value);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var texto = busqueda.Trim().ToLower();
            queryBase = queryBase.Where(o =>
                o.PedidoDetalle.Pedido.NumeroPedido.ToLower().Contains(texto) ||
                o.PedidoDetalle.Pedido.Cliente.NombreCliente.ToLower().Contains(texto) ||
                o.PedidoDetalle.ArticuloPrenda.NombreArticulo.ToLower().Contains(texto));
        }

        if (fechaDesde.HasValue)
            queryBase = queryBase.Where(o => o.FechaInicio >= fechaDesde.Value.Date);

        if (fechaHasta.HasValue)
            queryBase = queryBase.Where(o => o.FechaInicio < fechaHasta.Value.Date.AddDays(1));

        var conteos = await queryBase
            .GroupBy(o => o.EstadoOrden)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync();

        var conteosPorEstado = estadosValidos.ToDictionary(e => e, e => 0);
        foreach (var c in conteos)
            if (conteosPorEstado.ContainsKey(c.Estado))
                conteosPorEstado[c.Estado] = c.Cantidad;

        query = ordenAscendente
            ? query.OrderBy(o => o.FechaInicio).ThenBy(o => o.IdOrden)
            : query.OrderByDescending(o => o.FechaInicio).ThenByDescending(o => o.IdOrden);

        var items = await query
            .Skip((pagina - 1) * tamanioPagina)
            .Take(tamanioPagina)
            .Select(o => new OrdenProduccionListDto(
                o.IdOrden,
                o.IdLote,
                o.IdPedidoDetalle,
                o.PedidoDetalle.IdPedido,
                o.PedidoDetalle.Pedido.NumeroPedido,
                o.PedidoDetalle.Pedido.Cliente.NombreCliente,
                o.PedidoDetalle.IdArticuloPrenda,
                o.PedidoDetalle.ArticuloPrenda.NombreArticulo,
                o.PedidoDetalle.IdReceta,
                o.PedidoDetalle.Receta.NombreReceta,
                o.FechaInicio,
                o.FechaFin,
                o.CantidadProgramada,
                o.CantidadProducida,
                o.EstadoOrden
            ))
            .ToListAsync();

        var totalPaginas = (int)Math.Ceiling(total / (double)tamanioPagina);

        return Ok(new OrdenesPaginadasDto(items, total, pagina, tamanioPagina, totalPaginas, conteosPorEstado));
    }

    [HttpGet("{id}")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Consultar)]
    public async Task<ActionResult<OrdenProduccionDetalleDto>> GetOrdenPorId(int id)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Pedido).ThenInclude(p => p.Cliente)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.ArticuloPrenda)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Receta)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdOrden == id);

        if (orden == null)
            return NotFound(new { message = "Orden de producción no encontrada." });

        var dto = new OrdenProduccionDetalleDto(
            orden.IdOrden,
            orden.IdLote,
            orden.IdPedidoDetalle,
            orden.PedidoDetalle.IdPedido,
            orden.PedidoDetalle.Pedido.NumeroPedido,
            orden.PedidoDetalle.Pedido.Cliente.NombreCliente,
            orden.PedidoDetalle.Pedido.Cliente.TelefonoCliente,
            orden.PedidoDetalle.IdArticuloPrenda,
            orden.PedidoDetalle.ArticuloPrenda.NombreArticulo,
            orden.PedidoDetalle.IdReceta,
            orden.PedidoDetalle.Receta.NombreReceta,
            orden.PedidoDetalle.Cantidad,
            orden.PedidoDetalle.PrecioUnitarioAcordado,
            orden.FechaInicio,
            orden.FechaFin,
            orden.CantidadProgramada,
            orden.CantidadProducida,
            orden.EstadoOrden
        );

        return Ok(dto);
    }

    [HttpGet("pedidos-disponibles")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<PedidoDetalleDisponibleDto>>> GetPedidosDisponibles()
    {
        var detalles = await _context.PedidoDetalles
            .Include(pd => pd.Pedido).ThenInclude(p => p.Cliente)
            .Include(pd => pd.ArticuloPrenda)
            .Include(pd => pd.Receta)
            .Include(pd => pd.OrdenesProduccion)
            .Where(pd => pd.Pedido.EstadoPedido != "Cancelado" && pd.Pedido.EstadoPedido != "Entregado")
            .AsNoTracking()
            .ToListAsync();

        var resultado = detalles
            .Select(pd =>
            {
                var activas = pd.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada");
                var programada = activas.Sum(o => o.CantidadProgramada);
                var pendiente = pd.Cantidad - programada;
                return new PedidoDetalleDisponibleDto(
                    pd.IdDetalle,
                    pd.IdPedido,
                    pd.Pedido.NumeroPedido,
                    pd.Pedido.Cliente.NombreCliente,
                    pd.IdArticuloPrenda,
                    pd.ArticuloPrenda.NombreArticulo,
                    pd.IdReceta,
                    pd.Receta.NombreReceta,
                    pd.Cantidad,
                    programada,
                    pendiente,
                    pd.Pedido.FechaEstimadaEntrega,
                    pd.OrdenesProduccion
                        .OrderBy(o => o.IdOrden)
                        .Select(o => new LoteOrdenResumenDto(o.IdOrden, o.IdLote, o.CantidadProgramada, o.CantidadProducida, o.EstadoOrden))
                        .ToList()
                );
            })
            .Where(x => x.CantidadPendiente > 0)
            .OrderBy(x => x.FechaEstimadaEntrega)
            .ToList();

        return Ok(resultado);
    }

    [HttpGet("contexto-pedido/{idPedido}")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Consultar)]
    public async Task<ActionResult<ContextoPedidoDto>> GetContextoPedido(int idPedido)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Detalles).ThenInclude(d => d.ArticuloPrenda)
            .Include(p => p.Detalles).ThenInclude(d => d.Receta)
            .Include(p => p.Detalles).ThenInclude(d => d.OrdenesProduccion)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPedido == idPedido);

        if (pedido == null)
            return NotFound(new { message = "Pedido no encontrado." });

        var dto = new ContextoPedidoDto(
            pedido.IdPedido,
            pedido.NumeroPedido,
            pedido.Cliente.NombreCliente,
            pedido.EstadoPedido,
            pedido.Detalles.Select(d => new ContextoPrendaDto(
                d.IdDetalle,
                d.IdArticuloPrenda,
                d.ArticuloPrenda.NombreArticulo,
                d.Receta.NombreReceta,
                d.Cantidad,
                d.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada").Sum(o => o.CantidadProgramada),
                d.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada").Sum(o => o.CantidadProducida),
                d.OrdenesProduccion
                    .OrderBy(o => o.IdOrden)
                    .Select(o => new LoteOrdenResumenDto(o.IdOrden, o.IdLote, o.CantidadProgramada, o.CantidadProducida, o.EstadoOrden))
                    .ToList()
            )).ToList()
        );

        return Ok(dto);
    }

    [HttpPost]
    [RequierePermiso("PRODUCCION", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenProduccionDto dto)
    {
        if (dto.CantidadProgramada <= 0)
            return BadRequest(new { message = "La cantidad programada debe ser mayor a cero." });

        if (dto.FechaInicio == default)
            return BadRequest(new { message = "Debes indicar la fecha de inicio de la orden." });

        var detalle = await _context.PedidoDetalles
            .Include(pd => pd.Pedido)
            .Include(pd => pd.OrdenesProduccion)
            .FirstOrDefaultAsync(pd => pd.IdDetalle == dto.IdPedidoDetalle);

        if (detalle == null)
            return BadRequest(new { message = "El detalle de pedido seleccionado no existe." });

        if (detalle.Pedido.EstadoPedido == "Cancelado" || detalle.Pedido.EstadoPedido == "Entregado")
            return BadRequest(new { message = "No se pueden programar órdenes para un pedido cancelado o ya entregado." });

        var yaProgramada = detalle.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada").Sum(o => o.CantidadProgramada);
        var pendiente = detalle.Cantidad - yaProgramada;

        if (dto.CantidadProgramada > pendiente)
            return BadRequest(new { message = $"La cantidad excede el saldo pendiente de este pedido ({pendiente} unidades disponibles)." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevaOrden = new OrdenProduccion
            {
                IdPedidoDetalle = dto.IdPedidoDetalle,
                FechaInicio = dto.FechaInicio,
                CantidadProgramada = dto.CantidadProgramada,
                CantidadProducida = 0,
                EstadoOrden = "Iniciada"
            };

            _context.OrdenesProduccion.Add(nuevaOrden);
            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "INSERT",
                TablaAfectada = "Ordenes_Produccion",
                IdRegistro = nuevaOrden.IdOrden.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            await SincronizarEstadoPedido(detalle.IdPedido);

            return Ok(new { message = "Orden de producción creada exitosamente.", idOrden = nuevaOrden.IdOrden });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear la orden: " + ex.Message });
        }
    }

    [HttpPost("lote")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearLote([FromBody] CrearLoteProduccionDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { message = "Agrega al menos una prenda al lote." });

        if (dto.FechaInicio == default)
            return BadRequest(new { message = "Debes indicar la fecha de inicio del lote." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var creadas = new List<OrdenProduccion>();
            int? idPedidoComun = null;

            foreach (var item in dto.Items)
            {
                if (item.CantidadProgramada <= 0)
                    return BadRequest(new { message = "Cada prenda del lote debe tener una cantidad mayor a cero." });

                var detalle = await _context.PedidoDetalles
                    .Include(pd => pd.Pedido)
                    .Include(pd => pd.ArticuloPrenda)
                    .Include(pd => pd.OrdenesProduccion)
                    .FirstOrDefaultAsync(pd => pd.IdDetalle == item.IdPedidoDetalle);

                if (detalle == null)
                    return BadRequest(new { message = $"El detalle de pedido {item.IdPedidoDetalle} no existe." });

                if (detalle.Pedido.EstadoPedido == "Cancelado" || detalle.Pedido.EstadoPedido == "Entregado")
                    return BadRequest(new { message = "No se pueden programar órdenes para un pedido cancelado o ya entregado." });

                idPedidoComun ??= detalle.IdPedido;
                if (detalle.IdPedido != idPedidoComun)
                    return BadRequest(new { message = "Todas las prendas de un mismo lote deben pertenecer al mismo pedido." });

                var yaProgramada = detalle.OrdenesProduccion.Where(o => o.EstadoOrden != "Cancelada").Sum(o => o.CantidadProgramada);
                var pendiente = detalle.Cantidad - yaProgramada;

                if (item.CantidadProgramada > pendiente)
                    return BadRequest(new { message = $"'{detalle.ArticuloPrenda.NombreArticulo}' excede su saldo pendiente ({pendiente} unidades disponibles)." });

                var nueva = new OrdenProduccion
                {
                    IdPedidoDetalle = item.IdPedidoDetalle,
                    FechaInicio = dto.FechaInicio,
                    CantidadProgramada = item.CantidadProgramada,
                    CantidadProducida = 0,
                    EstadoOrden = "Iniciada"
                };
                _context.OrdenesProduccion.Add(nueva);
                creadas.Add(nueva);
            }

            await _context.SaveChangesAsync();

            var idLote = creadas.First().IdOrden;
            foreach (var orden in creadas) orden.IdLote = idLote;

            _context.Bitacora.Add(new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "INSERT_LOTE",
                TablaAfectada = "Ordenes_Produccion",
                IdRegistro = idLote.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (idPedidoComun.HasValue)
                await SincronizarEstadoPedido(idPedidoComun.Value);

            return Ok(new
            {
                message = $"Lote creado con {creadas.Count} prenda(s).",
                idLote,
                idsOrdenes = creadas.Select(o => o.IdOrden)
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear el lote: " + ex.Message });
        }
    }

    // REGISTRO DE AVANCE INTEGRADO CON KARDEX
    [HttpPut("{id}/avance")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Modificar)]
    public async Task<IActionResult> RegistrarAvance(int id, [FromBody] RegistrarAvanceDto dto)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle)
                .ThenInclude(pd => pd.ArticuloPrenda)
            .FirstOrDefaultAsync(o => o.IdOrden == id);

        if (orden == null)
            return NotFound(new { message = "Orden de producción no encontrada." });

        if (orden.EstadoOrden == "Terminada")
            return BadRequest(new { message = "Esta orden ya fue finalizada y no admite más cambios." });

        if (orden.EstadoOrden == "Cancelada")
            return BadRequest(new { message = "Esta orden fue cancelada y no admite más cambios." });

        var idxActual = Array.IndexOf(SecuenciaEstados, orden.EstadoOrden);
        var idxNuevo = Array.IndexOf(SecuenciaEstados, dto.EstadoOrden);

        if (idxNuevo == -1)
            return BadRequest(new { message = "El estado indicado no es válido." });

        if (idxNuevo < idxActual)
            return BadRequest(new { message = "No se puede retroceder el estado de una orden de producción." });

        if (idxNuevo > idxActual + 1)
            return BadRequest(new { message = $"No se pueden saltar etapas: la siguiente etapa válida es '{SecuenciaEstados[idxActual + 1]}'." });

        if (dto.CantidadProducida < 0)
            return BadRequest(new { message = "La cantidad no puede ser negativa." });

        if (dto.CantidadProducida > orden.CantidadProgramada)
            return BadRequest(new { message = $"La cantidad no puede exceder la cantidad programada ({orden.CantidadProgramada})." });

        int idUsuario = ObtenerUsuarioIdSesion();

        // 1. EVALUAR CONSUMO DE MATERIA PRIMA (Explosión de Ficha Técnica)
        bool entrandoACorte = (orden.EstadoOrden == "Iniciada" && dto.EstadoOrden == "En Corte");
        var insumosParaConsumir = new List<(int idArticulo, int idAlmacen, decimal cantidad, decimal costoUnitario)>();

        if (entrandoACorte)
        {
            var recetaDetalles = await _context.RecetaDetalles
                .Include(rd => rd.ArticuloInsumo)
                .Where(rd => rd.IdReceta == orden.PedidoDetalle.IdReceta)
                .ToListAsync();

            if (recetaDetalles.Count == 0)
                return BadRequest(new { message = "La prenda vinculada no tiene una receta con insumos configurada." });

            // Validación previa de stock de cada insumo antes de tocar la base de datos
            foreach (var rd in recetaDetalles)
            {
                decimal cantidadBrutaTotal = Math.Round(rd.CantidadBruta * orden.CantidadProgramada, 4);

                // Buscar almacén que disponga del insumo
                var existencia = await _context.Existencias
                    .Include(e => e.Almacen)
                    .Where(e => e.IdArticulo == rd.IdArticuloInsumo && e.StockActual > 0)
                    .OrderByDescending(e => e.StockActual)
                    .FirstOrDefaultAsync();

                if (existencia == null || existencia.StockActual < cantidadBrutaTotal)
                {
                    decimal disponible = existencia?.StockActual ?? 0m;
                    return BadRequest(new
                    {
                        message = $"Stock insuficiente de '{rd.ArticuloInsumo.NombreArticulo}'. Requerido para corte: {cantidadBrutaTotal:N2}, disponible: {disponible:N2}."
                    });
                }

                insumosParaConsumir.Add((rd.IdArticuloInsumo, existencia.IdAlmacen, cantidadBrutaTotal, rd.ArticuloInsumo.CostoPromedio));
            }
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // A. Registrar en Kardex la salida de telas (CONSUMO_PRODUCCION)
            if (entrandoACorte && insumosParaConsumir.Count > 0)
            {
                foreach (var ins in insumosParaConsumir)
                {
                    await _kardexService.RegistrarMovimientoAsync(
                        idArticulo: ins.idArticulo,
                        idAlmacen: ins.idAlmacen,
                        tipoMovimiento: "CONSUMO_PRODUCCION",
                        tipoDocumentoReferencia: "ORDENES_PRODUCCION",
                        idDocumentoReferencia: orden.IdOrden,
                        cantidad: -ins.cantidad, // Salida
                        costoUnitario: ins.costoUnitario,
                        idUsuario: idUsuario
                    );
                }
            }

            // B. Registrar en Kardex la entrada de prendas terminadas (ENTRADA_PRODUCIDO)
            if (dto.EstadoOrden == "Terminada")
            {
                orden.FechaFin = DateTime.UtcNow;
                orden.CantidadProducida = orden.CantidadProgramada;

                // Buscar almacén de producto terminado
                var almacenPT = await _context.Almacenes
                    .FirstOrDefaultAsync(a => a.EstadoAlmacen && (a.NombreAlmacen.Contains("Terminado") || a.NombreAlmacen.Contains("Prenda")))
                    ?? await _context.Almacenes.FirstOrDefaultAsync(a => a.EstadoAlmacen);

                if (almacenPT != null)
                {
                    await _kardexService.RegistrarMovimientoAsync(
                        idArticulo: orden.PedidoDetalle.IdArticuloPrenda,
                        idAlmacen: almacenPT.IdAlmacen,
                        tipoMovimiento: "ENTRADA_PRODUCIDO",
                        tipoDocumentoReferencia: "ORDENES_PRODUCCION",
                        idDocumentoReferencia: orden.IdOrden,
                        cantidad: orden.CantidadProgramada,
                        costoUnitario: orden.PedidoDetalle.ArticuloPrenda.CostoPromedio,
                        idUsuario: idUsuario
                    );
                }
            }

            var valoresAnteriores = new { orden.EstadoOrden, orden.CantidadProducida, orden.FechaFin };
            orden.EstadoOrden = dto.EstadoOrden;
            orden.CantidadProducida = dto.CantidadProducida;

            var log = new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "AVANCE_PRODUCCION",
                TablaAfectada = "Ordenes_Produccion",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(new { orden.EstadoOrden, orden.CantidadProducida, orden.FechaFin }),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await SincronizarEstadoPedido(orden.PedidoDetalle.IdPedido);

            return Ok(new
            {
                message = orden.EstadoOrden == "Terminada"
                    ? "Orden completada y prendas ingresadas al Kardex."
                    : entrandoACorte
                        ? "Orden en corte: telas descargadas de inventario y asentadas en Kardex."
                        : $"Avance registrado: {SecuenciaEstados[idxNuevo]}."
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al registrar el avance: " + ex.Message });
        }
    }

    [HttpPatch("{id}/cancelar")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Eliminar)]
    public async Task<IActionResult> CancelarOrden(int id)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle)
            .FirstOrDefaultAsync(o => o.IdOrden == id);

        if (orden == null)
            return NotFound(new { message = "Orden de producción no encontrada." });

        if (orden.EstadoOrden == "Terminada")
            return BadRequest(new { message = "No se puede cancelar una orden ya terminada." });

        if (orden.EstadoOrden == "Cancelada")
            return BadRequest(new { message = "Esta orden ya está cancelada." });

        if (orden.CantidadProducida > 0)
            return BadRequest(new { message = "No se puede cancelar una orden con avance físico registrado. Registre las mermas o finalícela." });

        int idUsuario = ObtenerUsuarioIdSesion();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Si la orden ya había descargado telas (estaba En Corte o En Costura), se devuelven al almacén
            var consumos = await _context.KardexMovimientos
                .Where(k => k.TipoDocumentoReferencia == "ORDENES_PRODUCCION" &&
                            k.IdDocumentoReferencia == orden.IdOrden &&
                            k.TipoMovimiento == "CONSUMO_PRODUCCION")
                .ToListAsync();

            foreach (var c in consumos)
            {
                await _kardexService.RegistrarMovimientoAsync(
                    idArticulo: c.IdArticulo,
                    idAlmacen: c.IdAlmacen,
                    tipoMovimiento: "AJUSTE_INVENTARIO",
                    tipoDocumentoReferencia: "CANCELACION_OP",
                    idDocumentoReferencia: orden.IdOrden,
                    cantidad: Math.Abs(c.Cantidad), // Reingreso positivo
                    costoUnitario: c.CostoUnitario,
                    idUsuario: idUsuario
                );
            }

            var estadoAnterior = orden.EstadoOrden;
            orden.EstadoOrden = "Cancelada";

            _context.Bitacora.Add(new Bitacora
            {
                IdUsuario = idUsuario,
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "CANCELAR",
                TablaAfectada = "Ordenes_Produccion",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { EstadoOrden = estadoAnterior }),
                ValoresNuevos = JsonSerializer.Serialize(new { EstadoOrden = "Cancelada" }),
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await SincronizarEstadoPedido(orden.PedidoDetalle.IdPedido);

            return Ok(new { message = "Orden cancelada y telas devueltas al Kardex correctamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al cancelar la orden: " + ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Eliminar)]
    public async Task<IActionResult> EliminarOrden(int id)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle)
            .FirstOrDefaultAsync(o => o.IdOrden == id);

        if (orden == null)
            return NotFound(new { message = "Orden de producción no encontrada." });

        if (orden.EstadoOrden != "Iniciada" || orden.CantidadProducida > 0)
            return BadRequest(new { message = "Solo se pueden eliminar órdenes en estado 'Iniciada' sin avance ni consumo de tela." });

        var idPedido = orden.PedidoDetalle.IdPedido;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = MODULO_PRODUCCION_ID,
                Accion = "DELETE",
                TablaAfectada = "Ordenes_Produccion",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { orden.IdPedidoDetalle, orden.CantidadProgramada, orden.FechaInicio }),
                ValoresNuevos = null,
                DireccionIp = HttpContext.ObtenerIpCliente(),
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            _context.OrdenesProduccion.Remove(orden);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            await SincronizarEstadoPedido(idPedido);

            return Ok(new { message = "Orden de producción eliminada." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al eliminar: " + ex.Message });
        }
    }
}