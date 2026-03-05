using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace API.Reports;

public class RejectReportEndpoint : Endpoint<RejectReportRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;
    private readonly ReportApprovalOptions _options;

    public RejectReportEndpoint(ReportStore store, IOptions<ReportApprovalOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public override void Configure()
    {
        Post("v1/reports/{id}/reject");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Reject report";
            s.Description = "Rejects a report. Access is restricted by configured role or email.";
        });
    }

    public override async Task HandleAsync(RejectReportRequest req, CancellationToken ct)
    {
        if (!User.CanApproveOrReject(_options))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);
        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Reason))
        {
            AddError("Reason is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var updated = await _store.RejectAsync(reportId, req.Reason.Trim());

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
