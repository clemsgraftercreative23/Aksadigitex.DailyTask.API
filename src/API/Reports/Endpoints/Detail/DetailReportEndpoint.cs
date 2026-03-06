using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace API.Reports;

public class DetailReportEndpoint : EndpointWithoutRequest<DetailReportResponse>
{
    private readonly ReportStore _store;

    public DetailReportEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Get("v1/reports/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Get report detail";
            s.Description = "Returns a single daily report by id.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);

        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new DetailReportResponse
        {
            Item = report.ToResponse()
        }, cancellation: ct);
    }
}
