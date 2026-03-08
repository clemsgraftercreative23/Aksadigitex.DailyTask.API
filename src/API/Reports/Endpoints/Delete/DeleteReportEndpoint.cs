using API.Auth;
using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Reports.Endpoints;

public class DeleteReportRequest
{
    public int Id { get; set; }
}

public class DeleteReportEndpoint : RoleAuthorizedEndpoint<DeleteReportRequest, object>
{
    private readonly AppDbContext _db;
    public DeleteReportEndpoint(AppDbContext db) => _db = db;

    protected override UserRole[]? GetAllowedRoles() =>
        new[] { UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override void Configure()
    {
        Delete("v1/reports/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
    }

    public override async Task HandleAsync(DeleteReportRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var report = await _db.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == req.Id, ct);

        if (report is null) { await SendNotFoundAsync(ct); return; }

        _db.DailyReportAttachments.RemoveRange(report.Attachments);
        _db.DailyReports.Remove(report);
        await _db.SaveChangesAsync(ct);

        await SendNoContentAsync(ct);
    }
}
