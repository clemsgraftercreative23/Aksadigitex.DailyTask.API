using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;
using System.Security.Claims;

namespace API.Reports;

/// <summary>
/// Ask director: set is_asked_director, notify admin/SDA. Per about.md §6.
/// </summary>
public class AskDirectorEndpoint : EndpointWithoutRequest<object>
{
    private readonly ReportStore _store;

    public AskDirectorEndpoint(ReportStore store) => _store = store;

    public override void Configure()
    {
        Post("v1/reports/{id}/ask-director");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Ask director";
            s.Description = "Minta bantuan ke admin/SDA. Per about.md §6. User→admin_divisi, Admin/SA→SDA.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var roleClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
        {
            await SendAsync(new { success = false, message = "Role tidak valid." }, 403, ct);
            return;
        }

        var (deptId, companyId, fullName) = await _store.GetReviewerContextAsync(userId.Value, ct);

        var reportId = Route<int>("id");
        var ok = await _store.AskDirectorAsync(reportId, userId.Value, role, fullName, deptId, companyId, ct);

        if (!ok)
        {
            await SendAsync(new { success = false, message = "Laporan tidak ditemukan." }, 404, ct);
            return;
        }

        await SendAsync(new { success = true, message = role == UserRole.User ? "Permintaan bantuan telah dikirim ke Admin Divisi Anda." : "Permintaan solusi telah dikirim ke Super Duper Admin." }, 200, ct);
    }
}
