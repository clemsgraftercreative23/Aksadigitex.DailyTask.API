using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;

namespace API.Reports;

public class UpdateManagerNoteRequest
{
    public string ManagerNote { get; set; } = string.Empty;
}

public class UpdateManagerNoteEndpoint : RoleAuthorizedEndpoint<UpdateManagerNoteRequest, UpdateReportStatusResponse>
{
    private readonly ReportStore _store;

    public UpdateManagerNoteEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Patch("v1/reports/{id}/manager-note");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Update manager note (CEO only)";
            s.Description = "Updates the manager note (catatan) on a report. SuperDuperAdmin (CEO) only.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(UpdateManagerNoteRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var reportId = Route<int>("id");
        var updated = await _store.UpdateManagerNoteAsync(reportId, req.ManagerNote?.Trim() ?? string.Empty, ct);
        if (updated is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new UpdateReportStatusResponse { Item = updated.ToResponse() }, cancellation: ct);
    }
}
