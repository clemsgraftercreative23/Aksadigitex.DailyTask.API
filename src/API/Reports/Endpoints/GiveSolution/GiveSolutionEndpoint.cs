using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;
using System.Security.Claims;

namespace API.Reports;

/// <summary>
/// SDA gives director_solution. Per about.md §7.
/// Standard report (daily_report) only. Holding (director_reports) requires separate entities.
/// </summary>
public class GiveSolutionEndpoint : RoleAuthorizedEndpoint<GiveSolutionRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;

    public GiveSolutionEndpoint(ReportStore store) => _store = store;

    public override void Configure()
    {
        Post("v1/reports/{id}/give-solution");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Give solution (SDA)";
            s.Description = "SDA memberikan solusi untuk laporan. Per about.md §7. Saat ini hanya daily_report (Standard).";
        });
    }

    protected override UserRole[] GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(GiveSolutionRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var userId = HttpContext.User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var (_, _, fullName) = await _store.GetReviewerContextAsync(userId.Value, ct);

        var reportId = Route<int>("id");

        if (req.IsHolding)
        {
            HttpContext.Response.StatusCode = 501;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Laporan Holding (director_reports) belum didukung di API ini." }, ct);
            return;
        }

        var updated = await _store.GiveSolutionAsync(reportId, req.DirectorSolution, req.ManagerNote, fullName, ct);
        if (updated is null) { await SendNotFoundAsync(ct); return; }

        await SendAsync(new UpdateReportStatusResponse { Item = updated.ToResponse() }, cancellation: ct);
    }
}
