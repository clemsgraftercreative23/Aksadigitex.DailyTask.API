using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using API.Reports;

namespace API.Dashboard;

public class DashboardResponse
{
    public int TotalReports { get; set; }
    public int TodayReports { get; set; }
    public int NotReportedCount { get; set; }
    public int TotalEmployees { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public string Date { get; set; } = string.Empty;
    public List<CompanyStatItem> CompanyStats { get; set; } = new();
    public List<DeptStatItem> DeptStats { get; set; } = new();
}

public class CompanyStatItem
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public int EmployeeCount { get; set; }
}

public class DeptStatItem
{
    public string DepartmentName { get; set; } = string.Empty;
    public int ReportCount { get; set; }
}

public class DashboardEndpoint : RoleAuthorizedEndpointWithoutRequest<DashboardResponse>
{
    private readonly AppDbContext _db;

    public DashboardEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("v1/dashboard");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Dashboard"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var currentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (currentUser is null) { await SendUnauthorizedAsync(ct); return; }

        var role = currentUser.Role;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStr = today.ToString("yyyy-MM-dd");

        IQueryable<DailyReport> reportQuery = _db.DailyReports.AsNoTracking();
        IQueryable<User> userQuery = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (role == UserRole.User)
        {
            reportQuery = reportQuery.Where(r => r.UserId == userId.Value);
            userQuery = userQuery.Where(u => u.Id == userId.Value);
        }
        else if (role == UserRole.AdminDivisi)
        {
            reportQuery = reportQuery.Where(r => r.DepartmentId == currentUser.DepartmentId);
            userQuery = userQuery.Where(u => u.DepartmentId == currentUser.DepartmentId);
        }
        else if (role == UserRole.SuperAdmin)
        {
            reportQuery = reportQuery.Join(_db.Users.AsNoTracking(), r => r.UserId, u => u.Id, (r, u) => new { r, u })
                .Where(x => x.u.CompanyId == currentUser.CompanyId).Select(x => x.r);
            userQuery = userQuery.Where(u => u.CompanyId == currentUser.CompanyId);
        }

        var totalReports = await reportQuery.CountAsync(ct);
        var todayReports = await reportQuery.Where(r => r.ReportDate == today).CountAsync(ct);
        var totalEmployees = await userQuery.CountAsync(ct);

        var statusCounts = await reportQuery
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var draft = statusCounts.FirstOrDefault(s => s.Status == "draft")?.Count ?? 0;
        var submitted = statusCounts.FirstOrDefault(s => s.Status == "submitted")?.Count ?? 0;
        var approved = statusCounts.FirstOrDefault(s => s.Status == "approved")?.Count ?? 0;
        var rejected = statusCounts.FirstOrDefault(s => s.Status == "rejected")?.Count ?? 0;

        var todayReporterIds = await reportQuery
            .Where(r => r.ReportDate == today)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        var notReportedCount = await userQuery
            .Where(u => u.RoleId <= (int)UserRole.AdminDivisi && !todayReporterIds.Contains(u.Id))
            .CountAsync(ct);

        await SendAsync(new DashboardResponse
        {
            TotalReports = totalReports,
            TodayReports = todayReports,
            NotReportedCount = notReportedCount,
            TotalEmployees = totalEmployees,
            DraftCount = draft,
            SubmittedCount = submitted,
            ApprovedCount = approved,
            RejectedCount = rejected,
            Date = todayStr,
        }, cancellation: ct);
    }
}
