using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Reflow.Modules.Triggers.Services;

public static class WebhookToken
{
    public static string Generate()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
