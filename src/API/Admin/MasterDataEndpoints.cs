using API.Auth;
using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Admin;

// === Contracts ===
public class CompanyItemResponse
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyCode { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public class DepartmentItemResponse
{
    public int Id { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public List<int> CompanyIds { get; set; } = new();
    public List<string> CompanyNames { get; set; } = new();
}

public class PeriodItemResponse
{
    public int Id { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string? Deadline { get; set; }
    public bool IsActive { get; set; }
}

public class RoleItemResponse
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

// === Companies ===
public class ListCompaniesEndpoint : RoleAuthorizedEndpointWithoutRequest<object>
{
    private readonly AppDbContext _db;
    public ListCompaniesEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/admin/companies");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Companies"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var items = await _db.Companies.AsNoTracking().OrderBy(c => c.CompanyName)
            .Select(c => new CompanyItemResponse { Id = c.Id, CompanyName = c.CompanyName, CompanyCode = c.CompanyCode, Address = c.Address, IsActive = c.IsActive })
            .ToListAsync(ct);
        await SendAsync(new { companies = items }, cancellation: ct);
    }
}

public class CreateCompanyRequest { public string CompanyName { get; set; } = ""; public string? CompanyCode { get; set; } public string? Address { get; set; } }

public class CreateCompanyEndpoint : RoleAuthorizedEndpoint<CreateCompanyRequest, object>
{
    private readonly AppDbContext _db;
    public CreateCompanyEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Post("v1/admin/companies");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Companies"));
    }

    public override async Task HandleAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var company = new Company { CompanyName = req.CompanyName, CompanyCode = req.CompanyCode, Address = req.Address, IsActive = true, CreatedAt = DateTime.UtcNow };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);
        await SendAsync(new { success = true, id = company.Id }, cancellation: ct);
    }
}

public class UpdateCompanyRequest { public int Id { get; set; } public string CompanyName { get; set; } = ""; public string? CompanyCode { get; set; } public string? Address { get; set; } public bool IsActive { get; set; } }

public class UpdateCompanyEndpoint : RoleAuthorizedEndpoint<UpdateCompanyRequest, object>
{
    private readonly AppDbContext _db;
    public UpdateCompanyEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Put("v1/admin/companies/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Companies"));
    }

    public override async Task HandleAsync(UpdateCompanyRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (c is null) { await SendNotFoundAsync(ct); return; }
        c.CompanyName = req.CompanyName; c.CompanyCode = req.CompanyCode; c.Address = req.Address; c.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        await SendAsync(new { success = true }, cancellation: ct);
    }
}

public class DeleteCompanyRequest { public int Id { get; set; } }

public class DeleteCompanyEndpoint : RoleAuthorizedEndpoint<DeleteCompanyRequest, object>
{
    private readonly AppDbContext _db;
    public DeleteCompanyEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Delete("v1/admin/companies/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Companies"));
    }

    public override async Task HandleAsync(DeleteCompanyRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (c is null) { await SendNotFoundAsync(ct); return; }
        _db.Companies.Remove(c);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}

// === Departments ===
public class ListDepartmentsEndpoint : RoleAuthorizedEndpointWithoutRequest<object>
{
    private readonly AppDbContext _db;
    public ListDepartmentsEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/admin/departments");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Departments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var depts = await _db.Departments.AsNoTracking().OrderBy(d => d.DepartmentName).ToListAsync(ct);
        var companyDepts = await _db.CompanyDepartments.AsNoTracking().ToListAsync(ct);
        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);

        var items = depts.Select(d =>
        {
            var cds = companyDepts.Where(cd => cd.DepartmentId == d.Id).ToList();
            return new DepartmentItemResponse
            {
                Id = d.Id, DepartmentName = d.DepartmentName,
                CompanyIds = cds.Select(cd => cd.CompanyId).ToList(),
                CompanyNames = cds.Select(cd => companies.FirstOrDefault(c => c.Id == cd.CompanyId)?.CompanyName ?? "").ToList(),
            };
        }).ToList();

        var companyItems = companies.Select(c => new CompanyItemResponse { Id = c.Id, CompanyName = c.CompanyName }).ToList();

        await SendAsync(new { departments = items, companies = companyItems }, cancellation: ct);
    }
}

public class CreateDepartmentRequest { public string DepartmentName { get; set; } = ""; public List<int>? CompanyIds { get; set; } }

public class CreateDepartmentEndpoint : RoleAuthorizedEndpoint<CreateDepartmentRequest, object>
{
    private readonly AppDbContext _db;
    public CreateDepartmentEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Post("v1/admin/departments");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Departments"));
    }

    public override async Task HandleAsync(CreateDepartmentRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var dept = new Department { DepartmentName = req.DepartmentName };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);
        if (req.CompanyIds?.Any() == true)
        {
            foreach (var cid in req.CompanyIds)
                _db.CompanyDepartments.Add(new CompanyDepartment { CompanyId = cid, DepartmentId = dept.Id });
            await _db.SaveChangesAsync(ct);
        }
        await SendAsync(new { success = true, id = dept.Id }, cancellation: ct);
    }
}

public class UpdateDepartmentRequest { public int Id { get; set; } public string DepartmentName { get; set; } = ""; public List<int>? CompanyIds { get; set; } }

