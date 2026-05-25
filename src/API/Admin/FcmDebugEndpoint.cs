using API.Auth;
using API.Services;
using Domain;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace API.Admin;

public class SendFcmDebugRequest
{
    public string Token { get; set; } = string.Empty;
    public string Title { get; set; } = "Debug Firebase";
    public string Body { get; set; } = "Test notifikasi Firebase dari DailyTask API.";
    public string Type { get; set; } = "debug_fcm";
    public int? ReferenceId { get; set; }
}

public class SendFcmDebugEndpoint : RoleAuthorizedEndpoint<SendFcmDebugRequest, object>
{
    private readonly IFirebasePushService _fcm;

    public SendFcmDebugEndpoint(IFirebasePushService fcm) => _fcm = fcm;

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Post("v1/admin/notifications/fcm-test");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Notifications"));
        Summary(s => s.Summary = "Send a Firebase test notification directly to an FCM token");
    }

    public override async Task HandleAsync(SendFcmDebugRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var token = req.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await SendAsync(new { success = false, message = "Token FCM tidak boleh kosong." }, 400, ct);
            return;
        }

        var title = string.IsNullOrWhiteSpace(req.Title) ? "Debug Firebase" : req.Title.Trim();
        var body = string.IsNullOrWhiteSpace(req.Body) ? "Test notifikasi Firebase dari DailyTask API." : req.Body.Trim();
        var type = string.IsNullOrWhiteSpace(req.Type) ? "debug_fcm" : req.Type.Trim();

        var sent = await _fcm.SendAsync(token, title, body, type, req.ReferenceId, ct);
        if (!sent)
        {
            await SendAsync(new
            {
                success = false,
                message = "FCM gagal dikirim atau Firebase/token tidak valid. Cek konfigurasi Firebase dan log API."
            }, 502, ct);
            return;
        }

        await SendAsync(new
        {
            success = true,
            message = "FCM test notification berhasil dikirim."
        }, cancellation: ct);
    }
}
