using Microsoft.AspNetCore.Authorization;

namespace MaquilaBackend.Security;

public enum AccionPermiso
{
    Consultar,
    Insertar,
    Modificar,
    Eliminar
}

public class PermisoRequirement : IAuthorizationRequirement
{
    public string CodigoModulo { get; }
    public AccionPermiso Accion { get; }

    public PermisoRequirement(string codigoModulo, AccionPermiso accion)
    {
        CodigoModulo = codigoModulo;
        Accion = accion;
    }
}