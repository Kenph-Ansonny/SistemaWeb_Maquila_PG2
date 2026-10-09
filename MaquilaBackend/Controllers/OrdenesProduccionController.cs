using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using MaquilaBackend.DTOs;
using MaquilaBackend.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MaquilaBackend.Security;
using System.Security.Claims;

namespace MaquilaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdenesProduccionController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private const int MODULO_PRODUCCION_ID = 8;

    // Máquina de estados, solo se permite avanzar en este orden, sin saltos ni retrocesos.
    private static readonly string[] SecuenciaEstados = { "Iniciada", "En Corte", "En Costura", "Terminada" };

    public OrdenesProduccionController(MaquilaDbContext context)
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

    private async Task SincronizarEstadoPedido(int idPedido)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Detalles).ThenInclude(d => d.OrdenesProduccion)
            .FirstOrDefaultAsync(p => p.IdPedido == idPedido);

        if (pedido == null) return;
        if (pedido.EstadoPedido == "Cancelado" || pedido.EstadoPedido == "Entregado") return;

        var tieneOrdenes = pedido.Detalles.Any(d => d.OrdenesProduccion.Any());

        string nuevoEstado;
        if (!tieneOrdenes)
        {
            nuevoEstado = "Registrado";
        }
        else
        {
            var todoCompletado = pedido.Detalles.All(d =>
                d.OrdenesProduccion.Any() &&
                d.OrdenesProduccion.All(o => o.EstadoOrden == "Terminada") &&
                d.OrdenesProduccion.Sum(o => o.CantidadProgramada) >= d.Cantidad);

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
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
    }

    [HttpGet]
    [RequierePermiso("PRODUCCION", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<OrdenProduccionListDto>>> GetOrdenes(
        [FromQuery] string? estado,
        [FromQuery] int? idPedido,
        [FromQuery] string? busqueda)
    {
        var query = _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Pedido).ThenInclude(p => p.Cliente)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.ArticuloPrenda)
            .Include(o => o.PedidoDetalle).ThenInclude(pd => pd.Receta)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!SecuenciaEstados.Contains(estado))
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

        var resultado = await query
            .OrderByDescending(o => o.FechaInicio)
            .Select(o => new OrdenProduccionListDto(
                o.IdOrden,
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

        return Ok(resultado);
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
                var programada = pd.OrdenesProduccion.Sum(o => o.CantidadProgramada);
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
                        .Select(o => new LoteOrdenResumenDto(o.IdOrden, o.CantidadProgramada, o.CantidadProducida, o.EstadoOrden))
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
                d.OrdenesProduccion.Sum(o => o.CantidadProgramada),
                d.OrdenesProduccion.Sum(o => o.CantidadProducida),
                d.OrdenesProduccion
                    .OrderBy(o => o.IdOrden)
                    .Select(o => new LoteOrdenResumenDto(o.IdOrden, o.CantidadProgramada, o.CantidadProducida, o.EstadoOrden))
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

        var yaProgramada = detalle.OrdenesProduccion.Sum(o => o.CantidadProgramada);
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
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            //sincroniza el estado del pedido.
            await SincronizarEstadoPedido(detalle.IdPedido);

            return Ok(new { message = "Orden de producción creada exitosamente.", idOrden = nuevaOrden.IdOrden });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al crear la orden: " + ex.Message });
        }
    }

    [HttpPut("{id}/avance")]
    [RequierePermiso("PRODUCCION", AccionPermiso.Modificar)]
    public async Task<IActionResult> RegistrarAvance(int id, [FromBody] RegistrarAvanceDto dto)
    {
        var orden = await _context.OrdenesProduccion
            .Include(o => o.PedidoDetalle)
            .FirstOrDefaultAsync(o => o.IdOrden == id);

        if (orden == null)
            return NotFound(new { message = "Orden de producción no encontrada." });

        if (orden.EstadoOrden == "Terminada")
            return BadRequest(new { message = "Esta orden ya fue finalizada y no admite más cambios." });

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

        var valoresAnteriores = new { orden.EstadoOrden, orden.CantidadProducida, orden.FechaFin };

        orden.EstadoOrden = dto.EstadoOrden;
        orden.CantidadProducida = dto.CantidadProducida;

        if (orden.EstadoOrden == "Terminada")
        {
            orden.FechaFin = DateTime.UtcNow;
            // Al terminar la orden, su cantidad completada siempre es el total programado
            orden.CantidadProducida = orden.CantidadProgramada;
        }

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = MODULO_PRODUCCION_ID,
            Accion = "AVANCE_PRODUCCION",
            TablaAfectada = "Ordenes_Produccion",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
            ValoresNuevos = JsonSerializer.Serialize(new { orden.EstadoOrden, orden.CantidadProducida, orden.FechaFin }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);

        await _context.SaveChangesAsync();

        await SincronizarEstadoPedido(orden.PedidoDetalle.IdPedido);

        return Ok(new
        {
            message = orden.EstadoOrden == "Terminada"
                ? "Orden marcada como terminada."
                : $"Avance registrado: {SecuenciaEstados[idxNuevo]}."
        });
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
            return BadRequest(new { message = "No se puede eliminar una orden que ya tiene producción o avance registrado. Si fue un error, contacta a un administrador." });

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
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
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