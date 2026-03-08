using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using API.Auth;
using Domain;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace API.Reports;

public class ApproveReportEndpoint : RoleAuthorizedEndpoint<ApproveReportRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;
    private readonly ReportApprovalOptions _options;

    public ApproveReportEndpoint(ReportStore store, IOptions<ReportApprovalOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public override void Configure()
    {
        Post("v1/reports/{id}/approve");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Approve report";
            s.Description = "Approves a report. Per about.md: admin_divisi (divisi), super_admin (perusahaan), super_duper_admin (semua). Urgensi hanya SDA.";
        });
    }

    // Per about.md §5.1: admin_divisi, super_admin, super_duper_admin bisa review
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(ApproveReportRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct)) return;

        var userId = HttpContext.User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var roleClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
        { await SendForbiddenAsync(ct); return; }

        var (deptId, companyId, fullName) = await _store.GetReviewerContextAsync(userId.Value, ct);

        var reportId = Route<int>("id");
        var check = await _store.CanReviewAsync(reportId, role, deptId, companyId, isApprove: true, ct);
        if (!check.Allowed)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { message = check.ErrorMessage }, ct);
            return;
        }

        var updated = await _store.ApproveAsync(reportId, req.Note?.Trim() ?? string.Empty, fullName, ct);
        if (updated is null) { await SendNotFoundAsync(ct); return; }

        await SendAsync(new UpdateReportStatusResponse { Item = updated.ToResponse() }, cancellation: ct);
    }
}
