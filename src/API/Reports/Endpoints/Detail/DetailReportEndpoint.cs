using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Reports;

public class DetailReportEndpoint : EndpointWithoutRequest<DetailReportResponse>
{
    private readonly ReportStore _store;
    private readonly AppDbContext _db;

    public DetailReportEndpoint(ReportStore store, AppDbContext db)
    {
        _store = store;
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/reports/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Get report detail";
            s.Description = "Returns a single daily report by id with user info.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);

        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var resp = report.ToResponse();

        var user = await _db.Users.AsNoTracking()
            .Include(u => u.RoleRef)
            .FirstOrDefaultAsync(u => u.Id == report.UserId, ct);

        if (user != null)
        {
            resp.UserFullName = user.FullName;
            resp.UserEmail = user.Email;
            resp.UserPosition = user.Position;
            resp.DepartmentId = user.DepartmentId;
            resp.CompanyId = user.CompanyId;
            resp.UserRoleName = user.Role switch
            {
                UserRole.User => "user",
                UserRole.AdminDivisi => "admin_divisi",
                UserRole.SuperAdmin => "super_admin",
                UserRole.SuperDuperAdmin => "super_duper_admin",
                _ => user.Role.ToString().ToLowerInvariant()
            };

            if (user.DepartmentId.HasValue)
                resp.DepartmentName = await _db.Departments.AsNoTracking()
                    .Where(d => d.Id == user.DepartmentId.Value).Select(d => d.DepartmentName).FirstOrDefaultAsync(ct);

            if (user.CompanyId.HasValue)
                resp.CompanyName = await _db.Companies.AsNoTracking()
                    .Where(c => c.Id == user.CompanyId.Value).Select(c => c.CompanyName).FirstOrDefaultAsync(ct);
        }

        await SendAsync(new DetailReportResponse { Item = resp }, cancellation: ct);
    }
}
