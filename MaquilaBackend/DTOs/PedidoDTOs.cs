namespace MaquilaBackend.DTOs;

//  para la tabla principal (listado con métricas calculadas)
public record PedidoListDto(
    int IdPedido,
    string NumeroPedido,
    int IdCliente,
    string NombreCliente,
    int IdUsuario,
    string NombreUsuario,
    DateTime FechaEmision,
    DateTime? FechaEstimadaEntrega,
    string EstadoPedido,
    int TotalPrendas,
    decimal TotalMonto,
    int CantidadLineas,
    int TotalOrdenesProduccion
);

// de una línea del pedido (para consulta detallada)
public record PedidoDetalleDto(
    long IdDetalle,
    int IdArticuloPrenda,
    string CodigoArticulo,
    string NombreArticulo,
    string? Talla,
    string? Color,
    int IdReceta,
    string NombreReceta,
    int Cantidad,
    decimal PrecioUnitarioAcordado,
    decimal Subtotal,
    int CantidadProgramada,
    int CantidadPendienteProgramar
);

// para consultar un pedido completo por ID con sus líneas
public record PedidoCompletoDto(
    int IdPedido,
    string NumeroPedido,
    int IdCliente,
    string NombreCliente,
    string? TelefonoCliente,
    string? DireccionCliente,
    int IdUsuario,
    string NombreUsuario,
    DateTime FechaEmision,
    DateTime? FechaEstimadaEntrega,
    string EstadoPedido,
    int TotalPrendas,
    decimal TotalMonto,
    List<PedidoDetalleDto> Detalles
);

// para cada línea al guardar o editar un pedido
public record GuardarPedidoDetalleDto(
    int IdArticuloPrenda,
    int IdReceta,
    int Cantidad,
    decimal PrecioUnitarioAcordado
);

// formulario de Creación y Edición del Pedido
public record GuardarPedidoDto(
    string NumeroPedido,
    int IdCliente,
    DateTime? FechaEstimadaEntrega,
    List<GuardarPedidoDetalleDto> Detalles
);

// para cambio de etapa del pedido
public record CambiarEstadoPedidoDto(
    string EstadoPedido
);