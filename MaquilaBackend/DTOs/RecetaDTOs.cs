namespace MaquilaBackend.DTOs;

public record RecetaDetalleDto(
    int IdArticuloInsumo,
    string CodigoInsumo,
    string NombreInsumo,
    string TipoArticulo,
    int IdUnidadConsumo,
    string CodigoMedida,
    string NombreMedida,
    decimal CantidadNeta,
    decimal PorcentajeMerma,
    decimal CantidadBruta,
    decimal CostoUnitarioInsumo,
    decimal Subtotal
);

public record RecetaListDto(
    int IdReceta,
    string NombreReceta,
    int IdArticuloPrenda,
    string CodigoPrenda,
    string NombrePrenda,
    string? Talla,
    string? Color,
    string? DescripcionReceta,
    bool EstadoReceta,
    int TotalInsumos,
    decimal CostoEstimadoTotal
);

public record GuardarRecetaDetalleDto(
    int IdArticuloInsumo,
    int IdUnidadConsumo,
    decimal CantidadNeta,
    decimal PorcentajeMerma
);

public record GuardarRecetaDto(
    int IdArticuloPrenda,
    string NombreReceta,
    string? DescripcionReceta,
    List<GuardarRecetaDetalleDto> Detalles
);