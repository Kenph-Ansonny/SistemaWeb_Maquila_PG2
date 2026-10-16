using Microsoft.AspNetCore.Http;
using System.Net;

namespace MaquilaBackend.Helpers;

public static class NetworkHelper
{
    /// <summary>
    /// Obtiene la dirección IP real del cliente (Nginx y Docker)
    /// </summary>
    public static string ObtenerIpCliente(this HttpContext context)
    {
        if (context == null) return "127.0.0.1";

        // Cabecera X-Forwarded-For. Estándar en Railway, Nginx, AWS, Cloudflare
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Puede venir una lista de IPs
            var ips = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ips.Length > 0 && IPAddress.TryParse(ips[0], out var ipParsed))
            {
                return NormalizarIp(ipParsed);
            }
        }

        // cabecera X-Real-IP, habitualmente por Nginx
        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp) && !string.IsNullOrWhiteSpace(realIp))
        {
            if (IPAddress.TryParse(realIp.ToString().Trim(), out var ipParsed))
            {
                return NormalizarIp(ipParsed);
            }
        }

        // conexión socket directa (Fallback)
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            return NormalizarIp(remoteIp);
        }

        return "127.0.0.1";
    }

    private static string NormalizarIp(IPAddress ip)
    {
        // Convierte loopback IPv6 (::1) a IPv4 estándar
        if (IPAddress.IsLoopback(ip) || ip.ToString() == "::1")
        {
            return "127.0.0.1";
        }

        // Si viene mapeada de IPv4 a IPv6 extrae el formato IPv4 limpio
        if (ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4().ToString();
        }

        return ip.ToString();
    }
}