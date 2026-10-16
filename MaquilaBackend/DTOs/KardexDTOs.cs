namespace MaquilaBackend.DTOs;

// DTO para la tabla principal y reportes
public record KardexMovimientoListDto(
    long IdKardex,
    int IdArticulo,
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    string UnidadMedida,
    int IdAlmacen,
    string NombreAlmacen,
    string TipoMovimiento,
    string TipoDocumentoReferencia,
    long IdDocumentoReferencia,
    decimal Cantidad,
    decimal StockAnterior,
    decimal StockPosterior,
    decimal CostoUnitario,
    decimal MontoTotalMovimiento,
    int IdUsuario,
    string NombreUsuario,
    DateTime FechaMovimiento
);

// DTO para registrar ajuste manual de inventario
public record RegistrarAjusteInventarioDto(
    int IdArticulo,
    int IdAlmacen,
    decimal CantidadAjuste, // Positiva si ingresa sobrante, negativa si falta material
    string MotivoAjuste,
    decimal? CostoUnitario = null
);

// DTO para consultar existencias actuales consolidadas por almacén
public record ArticuloExistenciaKardexDto(
    int IdArticulo,
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    string UnidadMedida,
    decimal CostoPromedio,
    decimal StockTotalEmpresa,
    decimal StockMinimo,
    List<AlmacenStockDto> Almacenes
);

public record AlmacenStockDto(
    int IdAlmacen,
    string NombreAlmacen,
    decimal StockActual
);