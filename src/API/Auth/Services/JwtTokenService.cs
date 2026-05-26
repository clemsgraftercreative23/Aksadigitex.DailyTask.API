using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Domain;

namespace API.Auth;

public class JwtTokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(
        int userId,
        string email,
        UserRole role = UserRole.User,
        AuthAccountType accountType = AuthAccountType.User)
    {
        var now = DateTime.UtcNow;
        // AccessTokenMinutes=0 => nonaktifkan fitur session (token tidak pernah expired)
        var expiresAt = _jwtOptions.AccessTokenMinutes > 0
            ? now.AddMinutes(_jwtOptions.AccessTokenMinutes)
            : now.AddYears(10);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("account_type", accountType.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (accessToken, expiresAt);
    }

    public (string AccessToken, DateTime ExpiresAtUtc) CreateClientCredentialsAccessToken(
        string clientId,
        string displayName,
        UserRole role,
        IReadOnlyCollection<string> scopes,
        int accessTokenMinutes)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(accessTokenMinutes > 0 ? accessTokenMinutes : 15);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(OAuthClientPrincipal.ClientIdClaim, clientId),
            new(JwtRegisteredClaimNames.Name, string.IsNullOrWhiteSpace(displayName) ? clientId : displayName),
            new(ClaimTypes.Role, role.ToString()),
            new("account_type", AuthAccountType.Client.ToString()),
            new(OAuthClientPrincipal.TokenUseClaim, OAuthClientPrincipal.ClientCredentialsTokenUse),
            new(OAuthClientPrincipal.ScopeClaim, string.Join(' ', scopes.OrderBy(s => s, StringComparer.Ordinal))),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (accessToken, expiresAt);
    }
}
