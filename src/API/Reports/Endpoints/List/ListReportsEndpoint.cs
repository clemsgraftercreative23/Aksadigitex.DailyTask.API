using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace API.Reports;

public class ListReportsEndpoint : EndpointWithoutRequest<ListReportsResponse>
{
    private readonly ReportStore _store;

    public ListReportsEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Get("v1/reports");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Get reports with pagination";
            s.Description = "Returns paginated daily reports.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pageQuery = Query<int?>("page", isRequired: false);
        var pageSizeQuery = Query<int?>("pageSize", isRequired: false);

        var page = pageQuery.GetValueOrDefault(1);
        var pageSize = pageSizeQuery.GetValueOrDefault(10);

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

        var (items, totalCount) = _store.List(page, pageSize);

        await SendAsync(new ListReportsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items.Select(x => x.ToResponse()).ToList()
        }, cancellation: ct);
    }
}
