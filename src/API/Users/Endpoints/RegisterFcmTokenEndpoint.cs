using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class RegisterFcmTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class RegisterFcmTokenEndpoint : Endpoint<RegisterFcmTokenRequest>
{
    private readonly AppDbContext _db;

    public RegisterFcmTokenEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/users/fcm-token");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users"));
        Summary(s => s.Summary = "Register or update FCM token for push notifications");
    }

    public override async Task HandleAsync(RegisterFcmTokenRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var token = req.Token?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            await SendAsync(new { success = false, message = "Token tidak boleh kosong." }, 400, ct);
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        user.FcmToken = token;
        await _db.SaveChangesAsync(ct);

        await SendAsync(new { success = true, message = "FCM token berhasil disimpan." }, cancellation: ct);
    }
}
