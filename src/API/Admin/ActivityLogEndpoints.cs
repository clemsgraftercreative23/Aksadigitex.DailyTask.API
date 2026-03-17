using API.Auth;
using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Admin;

public class ActivityLogEntry
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentName { get; set; }
}

public class ActivityLogResponse
{
    public string Date { get; set; } = string.Empty;
    public List<ActivityLogEntry> NotReported { get; set; } = new();
}

public class ActivityLogEndpoint : RoleAuthorizedEndpointWithoutRequest<ActivityLogResponse>
{
    private readonly AppDbContext _db;
    public ActivityLogEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() =>
        new[] { UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/activity-log");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Activity Log"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var currentUser = await _db.Users.AsNoTracking()
            .Include(u => u.RoleRef)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (currentUser is null) { await SendUnauthorizedAsync(ct); return; }

        // Authorize using role_name from DB (handles role_id/enum mismatch)
        var roleName = currentUser.RoleRef?.RoleName?.Trim().ToLowerInvariant() ?? "";
        var roleAllowed = roleName is "admin_divisi" or "super_admin" or "super_duper_admin"
            || currentUser.Role is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin;
        if (!roleAllowed)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = "Akses ditolak: role tidak diizinkan mengakses halaman belum lapor" },
                cancellationToken: ct);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var usersQuery = _db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId <= (int)UserRole.AdminDivisi);

        if (roleName == "admin_divisi" || currentUser.Role == UserRole.AdminDivisi)
        {
            // admin_divisi: hanya bawahan (user) di departemen yang sama
            usersQuery = usersQuery.Where(u => u.DepartmentId == currentUser.DepartmentId && u.RoleId == (int)UserRole.User);
        }
        else if (roleName == "super_admin" || currentUser.Role == UserRole.SuperAdmin)
            usersQuery = usersQuery.Where(u => u.CompanyId == currentUser.CompanyId);

        var todayReporterIds = await _db.DailyReports.AsNoTracking()
            .Where(r => r.ReportDate == today)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);
        var departments = await _db.Departments.AsNoTracking().ToListAsync(ct);

        var notReported = await usersQuery
            .Where(u => !todayReporterIds.Contains(u.Id))
            .Select(u => new ActivityLogEntry
            {
                Id = u.Id,
                FullName = u.FullName,
                Position = u.Position,
            })
            .ToListAsync(ct);

        await SendAsync(new ActivityLogResponse
        {
            Date = today.ToString("yyyy-MM-dd"),
            NotReported = notReported,
        }, cancellation: ct);
    }
}
