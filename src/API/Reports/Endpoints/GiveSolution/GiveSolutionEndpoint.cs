using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;
using System.Security.Claims;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace API.Reports;

/// <summary>
/// SDA gives director_solution. Per about.md §7.
/// Standard report (daily_report) only. Holding (director_reports) requires separate entities.
/// </summary>
public class GiveSolutionEndpoint : RoleAuthorizedEndpoint<GiveSolutionRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;
    private readonly AppDbContext _db;
    private readonly ILogger<GiveSolutionEndpoint> _logger;

    public GiveSolutionEndpoint(ReportStore store, AppDbContext db, ILogger<GiveSolutionEndpoint> logger)
    {
        _store = store;
        _db = db;
        _logger = logger;
    }

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

    protected override UserRole[] GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin, UserRole.SuperAdmin };

    public override async Task HandleAsync(GiveSolutionRequest req, CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var roleClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "-";

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.RoleRef)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        var departmentName = user?.DepartmentId.HasValue == true
            ? await _db.Departments
                .AsNoTracking()
                .Where(d => d.Id == user.DepartmentId!.Value)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync(ct)
            : null;

        _logger.LogInformation(
            "[GIVE_SOLUTION_AUDIT] userId={UserId}, fullName={FullName}, email={Email}, roleToken={RoleToken}, roleDb={RoleDb}, departmentId={DepartmentId}, departmentName={DepartmentName}, companyId={CompanyId}, reportId={ReportId}",
            userId.Value,
            user?.FullName ?? "-",
            user?.Email ?? "-",
            roleClaim,
            user?.RoleRef?.RoleName ?? user?.Role.ToString() ?? "-",
            user?.DepartmentId,
            departmentName ?? "-",
            user?.CompanyId,
            Route<int>("id")
        );

        if (!await ValidateRoleAsync(ct)) return;

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
