namespace MaquilaBackend.DTOs;

public record UsuarioListDto(
    int IdUsuario,
    string NombreUsuario,
    string Correo,
    bool EstadoUsuario,
    DateTime FechaCreacion,
    DateTime? FechaUltimoAcceso,
    DateTime? FechaBloqueo,
    List<string> Roles,
    List<int> RolesIds
);

public record CrearUsuarioDto(
    string NombreUsuario,
    string Correo,
    string Password,
    List<int>? RolesIds
);

public record EditarUsuarioDto(
    string NombreUsuario,
    string Correo,
    string? Password,
    List<int>? RolesIds
);

public record PermisoModuloDto(
    string CodigoModulo,
    string NombreModulo,
    bool PuedeConsultar,
    bool PuedeInsertar,
    bool PuedeModificar,
    bool PuedeEliminar
);

public record LoginRequestDto(string Identificador, string Password);

public record LoginResponseDto(
    int IdUsuario,
    string NombreUsuario,
    string Correo,
    string RolPrincipal,
    List<PermisoModuloDto> Permisos,
    string Token
);