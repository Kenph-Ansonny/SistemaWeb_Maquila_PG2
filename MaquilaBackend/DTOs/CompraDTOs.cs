namespace MaquilaBackend.DTOs;

public record CompraListDto(
    int IdCompra,
    string NumeroDocumento,
    int IdProveedor,
    string NombreProveedor,
    int IdUsuario,
    string NombreUsuario,
    DateTime FechaCompra,
    string EstadoCompra,
    decimal CostoTotal,
    int TotalRenglones,
    decimal TotalUnidades
);

public record CompraDetalleDto(
    long IdDetalle,
    int IdArticulo,
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    int IdUnidadMedida,
    string CodigoMedida,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal Subtotal,
    int IdAlmacenDestino,
    string NombreAlmacenDestino
);

public record CompraCompletaDto(
    int IdCompra,
    string NumeroDocumento,
    int IdProveedor,
    string NombreProveedor,
    string? TelefonoProveedor,
    string? DireccionProveedor,
    int IdUsuario,
    string NombreUsuario,
    DateTime FechaCompra,
    string EstadoCompra,
    decimal CostoTotal,
    List<CompraDetalleDto> Detalles
);

public record GuardarCompraDetalleDto(
    int IdArticulo,
    int IdUnidadMedida,
    decimal Cantidad,
    decimal CostoUnitario,
    int IdAlmacenDestino
);

public record GuardarCompraDto(
    string NumeroDocumento,
    int IdProveedor,
    DateTime FechaCompra,
    List<GuardarCompraDetalleDto> Detalles
);