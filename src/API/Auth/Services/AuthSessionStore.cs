using System.Collections.Concurrent;

namespace API.Auth;

public class AuthSessionStore
{
    private readonly ConcurrentDictionary<string, PendingMfaChallenge> _mfaChallenges = new();
    private readonly ConcurrentDictionary<string, RefreshSession> _refreshSessions = new();

    public (string ChallengeToken, string OtpCode, DateTime ExpiresAtUtc) CreateMfaChallenge(int userId, int otpMinutes)
    {
        var now = DateTime.UtcNow;
        var challengeToken = Guid.NewGuid().ToString("N");
        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        _mfaChallenges[challengeToken] = new PendingMfaChallenge(
            UserId: userId,
            OtpCode: otpCode,
            ExpiresAtUtc: now.AddMinutes(otpMinutes));

        return (challengeToken, otpCode, now.AddMinutes(otpMinutes));
    }

    public bool TryVerifyMfa(string challengeToken, string otpCode, out int userId)
    {
        userId = 0;

        if (!_mfaChallenges.TryGetValue(challengeToken, out var challenge))
            return false;

        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            _mfaChallenges.TryRemove(challengeToken, out _);
            return false;
        }

        if (!string.Equals(challenge.OtpCode, otpCode, StringComparison.Ordinal))
            return false;

        _mfaChallenges.TryRemove(challengeToken, out _);
        userId = challenge.UserId;
        return true;
    }

    public (string RefreshToken, DateTime ExpiresAtUtc) CreateRefreshToken(int userId, int refreshDays)
    {
        var now = DateTime.UtcNow;
        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var expiresAt = now.AddDays(refreshDays);

        _refreshSessions[refreshToken] = new RefreshSession(userId, expiresAt);
        return (refreshToken, expiresAt);
    }

    public bool TryConsumeRefreshToken(string refreshToken, out int userId)
    {
        userId = 0;

        if (!_refreshSessions.TryRemove(refreshToken, out var session))
            return false;

        if (session.ExpiresAtUtc < DateTime.UtcNow)
            return false;

        userId = session.UserId;
        return true;
    }

    private sealed record PendingMfaChallenge(int UserId, string OtpCode, DateTime ExpiresAtUtc);

    private sealed record RefreshSession(int UserId, DateTime ExpiresAtUtc);
}
