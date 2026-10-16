namespace MaquilaBackend.DTOs;

public record ProveedorListDto(
    int IdProveedor,
    string NombreProveedor,
    string? ContactoPrincipal,
    string? Telefono,
    string? Direccion,
    bool EstadoProveedor
);

public record GuardarProveedorDto(
    string NombreProveedor,
    string? ContactoPrincipal,
    string? Telefono,
    string? Direccion
);

public record ProveedorOptionDto(
    int IdProveedor,
    string NombreProveedor
);