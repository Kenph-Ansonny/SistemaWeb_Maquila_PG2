namespace MaquilaBackend.DTOs;

// ---- Listado principal ----
public record OrdenProduccionListDto(
    int IdOrden,
    int? IdLote,
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

// ---- Respuesta paginada del listado ----
// ConteosPorEstado: total de órdenes por cada estado, aplicando los mismos filtros de
// búsqueda/fecha/pedido pero IGNORANDO el filtro de estado — así los KPIs/pills de la vista
// reflejan el universo filtrado completo, no solo la página actual.
public record OrdenesPaginadasDto(
    List<OrdenProduccionListDto> Items,
    int Total,
    int Pagina,
    int TamanioPagina,
    int TotalPaginas,
    Dictionary<string, int> ConteosPorEstado
);

// ---- Detalle individual ----
public record OrdenProduccionDetalleDto(
    int IdOrden,
    int? IdLote,
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

// ---- Lookup: detalles de pedido con saldo pendiente de programar ----
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

// ---- Resumen compacto de un lote/orden ya creado para un detalle ----
public record LoteOrdenResumenDto(
    int IdOrden,
    int? IdLote,
    int CantidadProgramada,
    int CantidadProducida,
    string EstadoOrden
);

// ---- Crear una orden individual (una sola prenda) ----
public record CrearOrdenProduccionDto(
    long IdPedidoDetalle,
    DateTime FechaInicio,
    int CantidadProgramada
);

// ---- Crear un lote: varias prendas del mismo pedido programadas juntas ----
public record ItemLoteProduccionDto(
    long IdPedidoDetalle,
    int CantidadProgramada
);

public record CrearLoteProduccionDto(
    DateTime FechaInicio,
    List<ItemLoteProduccionDto> Items
);

// ---- Registrar avance (cambio de etapa + cantidad completada en la etapa) ----
public record RegistrarAvanceDto(
    string EstadoOrden,
    int CantidadProducida
);

// ---- Contexto de pedido para la vista de avance: todas las prendas del mismo
// pedido, para saber "cuál es cuál" cuando un pedido mezcla varios artículos ----
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