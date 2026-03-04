using FastEndpoints;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Auth;

public class VerifyOtpEndpoint : Endpoint<VerifyOtpRequest, VerifyOtpResponse>
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthSessionStore _sessionStore;
    private readonly JwtOptions _jwtOptions;

    public VerifyOtpEndpoint(
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
        Post("v1/auth/mfa/verify");
        AllowAnonymous();
        Description(d => d.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Verify OTP code";
            s.Description = "Verify MFA challenge with OTP and issue access/refresh tokens.";
        });
    }

    public override async Task HandleAsync(VerifyOtpRequest req, CancellationToken ct)
    {
        if (!_sessionStore.TryVerifyMfa(req.ChallengeToken, req.OtpCode, out var userId))
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

        var access = _jwtTokenService.CreateAccessToken(user.Id, user.Email);
        var refresh = _sessionStore.CreateRefreshToken(user.Id, _jwtOptions.RefreshTokenDays);

        await SendAsync(new VerifyOtpResponse
        {
            AccessToken = access.AccessToken,
            AccessTokenExpiresAtUtc = access.ExpiresAtUtc,
            RefreshToken = refresh.RefreshToken,
            RefreshTokenExpiresAtUtc = refresh.ExpiresAtUtc
        }, cancellation: ct);
    }
}
