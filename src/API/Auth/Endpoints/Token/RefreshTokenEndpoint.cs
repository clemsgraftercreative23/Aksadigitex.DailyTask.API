using FastEndpoints;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Auth;

public class RefreshTokenEndpoint : Endpoint<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthSessionStore _sessionStore;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenEndpoint(
        AppDbContext db,
        JwtTokenService jwtTokenService,
        AuthSessionStore sessionStore,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _sessionStore = sessionStore;
        _jwtOptions = jwtOptions.Value;
    }

    public override void Configure()
    {
        Post("v1/auth/refresh");
        AllowAnonymous();
        Description(d => d.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Refresh access token";
            s.Description = "Rotate refresh token and issue a new access token pair.";
        });
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        if (!_sessionStore.TryConsumeRefreshToken(req.RefreshToken, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);

        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var access = _jwtTokenService.CreateAccessToken(user.Id, user.Email, user.Role);
        var refresh = _sessionStore.CreateRefreshToken(user.Id, _jwtOptions.RefreshTokenDays);

        await SendAsync(new RefreshTokenResponse
        {
            AccessToken = access.AccessToken,
            AccessTokenExpiresAtUtc = access.ExpiresAtUtc,
            RefreshToken = refresh.RefreshToken,
            RefreshTokenExpiresAtUtc = refresh.ExpiresAtUtc
        }, cancellation: ct);
    }
}
