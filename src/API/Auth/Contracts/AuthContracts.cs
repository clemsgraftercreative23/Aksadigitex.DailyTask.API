namespace API.Auth;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool MfaRequired { get; set; }
    public string ChallengeToken { get; set; } = string.Empty;
    public DateTime? ChallengeExpiresAtUtc { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
}

public class VerifyOtpRequest
{
    public string ChallengeToken { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}

public class VerifyOtpResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
}

/// <summary>
/// Response error untuk login gagal — memberikan feedback detail ke user.
/// </summary>
public class LoginErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? Details { get; set; }
}
