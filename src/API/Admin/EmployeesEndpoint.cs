#nullable enable
using API.Auth;
using API.Users;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Admin;

public class EmployeeItemResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? CompanyName { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeesResponse
{
    public List<EmployeeItemResponse> Employees { get; set; } = new();
    public List<DeptStatWithCount> DeptStats { get; set; } = new();
}

public class DeptStatWithCount
{
    public int Id { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}

public class EmployeesEndpoint : RoleAuthorizedEndpointWithoutRequest<EmployeesResponse>
{
    private readonly AppDbContext _db;
    public EmployeesEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() =>
        new[] { UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/employees");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Employees"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var userId = UserClaims.GetUserId(User);
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var currentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (currentUser is null) { await SendUnauthorizedAsync(ct); return; }

        var role = currentUser.Role;
        var search = Query<string?>("search", isRequired: false);
        var deptId = Query<int?>("departmentId", isRequired: false);

        var query = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (role == UserRole.AdminDivisi)
            query = query.Where(u => u.DepartmentId == currentUser.DepartmentId);
        else if (role == UserRole.SuperAdmin)
            query = query.Where(u => u.CompanyId == currentUser.CompanyId);

        if (deptId.HasValue)
            query = query.Where(u => u.DepartmentId == deptId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.ToLower().Contains(search.ToLower()) || u.Email.ToLower().Contains(search.ToLower()));

        var departments = await _db.Departments.AsNoTracking().ToListAsync(ct);
        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);

        var users = await query.Include(u => u.RoleRef).OrderBy(u => u.FullName).ToListAsync(ct);

        var employees = users.Select(u => new EmployeeItemResponse
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            RoleName = u.RoleRef?.RoleName ?? u.Role.ToString(),
            DepartmentName = departments.FirstOrDefault(d => d.Id == u.DepartmentId)?.DepartmentName,
            CompanyName = companies.FirstOrDefault(c => c.Id == u.CompanyId)?.CompanyName,
            Position = u.Position,
            IsActive = u.IsActive,
        }).ToList();

        var deptStats = departments
            .Select(d => new DeptStatWithCount
            {
                Id = d.Id,
                DepartmentName = d.DepartmentName,
                EmployeeCount = users.Count(u => u.DepartmentId == d.Id),
            })
            .Where(d => d.EmployeeCount > 0)
            .ToList();

        await SendAsync(new EmployeesResponse
        {
            Employees = employees,
            DeptStats = deptStats,
        }, cancellation: ct);
    }
}
