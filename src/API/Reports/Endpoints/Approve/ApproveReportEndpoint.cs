using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using API.Auth;
using Domain;

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
            s.Description = "Approves a report. Access is restricted to SuperAdmin role only.";
        });
    }

    // SuperAdmin dan SuperDuperAdmin bisa approve report
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(ApproveReportRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);
        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var updated = await _store.ApproveAsync(reportId, req.Note?.Trim() ?? string.Empty);

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
