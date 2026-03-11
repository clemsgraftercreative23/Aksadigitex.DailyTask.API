using API.Users;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Auth;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest>
{
    private readonly AppDbContext _db;

    public ChangePasswordEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/auth/change-password");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Auth"));
        Summary(s => s.Summary = "Change password for authenticated user");
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var currentPassword = req.CurrentPassword?.Trim() ?? "";
        var newPassword = req.NewPassword?.Trim() ?? "";

        if (string.IsNullOrEmpty(currentPassword))
        {
            await SendAsync(new { success = false, message = "Kata sandi lama wajib diisi." }, 400, ct);
            return;
        }

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            await SendAsync(new { success = false, message = "Kata sandi baru minimal 6 karakter." }, 400, ct);
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!PasswordVerifier.Verify(currentPassword, user.PasswordHash))
        {
            await SendAsync(new { success = false, message = "Kata sandi lama salah." }, 400, ct);
            return;
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await _db.SaveChangesAsync(ct);

        await SendAsync(new { success = true, message = "Kata sandi berhasil diubah." }, cancellation: ct);
    }
}
