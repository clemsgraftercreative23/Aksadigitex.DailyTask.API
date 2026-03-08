using API.Auth;
using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Reports.Endpoints;

public class UrgentReportItem
{
    public int Id { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public string? Issue { get; set; }
    public string? Solution { get; set; }
    public string? Result { get; set; }
    public string? DirectorSolution { get; set; }
    public string? ManagerNote { get; set; }
    public string? AttachmentPath { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? DepartmentName { get; set; }
    public string? CompanyName { get; set; }
    public string ReportDate { get; set; } = string.Empty;
    public bool IsSolved { get; set; }
}

public class UrgentReportsResponse
{
    public List<UrgentReportItem> UrgentReports { get; set; } = new();
}

public class UrgentReportsEndpoint : RoleAuthorizedEndpointWithoutRequest<UrgentReportsResponse>
{
    private readonly AppDbContext _db;
    public UrgentReportsEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() =>
        new[] { UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/reports/urgent");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var reports = await _db.DailyReports.AsNoTracking()
            .Where(r => r.IsAskedDirector)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var userIds = reports.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync(ct);
        var departments = await _db.Departments.AsNoTracking().ToListAsync(ct);
        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);

        var items = reports.Select(r =>
        {
            var u = users.FirstOrDefault(x => x.Id == r.UserId);
            return new UrgentReportItem
            {
                Id = r.Id,
                TaskDescription = r.TaskDescription,
                Issue = r.Issue,
                Solution = r.Solution,
                Result = r.Result,
                DirectorSolution = r.DirectorSolution,
                ManagerNote = r.ManagerNote,
                AttachmentPath = r.AttachmentPath,
                FullName = u?.FullName ?? "",
                Position = u?.Position,
                DepartmentName = departments.FirstOrDefault(d => d.Id == u?.DepartmentId)?.DepartmentName,
                CompanyName = companies.FirstOrDefault(c => c.Id == u?.CompanyId)?.CompanyName,
                ReportDate = r.ReportDate.ToString("yyyy-MM-dd"),
                IsSolved = !string.IsNullOrEmpty(r.DirectorSolution),
            };
        }).ToList();

        await SendAsync(new UrgentReportsResponse { UrgentReports = items }, cancellation: ct);
    }
}
