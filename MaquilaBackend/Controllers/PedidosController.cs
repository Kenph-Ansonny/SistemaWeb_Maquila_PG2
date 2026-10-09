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
public class PedidosController : ControllerBase
{
    private readonly MaquilaDbContext _context;
    private const int ID_MODULO_PEDIDOS = 7; 

    private static readonly string[] ESTADOS_VALIDOS = 
    { 
        "Registrado", 
        "En Produccion", 
        "Finalizado", 
        "Entregado", 
        "Cancelado" 
    };

    private static readonly Dictionary<string, string[]> TRANSICIONES_VALIDAS = new()
    {
        ["Registrado"]     = new[] { "En Produccion", "Cancelado" },
        ["En Produccion"]  = new[] { "Finalizado", "Cancelado" },
        ["Finalizado"]     = new[] { "Entregado" },
        ["Entregado"]      = Array.Empty<string>(),
        ["Cancelado"]      = Array.Empty<string>()
    };

    public PedidosController(MaquilaDbContext context)
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
    [RequierePermiso("PEDIDOS", AccionPermiso.Consultar)]
    public async Task<ActionResult<IEnumerable<PedidoListDto>>> GetPedidos(
        [FromQuery] string? estado = null,
        [FromQuery] int? idCliente = null,
        [FromQuery] string? busqueda = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.OrdenesProduccion)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(p => p.EstadoPedido == estado.Trim());

