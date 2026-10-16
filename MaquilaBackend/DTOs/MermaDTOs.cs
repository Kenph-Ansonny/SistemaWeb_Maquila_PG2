namespace MaquilaBackend.DTOs;

// DTO para la tabla principal del listado
public record MermaListDto(
    long IdMerma,
    int IdOrdenProduccion,
    string NumeroPedido,
    string NombrePrenda,
    int IdArticuloInsumo,
    string CodigoInsumo,
    string NombreInsumo,
    string UnidadMedida,
    decimal CantidadMermaReal,
    string TipoMerma,
    int? IdAlmacenDestino,
    string? NombreAlmacenDestino,
    string? Observaciones,
    DateTime FechaRegistro
);

// DTO para enviar el formulario de registro
public record GuardarMermaDto(
    int IdOrdenProduccion,
    int IdArticuloInsumo,
    decimal CantidadMermaReal,
    string TipoMerma,
    int? IdAlmacenDestino,
    string? Observaciones
);

// DTO auxiliar para cargar los insumos de la orden y su merma teórica estimada
public record InsumoOrdenTeoricoDto(
    int IdArticuloInsumo,
    string CodigoInsumo,
    string NombreInsumo,
    string UnidadMedida,
    decimal CantidadBrutaTeorica,
    decimal PorcentajeMermaEstimado,
    decimal CantidadMermaEstimada,
    decimal CantidadMermaYaRegistrada
);