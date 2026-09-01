namespace MaquilaBackend.DTOs;

public record BitacoraItemDto(
    long IdBitacora,
    int IdUsuario,
    string NombreUsuario,
    string Modulo,
    string Accion,
    string TablaAfectada,
    string IdRegistro,
    string? ValoresAnteriores,
    string? ValoresNuevos,
    string? DireccionIp,
    DateTime FechaRegistro
);

public record BitacoraFiltroDto(
    string? Busqueda,
    string? Modulo,
    string? Accion,
    DateTime? FechaInicio,
    DateTime? FechaFin
);