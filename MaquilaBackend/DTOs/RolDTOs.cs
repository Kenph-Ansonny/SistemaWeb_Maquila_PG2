namespace MaquilaBackend.DTOs;

public record RolListDto(
    int IdRol,
    string NombreRol,
    string? Descripcion,
    bool EstadoRol,
    int CantidadUsuarios
);

public record CrearRolDto(
    string NombreRol,
    string? Descripcion
);

public record EditarRolDto(
    string NombreRol,
    string? Descripcion
);

public record PermisoModuloItemDto(
    int IdModulo,
    string CodigoModulo,
    string NombreModulo,
    bool PuedeConsultar,
    bool PuedeInsertar,
    bool PuedeModificar,
    bool PuedeEliminar
);

public record ActualizarPermisosRolDto(
    int IdRol,
    List<PermisoModuloItemDto> Permisos
);