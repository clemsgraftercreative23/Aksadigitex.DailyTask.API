using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace API.Auth;

public enum AuthAccountType
{
    User,
    DirectorUser
}

public sealed record AuthenticatedUser(
    int Id,
    string Email,
    string PasswordHash,
    UserRole Role,
    bool MfaEnabled,
    AuthAccountType AccountType);

public sealed record LoginLookupResult(
    AuthenticatedUser? User,
    bool AccountExists);

public class AuthUserLookupService
{
    private readonly AppDbContext _db;

    public AuthUserLookupService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LoginLookupResult> FindForLoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, ct);

        if (user is not null && PasswordVerifier.Verify(password, user.PasswordHash))
        {
            return new LoginLookupResult(ToAuthenticatedUser(user), AccountExists: true);
        }

        var directorUser = await _db.DirectorUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, ct);

        if (directorUser is not null && PasswordVerifier.Verify(password, directorUser.PasswordHash))
        {
            return new LoginLookupResult(ToAuthenticatedUser(directorUser), AccountExists: true);
        }

        return new LoginLookupResult(User: null, AccountExists: user is not null || directorUser is not null);
    }

    public async Task<AuthenticatedUser?> FindActiveByIdAsync(int id, AuthAccountType accountType, CancellationToken ct)
    {
        if (accountType == AuthAccountType.DirectorUser)
        {
            var directorUser = await _db.DirectorUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            return directorUser is null ? null : ToAuthenticatedUser(directorUser);
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

        return user is null ? null : ToAuthenticatedUser(user);
    }

    private static AuthenticatedUser ToAuthenticatedUser(User user) =>
        new(
            user.Id,
            user.Email,
            user.PasswordHash,
            user.Role,
            user.MfaEnabled == true,
            AuthAccountType.User);

    private static AuthenticatedUser ToAuthenticatedUser(DirectorUser user) =>
        new(
            user.Id,
            user.Email,
            user.PasswordHash,
            user.Role,
            user.MfaEnabled == true,
            AuthAccountType.DirectorUser);
}
