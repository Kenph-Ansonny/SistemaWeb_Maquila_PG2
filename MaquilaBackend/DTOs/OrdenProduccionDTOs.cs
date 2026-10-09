namespace MaquilaBackend.DTOs;

public record OrdenProduccionListDto(
    int IdOrden,
    long IdPedidoDetalle,
    int IdPedido,
    string NumeroPedido,
    string NombreCliente,
    int IdArticuloPrenda,
    string NombreArticulo,
    int IdReceta,
    string NombreReceta,
    DateTime FechaInicio,
    DateTime? FechaFin,
    int CantidadProgramada,
    int CantidadProducida,
    string EstadoOrden
);

//Detalle individual
public record OrdenProduccionDetalleDto(
    int IdOrden,
    long IdPedidoDetalle,
    int IdPedido,
    string NumeroPedido,
    string NombreCliente,
    string? TelefonoCliente,
    int IdArticuloPrenda,
    string NombreArticulo,
    int IdReceta,
    string NombreReceta,
    int CantidadPedida,
    decimal PrecioUnitarioAcordado,
    DateTime FechaInicio,
    DateTime? FechaFin,
    int CantidadProgramada,
    int CantidadProducida,
    string EstadoOrden
);

//detalles de pedido con saldo pendiente de programar
public record PedidoDetalleDisponibleDto(
    long IdPedidoDetalle,
    int IdPedido,
    string NumeroPedido,
    string NombreCliente,
    int IdArticuloPrenda,
    string NombreArticulo,
    int IdReceta,
    string NombreReceta,
    int CantidadPedida,
    int CantidadYaProgramada,
    int CantidadPendiente,
    DateTime? FechaEstimadaEntrega,
    List<LoteOrdenResumenDto> OrdenesExistentes
);

//Resumen compacto de un lote/orden ya creado para un detalle
public record LoteOrdenResumenDto(
    int IdOrden,
    int CantidadProgramada,
    int CantidadProducida,
    string EstadoOrden
);

public record CrearOrdenProduccionDto(
    long IdPedidoDetalle,
    DateTime FechaInicio,
    int CantidadProgramada
);

//  Registrar avance, cambio de etapa y cantidad completada en la etapa
public record RegistrarAvanceDto(
    string EstadoOrden,
    int CantidadProducida
);

// Contexto de pedido para la vista de avance
public record ContextoPedidoDto(
    int IdPedido,
    string NumeroPedido,
    string NombreCliente,
    string EstadoPedido,
    List<ContextoPrendaDto> Prendas
);

public record ContextoPrendaDto(
    long IdPedidoDetalle,
    int IdArticuloPrenda,
    string NombreArticulo,
    string NombreReceta,
    int CantidadPedida,
    int CantidadProgramadaTotal,
    int CantidadProducidaTotal,
    List<LoteOrdenResumenDto> Lotes
);