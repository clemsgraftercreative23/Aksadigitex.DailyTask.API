using API.Auth;
using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Reports.Endpoints;

public class DeleteReportEndpoint : RoleAuthorizedEndpointWithoutRequest<object>
{
    private readonly AppDbContext _db;
    public DeleteReportEndpoint(AppDbContext db) => _db = db;

    // Allow all authenticated users - we'll check permissions in HandleAsync
    protected override UserRole[]? GetAllowedRoles() => null;

    public override void Configure()
    {
        Delete("v1/reports/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reportId = Route<int>("id");
        var userId = User.GetUserId();

        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var report = await _db.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Semua role: hanya bisa hapus laporan sendiri, dan hanya status draft
        if (report.UserId != userId.Value)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Anda hanya dapat menghapus laporan milik Anda sendiri." }, ct);
            return;
        }

        if (report.Status?.ToLowerInvariant() != "draft")
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Hanya laporan draft yang dapat dihapus." }, ct);
            return;
        }

        _db.DailyReportAttachments.RemoveRange(report.Attachments);
        _db.DailyReports.Remove(report);
        await _db.SaveChangesAsync(ct);

        await SendNoContentAsync(ct);
    }
}
