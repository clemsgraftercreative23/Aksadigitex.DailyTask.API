using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Reports;

public class RejectReportEndpoint : Endpoint<RejectReportRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;
    private readonly AppDbContext _db;
    private readonly ReportApprovalOptions _options;

    public RejectReportEndpoint(ReportStore store, AppDbContext db, IOptions<ReportApprovalOptions> options)
    {
        _store = store;
        _db = db;
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

        var report = _store.GetById(Route<int>("id"));
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

        var currentUser = await GetCurrentUserAsync(ct);
        if (currentUser is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var updated = _store.Reject(report.Id, req.Reason.Trim(), currentUser.ToReportUser());

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }

    private async Task<Domain.User> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId.HasValue)
        {
            return await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value, ct);
        }

        var email = User.GetEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
        }

        return null;
    }
}
