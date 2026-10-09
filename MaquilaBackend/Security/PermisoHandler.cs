using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MaquilaBackend.Data;
using System.Security.Claims;

namespace MaquilaBackend.Security;

public class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    private readonly MaquilaDbContext _context;

    public PermisoHandler(MaquilaDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out int idUsuario))
            return; // Sin identidad válida en el token: se deniega (no se llama a Succeed).

        var rolesIds = await _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == idUsuario)
            .Select(ur => ur.IdRol)
            .ToListAsync();

        if (rolesIds.Count == 0) return;

        // Red de seguridad basada en un DATO (flag por IdRol), nunca en el nombre del rol.
        bool esSuperAdmin = await _context.Roles
            .Where(r => rolesIds.Contains(r.IdRol) && r.EstadoRol)
            .AnyAsync(r => r.EsSuperAdmin);

        if (esSuperAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var permisos = await _context.PermisosRol
            .Include(pr => pr.Modulo)
            .Include(pr => pr.Rol)
            .Where(pr => rolesIds.Contains(pr.IdRol)
                      && pr.Rol.EstadoRol
                      && pr.Modulo.CodigoModulo == requirement.CodigoModulo
                      && pr.Modulo.EstadoModulo)
            .ToListAsync();

        bool tienePermiso = requirement.Accion switch
        {
            AccionPermiso.Consultar => permisos.Any(p => p.PuedeConsultar),
            AccionPermiso.Insertar => permisos.Any(p => p.PuedeInsertar),
            AccionPermiso.Modificar => permisos.Any(p => p.PuedeModificar),
            AccionPermiso.Eliminar => permisos.Any(p => p.PuedeEliminar),
            _ => false
        };

        if (tienePermiso)
            context.Succeed(requirement);
    }
}