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
            s.Summary = "Set report rating";
            s.Description = "Sets rating for a report (1-5). Only SuperAdmin can set rating.";
        });
    }

    // SuperAdmin dan SuperDuperAdmin bisa set rating
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(SetRatingRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        // Validasi rating value
        if (req.Rating < 1 || req.Rating > 5)
        {
            AddError("Rating must be between 1 and 5.");
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

        var updated = await _store.SetRatingAsync(reportId, req.Rating);

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
