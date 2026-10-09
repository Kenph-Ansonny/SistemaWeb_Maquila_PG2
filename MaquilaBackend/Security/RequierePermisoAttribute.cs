using Microsoft.AspNetCore.Authorization;

namespace MaquilaBackend.Security;

/// <summary>
/// Uso: [RequierePermiso("ALMACENES", AccionPermiso.Insertar)]
/// El código de módulo debe coincidir exactamente con Modulo.CodigoModulo en la base de datos.
/// </summary>
public class RequierePermisoAttribute : AuthorizeAttribute
{
    public RequierePermisoAttribute(string codigoModulo, AccionPermiso accion)
    {
        Policy = $"PERMISO:{codigoModulo}:{accion}";
    }
}