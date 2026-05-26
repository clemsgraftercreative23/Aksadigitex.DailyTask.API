#nullable enable
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace API.Auth;

public sealed class OAuthClientCredentialService
{
    private readonly OAuthOptions _options;

    public OAuthClientCredentialService(IOptions<OAuthOptions> options)
    {
        _options = options.Value;
    }

    public bool TryValidate(
        string clientId,
        string clientSecret,
        string? requestedScope,
        out OAuthClientOptions? client,
        out string[] grantedScopes,
        out string error)
    {
        client = null;
        grantedScopes = Array.Empty<string>();
        error = string.Empty;

        clientId = clientId.Trim();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            error = "invalid_client";
            return false;
        }

        client = _options.Clients.FirstOrDefault(c =>
            c.Enabled && string.Equals(c.ClientId, clientId, StringComparison.Ordinal));

        if (client is null || string.IsNullOrWhiteSpace(client.SecretSha256) || !SecretMatches(clientSecret, client.SecretSha256))
        {
            client = null;
            error = "invalid_client";
            return false;
        }

        var allowedScopes = client.Scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var requestedScopes = SplitScopes(requestedScope).ToArray();
        grantedScopes = requestedScopes.Length == 0
            ? allowedScopes.OrderBy(s => s, StringComparer.Ordinal).ToArray()
            : requestedScopes;

        if (grantedScopes.Any(scope => !allowedScopes.Contains(scope)))
        {
            error = "invalid_scope";
            return false;
        }

        return true;
    }

    public static string ComputeSha256Hex(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IEnumerable<string> SplitScopes(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            yield break;

        foreach (var item in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return item;
    }

    private static bool SecretMatches(string providedSecret, string configuredHash)
    {
        var normalizedHash = configuredHash.Trim();
        if (normalizedHash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            normalizedHash = normalizedHash["sha256:".Length..];

        var providedHash = ComputeSha256Hex(providedSecret);
        var configuredBytes = Encoding.ASCII.GetBytes(normalizedHash.ToLowerInvariant());
        var providedBytes = Encoding.ASCII.GetBytes(providedHash);

        return configuredBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
