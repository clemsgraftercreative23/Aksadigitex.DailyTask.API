using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;

namespace API.Reports;

public class SetRatingEndpoint : RoleAuthorizedEndpoint<SetRatingRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;

    public SetRatingEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Patch("v1/reports/{id}/rating");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Set report ratings";
            s.Description = "Sets issue and solution ratings for a report (1-5). Only Super Duper Admin can set rating.";
        });
    }

    // Hanya SuperDuperAdmin yang boleh memberi rating
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(SetRatingRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        // Validasi rating value
        if (req.IssueRating < 1 || req.IssueRating > 5 || req.SolutionRating < 1 || req.SolutionRating > 5)
        {
            AddError("Ratings must be between 1 and 5.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);
        
        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var updated = await _store.SetRatingAsync(reportId, req.IssueRating, req.SolutionRating);

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
