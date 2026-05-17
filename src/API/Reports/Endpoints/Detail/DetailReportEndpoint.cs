using API.Users;
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
        var accountType = User.GetAccountType();

        if (accountType == API.Auth.AuthAccountType.DirectorUser)
        {
            // ---- DirectorUser: query director_reports ----
            var dirReport = await _store.GetDirectorReportByIdAsync(reportId, ct);
            if (dirReport is null) { await SendNotFoundAsync(ct); return; }

            var resp = dirReport.ToResponse();

            // Enrich dengan data dari director_users
            var du = await _db.DirectorUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == dirReport.UserId, ct);

            if (du != null)
            {
                resp.UserFullName = du.FullName;
                resp.UserEmail    = du.Email;
                resp.UserPosition = "Director";
                resp.CompanyId    = du.CompanyId;
                resp.UserRoleName = "director";

                if (du.CompanyId.HasValue)
                    resp.CompanyName = await _db.Companies.AsNoTracking()
                        .Where(c => c.Id == du.CompanyId.Value)
                        .Select(c => c.CompanyName)
                        .FirstOrDefaultAsync(ct);
            }

            await SendAsync(new DetailReportResponse { Item = resp }, cancellation: ct);
        }
        else
        {
            // ---- Regular User: query daily_report (flow lama tidak berubah) ----
            var report = await _store.GetByIdAsync(reportId);
            if (report is null) { await SendNotFoundAsync(ct); return; }

            var resp = report.ToResponse();

            // Try users table first
            var user = await _db.Users.AsNoTracking()
                .Include(u => u.RoleRef)
                .FirstOrDefaultAsync(u => u.Id == report.UserId, ct);

            if (user != null)
            {
                resp.UserFullName = user.FullName;
                resp.UserEmail    = user.Email;
                resp.UserPosition = user.Position;
                resp.DepartmentId = user.DepartmentId;
                resp.CompanyId    = user.CompanyId;
                resp.UserRoleName = user.Role switch
                {
                    UserRole.User            => "user",
                    UserRole.AdminDivisi     => "admin_divisi",
                    UserRole.SuperAdmin      => "super_admin",
                    UserRole.SuperDuperAdmin => "super_duper_admin",
                    _                        => user.Role.ToString().ToLowerInvariant()
                };

                if (user.DepartmentId.HasValue)
                    resp.DepartmentName = await _db.Departments.AsNoTracking()
                        .Where(d => d.Id == user.DepartmentId.Value)
                        .Select(d => d.DepartmentName)
                        .FirstOrDefaultAsync(ct);

                if (user.CompanyId.HasValue)
                    resp.CompanyName = await _db.Companies.AsNoTracking()
                        .Where(c => c.Id == user.CompanyId.Value)
                        .Select(c => c.CompanyName)
                        .FirstOrDefaultAsync(ct);
            }
            else
            {
                // Fallback: cross-review scenario (director reviewing regular report)
                var directorUser = await _db.DirectorUsers.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == report.UserId, ct);

                if (directorUser != null)
                {
                    resp.UserFullName = directorUser.FullName;
                    resp.UserEmail    = directorUser.Email;
                    resp.CompanyId    = directorUser.CompanyId;
                    resp.UserRoleName = "director";

                    if (directorUser.CompanyId.HasValue)
                        resp.CompanyName = await _db.Companies.AsNoTracking()
                            .Where(c => c.Id == directorUser.CompanyId.Value)
                            .Select(c => c.CompanyName)
                            .FirstOrDefaultAsync(ct);
                }
            }

            await SendAsync(new DetailReportResponse { Item = resp }, cancellation: ct);
        }
    }
}
