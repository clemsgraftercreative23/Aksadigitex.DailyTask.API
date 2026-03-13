using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using Domain;

namespace API.Reports;

public class SubmitReportEndpoint : RoleAuthorizedEndpointWithoutRequest<UpdateReportStatusResponse>
{
    private readonly ReportStore _store;

    public SubmitReportEndpoint(ReportStore store)
    {
        _store = store;
    }

    public override void Configure()
    {
        Post("v1/reports/{id}/submit");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Submit draft report";
            s.Description = "Changes a draft report status to submitted. Allowed roles: User, AdminDivisi, SuperAdmin, SuperDuperAdmin.";
        });
    }

    // Izinkan User, AdminDivisi, SuperAdmin, dan SuperDuperAdmin untuk submit report
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.User, UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var reportId = Route<int>("id");
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            AddError("User ID not found in token.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var existing = await _store.GetByIdAsync(reportId);
        if (existing is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (existing.UserId != userId.Value)
        {
            AddError("Anda tidak memiliki akses ke laporan ini.");
            await SendErrorsAsync(statusCode: 403, cancellation: ct);
            return;
        }

        if (existing.Status != null && existing.Status != "draft")
        {
            AddError($"Laporan tidak bisa dikirim karena status saat ini '{existing.Status}'.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var updated = await _store.SubmitAsync(reportId, userId.Value);

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
