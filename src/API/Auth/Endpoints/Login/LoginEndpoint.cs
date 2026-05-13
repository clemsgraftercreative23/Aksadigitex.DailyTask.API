using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Auth;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly AuthUserLookupService _userLookup;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthSessionStore _sessionStore;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<LoginEndpoint> _logger;

    public LoginEndpoint(
        AuthUserLookupService userLookup,
        JwtTokenService jwtTokenService,
        AuthSessionStore sessionStore,
        IOptions<JwtOptions> jwtOptions,
        ILogger<LoginEndpoint> logger)
    {
        _userLookup = userLookup;
        _jwtTokenService = jwtTokenService;
        _sessionStore = sessionStore;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("v1/auth/login");
        AllowAnonymous();
        Description(d => d.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Login with email and password";
            s.Description = "Authenticate user. If MFA active, return challenge token for OTP verification.";
        });
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var loginResult = await _userLookup.FindForLoginAsync(req.Email, req.Password, ct);

        if (loginResult.User is null && !loginResult.AccountExists)
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsJsonAsync(new LoginErrorResponse
            {
                Message = "Email tidak terdaftar atau akun tidak aktif.",
                ErrorCode = "USER_NOT_FOUND",
                Details = "Periksa kembali alamat email atau ID karyawan. Jika belum terdaftar, hubungi administrator."
            }, ct);
            return;
        }

        if (loginResult.User is null)
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsJsonAsync(new LoginErrorResponse
            {
                Message = "Password salah.",
                ErrorCode = "INVALID_PASSWORD",
                Details = "Kata sandi yang Anda masukkan tidak sesuai. Silakan coba lagi atau gunakan fitur lupa sandi."
            }, ct);
            return;
        }

        var user = loginResult.User;

        if (user.MfaEnabled == true)
        {
            var challenge = _sessionStore.CreateMfaChallenge(user.Id, user.AccountType, _jwtOptions.OtpMinutes);

            _logger.LogInformation(
                "OTP code for user {UserId}: {OtpCode}. ChallengeToken: {ChallengeToken}",
                user.Id,
                challenge.OtpCode,
                challenge.ChallengeToken);

            await SendAsync(new LoginResponse
            {
                MfaRequired = true,
                ChallengeToken = challenge.ChallengeToken,
                ChallengeExpiresAtUtc = challenge.ExpiresAtUtc
            }, cancellation: ct);

            return;
        }

        var access = _jwtTokenService.CreateAccessToken(user.Id, user.Email, user.Role);
        var refresh = _sessionStore.CreateRefreshToken(user.Id, user.AccountType, _jwtOptions.RefreshTokenDays);

        await SendAsync(new LoginResponse
        {
            MfaRequired = false,
            AccessToken = access.AccessToken,
            AccessTokenExpiresAtUtc = access.ExpiresAtUtc,
            RefreshToken = refresh.RefreshToken,
            RefreshTokenExpiresAtUtc = refresh.ExpiresAtUtc
        }, cancellation: ct);
    }
}
