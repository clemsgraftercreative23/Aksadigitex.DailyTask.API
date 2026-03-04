using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace API.Reports;

public class CreateReportEndpoint : Endpoint<CreateReportRequest, CreateReportResponse>
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
            s.Description = "Creates a new daily report with pending status.";
        });
    }

    public override async Task HandleAsync(CreateReportRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
        {
            AddError("Title and content are required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var email = User.GetEmail();
        var item = _store.Create(req.ReportDate, req.Title.Trim(), req.Content.Trim(), email);

        await SendAsync(new CreateReportResponse
        {
            Item = item.ToResponse()
        }, cancellation: ct);
    }
}
