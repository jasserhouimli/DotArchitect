using System.Net;
using System.Net.Sockets;

namespace Reflow.Modules.WorkflowExecution.Services;

public static class SsrfGuard
{
    public static async Task AssertSafeAsync(string url, bool allowPrivateNetwork, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("URL must be an absolute http(s) URL");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("URL must not contain credentials");

        if (uri.Port != 80 && uri.Port != 443 && (uri.Port < 1 || uri.Port > 65535))
            throw new InvalidOperationException("URL port is not allowed");

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            }
            catch (SocketException)
            {
                throw new InvalidOperationException($"Could not resolve host: {uri.Host}");
            }
        }

        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve host: {uri.Host}");

        if (!allowPrivateNetwork)
        {
            foreach (var ip in addresses)
            {
                if (IsBlocked(ip))
                    throw new InvalidOperationException($"URL resolves to a prohibited address ({uri.Host})");
            }
        }
    }

    private static bool IsBlocked(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 0) return true;
            if (bytes[0] >= 224) return true;
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return true;
            if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return true;
            if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal) return true;
            if (bytes[0] == 0xFC || bytes[0] == 0xFD) return true;
            if (bytes.All(b => b == 0)) return true;
        }

        return false;
    }
}