        if (idCliente.HasValue && idCliente.Value > 0)
            query = query.Where(p => p.IdCliente == idCliente.Value);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = busqueda.Trim().ToLower();
            query = query.Where(p => 
                p.NumeroPedido.ToLower().Contains(term) || 
                p.Cliente.NombreCliente.ToLower().Contains(term));
        }

        var lista = await query
            .OrderByDescending(p => p.FechaEmision)
            .Select(p => new PedidoListDto(
                p.IdPedido,
                p.NumeroPedido,
                p.IdCliente,
                p.Cliente.NombreCliente,
                p.IdUsuario,
                p.Usuario.NombreUsuario,
                p.FechaEmision,
                p.FechaEstimadaEntrega,
                p.EstadoPedido,
                p.Detalles.Sum(d => d.Cantidad),
                p.Detalles.Sum(d => d.Cantidad * d.PrecioUnitarioAcordado),
                p.Detalles.Count,
                p.Detalles.SelectMany(d => d.OrdenesProduccion).Count()
            ))
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("{id}")]
    [RequierePermiso("PEDIDOS", AccionPermiso.Consultar)]
    public async Task<ActionResult<PedidoCompletoDto>> GetPedidoPorId(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.ArticuloPrenda)
                    .ThenInclude(a => a.DetallePrenda)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Receta)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.OrdenesProduccion)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPedido == id);

        if (pedido == null)
            return NotFound(new { message = "El pedido solicitado no existe." });

        var detallesDto = pedido.Detalles.Select(d =>
        {
            int cantProgramada = d.OrdenesProduccion.Sum(o => o.CantidadProgramada);
            return new PedidoDetalleDto(
                d.IdDetalle,
                d.IdArticuloPrenda,
                d.ArticuloPrenda.CodigoArticulo,
                d.ArticuloPrenda.NombreArticulo,
                d.ArticuloPrenda.DetallePrenda?.Talla,
                d.ArticuloPrenda.DetallePrenda?.Color,
                d.IdReceta,
                d.Receta.NombreReceta,
                d.Cantidad,
                d.PrecioUnitarioAcordado,
                d.Cantidad * d.PrecioUnitarioAcordado,
                cantProgramada,
                Math.Max(0, d.Cantidad - cantProgramada)
            );
        }).ToList();

        var resultado = new PedidoCompletoDto(
            pedido.IdPedido,
            pedido.NumeroPedido,
            pedido.IdCliente,
            pedido.Cliente.NombreCliente,
            pedido.Cliente.TelefonoCliente,
            pedido.Cliente.DireccionCliente,
            pedido.IdUsuario,
            pedido.Usuario.NombreUsuario,
            pedido.FechaEmision,
            pedido.FechaEstimadaEntrega,
            pedido.EstadoPedido,
            pedido.Detalles.Sum(d => d.Cantidad),
            pedido.Detalles.Sum(d => d.Cantidad * d.PrecioUnitarioAcordado),
            detallesDto
        );

        return Ok(resultado);
    }

    [HttpPost]
    [RequierePermiso("PEDIDOS", AccionPermiso.Insertar)]
    public async Task<IActionResult> CrearPedido([FromBody] GuardarPedidoDto dto)
    {
        var numeroLimpio = dto.NumeroPedido?.Trim().ToUpper() ?? string.Empty;

        //Validaciones de Formato y Encabezado
        if (!Regex.IsMatch(numeroLimpio, @"^[A-Z0-9_\-]{3,30}$"))
            return BadRequest(new { message = "El número de pedido solo admite mayúsculas, números y guiones (entre 3 y 30 caracteres)." });

        if (await _context.Pedidos.AnyAsync(p => p.NumeroPedido == numeroLimpio))
            return BadRequest(new { message = $"Ya existe un pedido registrado con el número '{numeroLimpio}'." });

        var cliente = await _context.Clientes.FindAsync(dto.IdCliente);
        if (cliente == null || !cliente.EstadoCliente)
            return BadRequest(new { message = "El cliente seleccionado no existe o se encuentra inactivo." });

        if (dto.Detalles == null || !dto.Detalles.Any())
            return BadRequest(new { message = "El pedido debe contener al menos una prenda en su detalle." });

        //Validación de cada línea de detalle
        foreach (var (linea, index) in dto.Detalles.Select((item, idx) => (item, idx + 1)))
        {
            if (linea.Cantidad <= 0)
                return BadRequest(new { message = $"Línea #{index}: La cantidad solicitada debe ser mayor a cero." });

            if (linea.PrecioUnitarioAcordado < 0)
                return BadRequest(new { message = $"Línea #{index}: El precio acordado no puede ser negativo." });

            var prenda = await _context.Articulos.FindAsync(linea.IdArticuloPrenda);
            if (prenda == null || !prenda.EstadoArticulo || prenda.TipoArticulo != "PrendaTerminada")
                return BadRequest(new { message = $"Línea #{index}: El artículo seleccionado no existe, está inactivo o no es una Prenda Terminada." });

            var receta = await _context.Recetas.FindAsync(linea.IdReceta);
            if (receta == null || !receta.EstadoReceta)
                return BadRequest(new { message = $"Línea #{index}: La receta/ficha técnica seleccionada no existe o está inactiva." });

            if (receta.IdArticuloPrenda != linea.IdArticuloPrenda)
                return BadRequest(new { message = $"Línea #{index}: La receta '{receta.NombreReceta}' no corresponde a la prenda '{prenda.NombreArticulo}'." });
        }

        // 3. Persistencia en Transacción
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var nuevoPedido = new Pedido
            {
                NumeroPedido = numeroLimpio,
                IdCliente = dto.IdCliente,
                IdUsuario = ObtenerUsuarioIdSesion(),
                FechaEmision = DateTime.UtcNow,
                FechaEstimadaEntrega = dto.FechaEstimadaEntrega,
                EstadoPedido = "Registrado"
            };

            foreach (var item in dto.Detalles)
            {
                nuevoPedido.Detalles.Add(new PedidoDetalle
                {
                    IdArticuloPrenda = item.IdArticuloPrenda,
                    IdReceta = item.IdReceta,
                    Cantidad = item.Cantidad,
                    PrecioUnitarioAcordado = Math.Round(item.PrecioUnitarioAcordado, 2)
                });
            }

            _context.Pedidos.Add(nuevoPedido);
            await _context.SaveChangesAsync();

            // Auditoría
            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = ID_MODULO_PEDIDOS,
                Accion = "INSERT",
                TablaAfectada = "Pedidos",
                IdRegistro = nuevoPedido.IdPedido.ToString(),
                ValoresAnteriores = null,
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Pedido registrado exitosamente.", idPedido = nuevoPedido.IdPedido });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al registrar el pedido: " + ex.Message });
        }
    }

    [HttpPut("{id}")]
    [RequierePermiso("PEDIDOS", AccionPermiso.Modificar)]
    public async Task<IActionResult> EditarPedido(int id, [FromBody] GuardarPedidoDto dto)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Detalles)
                .ThenInclude(d => d.OrdenesProduccion)
            .FirstOrDefaultAsync(p => p.IdPedido == id);

        if (pedido == null)
            return NotFound(new { message = "Pedido no encontrado." });

        if (pedido.EstadoPedido != "Registrado")
            return BadRequest(new { message = $"No se puede modificar el pedido porque se encuentra en estado '{pedido.EstadoPedido}'." });

        if (pedido.Detalles.Any(d => d.OrdenesProduccion.Any()))
            return BadRequest(new { message = "No se puede modificar el pedido porque ya cuenta con órdenes de producción vinculadas." });

        var numeroLimpio = dto.NumeroPedido?.Trim().ToUpper() ?? string.Empty;
        if (!Regex.IsMatch(numeroLimpio, @"^[A-Z0-9_\-]{3,30}$"))
            return BadRequest(new { message = "El número de pedido solo admite mayúsculas, números y guiones (entre 3 y 30 caracteres)." });

        if (await _context.Pedidos.AnyAsync(p => p.NumeroPedido == numeroLimpio && p.IdPedido != id))
            return BadRequest(new { message = $"El número de pedido '{numeroLimpio}' ya pertenece a otro registro." });

        var cliente = await _context.Clientes.FindAsync(dto.IdCliente);
        if (cliente == null || !cliente.EstadoCliente)
            return BadRequest(new { message = "El cliente seleccionado no existe o está inactivo." });

        if (dto.Detalles == null || !dto.Detalles.Any())
            return BadRequest(new { message = "El pedido debe contener al menos una prenda." });

        // validaciones de ítems
        foreach (var (linea, index) in dto.Detalles.Select((item, idx) => (item, idx + 1)))
        {
            if (linea.Cantidad <= 0)
                return BadRequest(new { message = $"Línea #{index}: La cantidad debe ser mayor a cero." });

            if (linea.PrecioUnitarioAcordado < 0)
                return BadRequest(new { message = $"Línea #{index}: El precio acordado no puede ser negativo." });

            var prenda = await _context.Articulos.FindAsync(linea.IdArticuloPrenda);
            if (prenda == null || !prenda.EstadoArticulo || prenda.TipoArticulo != "PrendaTerminada")
                return BadRequest(new { message = $"Línea #{index}: La prenda no es válida." });

            var receta = await _context.Recetas.FindAsync(linea.IdReceta);
            if (receta == null || !receta.EstadoReceta || receta.IdArticuloPrenda != linea.IdArticuloPrenda)
                return BadRequest(new { message = $"Línea #{index}: La receta no corresponde a la prenda especificada." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var valoresAnteriores = new
            {
                pedido.NumeroPedido,
                pedido.IdCliente,
                pedido.FechaEstimadaEntrega,
                Detalles = pedido.Detalles.Select(d => new { d.IdArticuloPrenda, d.IdReceta, d.Cantidad, d.PrecioUnitarioAcordado }).ToList()
            };

            pedido.NumeroPedido = numeroLimpio;
            pedido.IdCliente = dto.IdCliente;
            pedido.FechaEstimadaEntrega = dto.FechaEstimadaEntrega;

            // reemplazar líneas del pedido (eliminación física de las anteriores)
            _context.PedidoDetalles.RemoveRange(pedido.Detalles);

            foreach (var item in dto.Detalles)
            {
                _context.PedidoDetalles.Add(new PedidoDetalle
                {
                    IdPedido = id,
                    IdArticuloPrenda = item.IdArticuloPrenda,
                    IdReceta = item.IdReceta,
                    Cantidad = item.Cantidad,
                    PrecioUnitarioAcordado = Math.Round(item.PrecioUnitarioAcordado, 2)
                });
            }

            await _context.SaveChangesAsync();

            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = ID_MODULO_PEDIDOS,
                Accion = "UPDATE",
                TablaAfectada = "Pedidos",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores),
                ValoresNuevos = JsonSerializer.Serialize(dto),
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Pedido actualizado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error al actualizar el pedido: " + ex.Message });
        }
    }

    [HttpPatch("{id}/estado")]
    [RequierePermiso("PEDIDOS", AccionPermiso.Modificar)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoPedidoDto dto)
    {
        var nuevoEstado = dto.EstadoPedido?.Trim();
        if (string.IsNullOrWhiteSpace(nuevoEstado) || !ESTADOS_VALIDOS.Contains(nuevoEstado))
            return BadRequest(new { message = $"Estado no válido. Valores permitidos: {string.Join(", ", ESTADOS_VALIDOS)}." });

        var pedido = await _context.Pedidos
            .Include(p => p.Detalles)
                .ThenInclude(d => d.OrdenesProduccion)
            .FirstOrDefaultAsync(p => p.IdPedido == id);

        if (pedido == null)
            return NotFound(new { message = "Pedido no encontrado." });

        if (!TRANSICIONES_VALIDAS.TryGetValue(pedido.EstadoPedido, out var permitidos) || !permitidos.Contains(nuevoEstado))
        {
            var opciones = permitidos != null && permitidos.Length > 0 ? string.Join(", ", permitidos) : "ninguna (estado final)";
            return BadRequest(new { message = $"No se puede pasar de '{pedido.EstadoPedido}' a '{nuevoEstado}'. Transiciones permitidas: {opciones}." });
        }
        
        // cambios de estado
        if (nuevoEstado == "Cancelado")
        {
            var tieneOrdenesEnCurso = pedido.Detalles
                .SelectMany(d => d.OrdenesProduccion)
                .Any(o => o.EstadoOrden != "Iniciada" && o.CantidadProducida > 0);

            if (tieneOrdenesEnCurso)
                return BadRequest(new { message = "No se puede cancelar el pedido porque ya cuenta con avance físico en el taller de producción." });
        }

        var estadoAnterior = pedido.EstadoPedido;
        pedido.EstadoPedido = nuevoEstado;

        var log = new Bitacora
        {
            IdUsuario = ObtenerUsuarioIdSesion(),
            IdModulo = ID_MODULO_PEDIDOS,
            Accion = "STATE_CHANGE",
            TablaAfectada = "Pedidos",
            IdRegistro = id.ToString(),
            ValoresAnteriores = JsonSerializer.Serialize(new { EstadoPedido = estadoAnterior }),
            ValoresNuevos = JsonSerializer.Serialize(new { EstadoPedido = nuevoEstado }),
            DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            FechaRegistro = DateTime.UtcNow
        };
        _context.Bitacora.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Estado del pedido actualizado a '{nuevoEstado}'." });
    }

    [HttpDelete("{id}")]
    [RequierePermiso("PEDIDOS", AccionPermiso.Eliminar)]
    public async Task<IActionResult> EliminarPedido(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Detalles)
                .ThenInclude(d => d.OrdenesProduccion)
            .FirstOrDefaultAsync(p => p.IdPedido == id);

        if (pedido == null)
            return NotFound(new { message = "Pedido no encontrado." });

        if (pedido.EstadoPedido != "Registrado")
            return BadRequest(new { message = "Solo se pueden eliminar pedidos en estado 'Registrado'." });

        if (pedido.Detalles.Any(d => d.OrdenesProduccion.Any()))
            return BadRequest(new { message = "No es posible eliminar el pedido porque tiene órdenes de producción asociadas." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var log = new Bitacora
            {
                IdUsuario = ObtenerUsuarioIdSesion(),
                IdModulo = ID_MODULO_PEDIDOS,
                Accion = "DELETE",
                TablaAfectada = "Pedidos",
                IdRegistro = id.ToString(),
                ValoresAnteriores = JsonSerializer.Serialize(new { pedido.NumeroPedido, pedido.IdCliente, pedido.EstadoPedido }),
                ValoresNuevos = null,
                DireccionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FechaRegistro = DateTime.UtcNow
            };
            _context.Bitacora.Add(log);

            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new { message = "Pedido eliminado exitosamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error interno al eliminar el pedido: " + ex.Message });
        }
    }

    [HttpGet("siguiente-numero")]
    [RequierePermiso("PEDIDOS", AccionPermiso.Consultar)]
    public async Task<ActionResult<string>> ObtenerSiguienteNumero()
    {
        int anioActual = DateTime.UtcNow.Year;
        string prefijo = $"PED-{anioActual}-";

        var ultimoNumero = await _context.Pedidos
            .Where(p => p.NumeroPedido.StartsWith(prefijo))
            .OrderByDescending(p => p.IdPedido)
            .Select(p => p.NumeroPedido)
            .FirstOrDefaultAsync();

        int correlativo = 1;
        if (!string.IsNullOrEmpty(ultimoNumero))
        {
            var partes = ultimoNumero.Split('-');
            if (partes.Length == 3 && int.TryParse(partes[2], out int num))
                correlativo = num + 1;
        }

        return Ok(new { siguienteNumero = $"{prefijo}{correlativo:D4}" });
    }
}