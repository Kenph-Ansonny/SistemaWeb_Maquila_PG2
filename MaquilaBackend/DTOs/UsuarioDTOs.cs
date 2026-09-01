namespace MaquilaBackend.DTOs;

public record UsuarioListDto(
    int IdUsuario,
    string NombreUsuario,
    string Correo,
    bool EstadoUsuario,
    DateTime FechaCreacion,
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
    string? Password, // Opcional al editar
    List<int>? RolesIds
);

public record LoginRequestDto(
    string Identificador, 
    string Password
);

public record LoginResponseDto(int IdUsuario,
    string NombreUsuario,
    string Correo,
    string RolPrincipal,
    string Token
);

