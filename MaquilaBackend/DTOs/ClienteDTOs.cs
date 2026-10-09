namespace MaquilaBackend.DTOs;

// DTO para listar en la tabla principal
public record ClienteListDto(
    int IdCliente,
    string NombreCliente,
    string? TelefonoCliente,
    string? DireccionCliente,
    bool EstadoCliente,
    int TotalPedidos
);

// DTO para el formulario de Creación y Edición
public record GuardarClienteDto(
    string NombreCliente,
    string? TelefonoCliente,
    string? DireccionCliente
);

// DTO optimizado y ligero para comboboxes/selects (por ejemplo, al crear pedidos)
public record ClienteOptionDto(
    int IdCliente,
    string NombreCliente
);