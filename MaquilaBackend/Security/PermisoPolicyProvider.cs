using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MaquilaBackend.Security;

public class PermisoPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefijo = "PERMISO:";
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermisoPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
        {
            var partes = policyName[Prefijo.Length..].Split(':');
            var codigoModulo = partes[0];
            var accion = Enum.Parse<AccionPermiso>(partes[1]);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermisoRequirement(codigoModulo, accion))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}