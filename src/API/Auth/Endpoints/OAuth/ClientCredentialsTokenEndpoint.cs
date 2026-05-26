#nullable enable
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Auth;

public sealed class ClientCredentialsTokenRequest
{
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

public sealed class ClientCredentialsTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("expires_at_utc")]
    public DateTime ExpiresAtUtc { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

public sealed class OAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; } = string.Empty;
}

public class ClientCredentialsTokenEndpoint : EndpointWithoutRequest<ClientCredentialsTokenResponse>
{
    private readonly OAuthClientCredentialService _clientCredentialService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly OAuthOptions _oauthOptions;

    public ClientCredentialsTokenEndpoint(
        OAuthClientCredentialService clientCredentialService,
        JwtTokenService jwtTokenService,
        IOptions<OAuthOptions> oauthOptions)
    {
        _clientCredentialService = clientCredentialService;
        _jwtTokenService = jwtTokenService;
        _oauthOptions = oauthOptions.Value;
    }

    public override void Configure()
    {
        Post("v1/oauth/token");
        AllowAnonymous();
        Description(d => d.WithTags("OAuth"));
        Summary(s =>
        {
            s.Summary = "OAuth client credentials token";
            s.Description = "Issues short-lived JWT bearer tokens for trusted API-to-API access. Use grant_type=client_credentials with HTTP Basic client authentication or client_id/client_secret form fields.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var request = await ReadTokenRequestAsync(ct);
        var (basicClientId, basicClientSecret) = ReadBasicClientCredentials();

        var clientId = basicClientId ?? request.ClientId;
        var clientSecret = basicClientSecret ?? request.ClientSecret;
        var grantType = NormalizeGrantType(request.GrantType);

        if (!string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
        {
            await SendOAuthErrorAsync(400, "unsupported_grant_type", "Only grant_type=client_credentials is supported.", ct);
            return;
        }

        if (!_clientCredentialService.TryValidate(
                clientId,
                clientSecret,
                request.Scope,
                out var client,
                out var grantedScopes,
                out var error))
        {
            var statusCode = error == "invalid_client" ? 401 : 400;
            var description = error == "invalid_scope"
                ? "Requested scope is not allowed for this client."
                : "Client authentication failed.";

            await SendOAuthErrorAsync(statusCode, error, description, ct);
            return;
        }

        var access = _jwtTokenService.CreateClientCredentialsAccessToken(
            client!.ClientId,
            client.DisplayName,
            client.Role,
            grantedScopes,
            _oauthOptions.AccessTokenMinutes);

        await SendAsync(new ClientCredentialsTokenResponse
        {
            AccessToken = access.AccessToken,
            ExpiresAtUtc = access.ExpiresAtUtc,
            ExpiresIn = Math.Max(0, (int)(access.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
            Scope = string.Join(' ', grantedScopes)
        }, cancellation: ct);
    }

    private async Task<ClientCredentialsTokenRequest> ReadTokenRequestAsync(CancellationToken ct)
    {
        if (HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            return new ClientCredentialsTokenRequest
            {
                GrantType = form["grant_type"].ToString(),
                ClientId = form["client_id"].ToString(),
                ClientSecret = form["client_secret"].ToString(),
                Scope = form["scope"].ToString()
            };
        }

        var jsonRequest = await HttpContext.Request.ReadFromJsonAsync<ClientCredentialsTokenRequest>(cancellationToken: ct);
        return jsonRequest ?? new ClientCredentialsTokenRequest();
    }

    private (string? ClientId, string? ClientSecret) ReadBasicClientCredentials()
    {
        if (!AuthenticationHeaderValue.TryParse(HttpContext.Request.Headers.Authorization, out var header) ||
            !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return (null, null);
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex <= 0)
                return (null, null);

            return (decoded[..separatorIndex], decoded[(separatorIndex + 1)..]);
        }
        catch (FormatException)
        {
            return (null, null);
        }
    }

    private static string NormalizeGrantType(string grantType) =>
        grantType.Trim().Replace("-", "_", StringComparison.Ordinal);

    private async Task SendOAuthErrorAsync(int statusCode, string error, string description, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = statusCode;
        await HttpContext.Response.WriteAsJsonAsync(new OAuthErrorResponse
        {
            Error = error,
            ErrorDescription = description
        }, cancellationToken: ct);
    }
}