public class UpdateDepartmentEndpoint : RoleAuthorizedEndpoint<UpdateDepartmentRequest, object>
{
    private readonly AppDbContext _db;
    public UpdateDepartmentEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Put("v1/admin/departments/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Departments"));
    }

    public override async Task HandleAsync(UpdateDepartmentRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var dept = await _db.Departments.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (dept is null) { await SendNotFoundAsync(ct); return; }
        dept.DepartmentName = req.DepartmentName;
        var oldLinks = await _db.CompanyDepartments.Where(cd => cd.DepartmentId == req.Id).ToListAsync(ct);
        _db.CompanyDepartments.RemoveRange(oldLinks);
        if (req.CompanyIds?.Any() == true)
        {
            foreach (var cid in req.CompanyIds)
                _db.CompanyDepartments.Add(new CompanyDepartment { CompanyId = cid, DepartmentId = req.Id });
        }
        await _db.SaveChangesAsync(ct);
        await SendAsync(new { success = true }, cancellation: ct);
    }
}

public class DeleteDepartmentRequest { public int Id { get; set; } }

public class DeleteDepartmentEndpoint : RoleAuthorizedEndpoint<DeleteDepartmentRequest, object>
{
    private readonly AppDbContext _db;
    public DeleteDepartmentEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Delete("v1/admin/departments/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Departments"));
    }

    public override async Task HandleAsync(DeleteDepartmentRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var dept = await _db.Departments.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (dept is null) { await SendNotFoundAsync(ct); return; }
        var links = await _db.CompanyDepartments.Where(cd => cd.DepartmentId == req.Id).ToListAsync(ct);
        _db.CompanyDepartments.RemoveRange(links);
        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}

// === Periods ===
public class ListPeriodsEndpoint : RoleAuthorizedEndpointWithoutRequest<object>
{
    private readonly AppDbContext _db;
    public ListPeriodsEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Get("v1/admin/periods");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Periods"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var periods = await _db.ReportPeriods.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(ct);
        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);
        var items = periods.Select(p => new PeriodItemResponse
        {
            Id = p.Id, PeriodName = p.PeriodName, CompanyId = p.CompanyId,
            CompanyName = companies.FirstOrDefault(c => c.Id == p.CompanyId)?.CompanyName,
            StartDate = p.StartDate.ToString("yyyy-MM-dd"), EndDate = p.EndDate.ToString("yyyy-MM-dd"),
            Deadline = p.Deadline, IsActive = p.IsActive,
        }).ToList();
        var companyItems = companies.Select(c => new CompanyItemResponse { Id = c.Id, CompanyName = c.CompanyName }).ToList();
        await SendAsync(new { periods = items, companies = companyItems }, cancellation: ct);
    }
}

public class CreatePeriodRequest { public string PeriodName { get; set; } = ""; public int? CompanyId { get; set; } public string StartDate { get; set; } = ""; public string EndDate { get; set; } = ""; public string? Deadline { get; set; } }

public class CreatePeriodEndpoint : RoleAuthorizedEndpoint<CreatePeriodRequest, object>
{
    private readonly AppDbContext _db;
    public CreatePeriodEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Post("v1/admin/periods");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Periods"));
    }

    public override async Task HandleAsync(CreatePeriodRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var period = new ReportPeriod
        {
            PeriodName = req.PeriodName, CompanyId = req.CompanyId,
            StartDate = DateOnly.Parse(req.StartDate), EndDate = DateOnly.Parse(req.EndDate),
            Deadline = req.Deadline, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        _db.ReportPeriods.Add(period);
        await _db.SaveChangesAsync(ct);
        await SendAsync(new { success = true, id = period.Id }, cancellation: ct);
    }
}

public class UpdatePeriodRequest { public int Id { get; set; } public string PeriodName { get; set; } = ""; public int? CompanyId { get; set; } public string StartDate { get; set; } = ""; public string EndDate { get; set; } = ""; public string? Deadline { get; set; } public bool IsActive { get; set; } }

public class UpdatePeriodEndpoint : RoleAuthorizedEndpoint<UpdatePeriodRequest, object>
{
    private readonly AppDbContext _db;
    public UpdatePeriodEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Put("v1/admin/periods/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Periods"));
    }

    public override async Task HandleAsync(UpdatePeriodRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var p = await _db.ReportPeriods.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) { await SendNotFoundAsync(ct); return; }
        p.PeriodName = req.PeriodName; p.CompanyId = req.CompanyId;
        p.StartDate = DateOnly.Parse(req.StartDate); p.EndDate = DateOnly.Parse(req.EndDate);
        p.Deadline = req.Deadline; p.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        await SendAsync(new { success = true }, cancellation: ct);
    }
}

public class DeletePeriodRequest { public int Id { get; set; } }

public class DeletePeriodEndpoint : RoleAuthorizedEndpoint<DeletePeriodRequest, object>
{
    private readonly AppDbContext _db;
    public DeletePeriodEndpoint(AppDbContext db) => _db = db;
    protected override UserRole[]? GetAllowedRoles() => new[] { UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Delete("v1/admin/periods/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Periods"));
    }

    public override async Task HandleAsync(DeletePeriodRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;
        var p = await _db.ReportPeriods.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) { await SendNotFoundAsync(ct); return; }
        _db.ReportPeriods.Remove(p);
        await _db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}

// === Roles (read-only) ===
public class ListRolesEndpoint : EndpointWithoutRequest<object>
{
    private readonly AppDbContext _db;
    public ListRolesEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("v1/admin/roles");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Roles"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new RoleItemResponse { Id = r.Id, RoleName = r.RoleName }).ToListAsync(ct);
        await SendAsync(new { roles }, cancellation: ct);
    }
}
