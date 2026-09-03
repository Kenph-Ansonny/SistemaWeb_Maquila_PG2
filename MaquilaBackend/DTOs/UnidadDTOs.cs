namespace MaquilaBackend.DTOs;

public record UnidadMedidaDto(
    int IdUnidadMedida,
    string CodigoMedida,
    string NombreMedida,
    string TipoMedida,
    int TotalArticulosAsociados
);

public record GuardarUnidadMedidaDto(
    string CodigoMedida,
    string NombreMedida,
    string TipoMedida
);

public record UnidadConversionListDto(
    int IdUnidadOrigen,
    string CodigoOrigen,
    string NombreOrigen,
    int IdUnidadDestino,
    string CodigoDestino,
    string NombreDestino,
    string TipoMedida,
    decimal FactorConversion
);

public record GuardarConversionDto(
    int IdUnidadOrigen,
    int IdUnidadDestino,
    decimal FactorConversion
);