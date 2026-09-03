namespace MaquilaBackend.DTOs;

public record ExistenciaBodegaDto(
    int IdAlmacen,
    string NombreAlmacen,
    decimal StockActual
);

public record ArticuloListDto(
    int IdArticulo,
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    int IdUnidadBaseMedida,
    string CodigoMedida,
    string NombreMedida,
    decimal StockMinimo,
    decimal StockTotal,
    decimal CostoPromedio,
    bool EstadoArticulo,
    string? Talla,
    string? Color,
    List<ExistenciaBodegaDto> DesgloseBodegas
);

public record GuardarArticuloDto(
    string CodigoArticulo,
    string NombreArticulo,
    string TipoArticulo,
    int IdUnidadBaseMedida,
    decimal StockMinimo,
    decimal CostoPromedio,
    string? Talla,
    string? Color
);

public record UnidadMedidaOptionDto(
    int IdUnidadMedida,
    string CodigoMedida,
    string NombreMedida,
    string TipoMedida
);