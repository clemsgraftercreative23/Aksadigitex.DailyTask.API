using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;

namespace API.Reports;

public class CreateReportEndpoint : RoleAuthorizedEndpoint<CreateReportRequest, CreateReportResponse>
{
    private readonly ReportStore _store;

    public CreateReportEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Post("v1/reports");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Create daily report";
            s.Description = "Creates a new daily report with pending status. Only User role can create reports.";
        });
    }

    // Hanya user dengan role User yang bisa create report
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.User };

    public override async Task HandleAsync(CreateReportRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        if (string.IsNullOrWhiteSpace(req.TaskDescription) || string.IsNullOrWhiteSpace(req.Issue))
        {
            AddError("TaskDescription and Issue are required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            AddError("User ID not found in token.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var report = await _store.CreateAsync(
            userId.Value,
            req.ReportDate,
            req.ReportTime,
            req.TaskDescription.Trim(),
            req.Issue.Trim(),
            req.Solution.Trim(),
            req.Result.Trim()
        );

        await SendAsync(new CreateReportResponse
        {
            Item = report!.ToResponse()
        }, cancellation: ct);
    }
}
