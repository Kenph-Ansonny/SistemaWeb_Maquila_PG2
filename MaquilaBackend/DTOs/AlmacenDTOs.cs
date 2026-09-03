namespace MaquilaBackend.DTOs;

public record AlmacenListDto(
    int IdAlmacen,
    string NombreAlmacen,
    bool EstadoAlmacen,
    int TotalArticulosDistintos,
    decimal TotalUnidadesStock
);

public record GuardarAlmacenDto(
    string NombreAlmacen
);

public record AlmacenStockDetalleDto(
    int IdArticulo,
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    string UnidadMedida,
    decimal StockActual
);