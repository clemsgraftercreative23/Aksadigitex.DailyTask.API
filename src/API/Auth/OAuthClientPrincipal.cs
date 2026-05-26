#nullable enable
using System.Security.Claims;

namespace API.Auth;

public static class OAuthClientPrincipal
{
    public const string TokenUseClaim = "token_use";
    public const string ClientCredentialsTokenUse = "client_credentials";
    public const string ClientIdClaim = "client_id";
    public const string ScopeClaim = "scope";

    public static bool IsClientCredentials(this ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(TokenUseClaim), ClientCredentialsTokenUse, StringComparison.Ordinal);

    public static string? GetClientId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClientIdClaim);

    public static bool HasAnyScope(this ClaimsPrincipal user, params string[] scopes)
    {
        if (scopes.Length == 0)
            return false;

        var grantedScopes = user.FindFirstValue(ScopeClaim)?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        return scopes.Any(grantedScopes.Contains);
    }

    public static async Task<bool> ValidateClientScopeAsync(
        this ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken ct,
        params string[] allowedScopes)
    {
        if (!user.IsClientCredentials())
            return true;

        if (user.HasAnyScope(allowedScopes))
            return true;

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                message = "Akses ditolak: token client_credentials tidak memiliki scope yang dibutuhkan.",
                requiredScopes = allowedScopes
            },
            cancellationToken: ct);
        return false;
    }
}
