using System.Security.Claims;
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
            s.Description = "Sets issue and solution ratings (1-5). AdminDivisi, SuperAdmin, SuperDuperAdmin can rate subordinates only (not own report).";
        });
    }

    // admin_divisi, superadmin, superduperadmin boleh memberi rating (ke bawahan saja)
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    /// <summary>Hierarchy: reviewer must be superior to report creator. user &lt; admin_divisi &lt; super_admin &lt; super_duper_admin</summary>
    private static bool CanRateByHierarchy(UserRole reviewerRole, UserRole creatorRole) =>
        (int)reviewerRole > (int)creatorRole;

    public override async Task HandleAsync(SetRatingRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        if ((req.IssueRating.HasValue && (req.IssueRating < 1 || req.IssueRating > 5)) ||
            (req.SolutionRating.HasValue && (req.SolutionRating < 1 || req.SolutionRating > 5)))
        {
            AddError("Ratings must be between 1 and 5 when provided.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var reportId = Route<int>("id");
        var (report, reportUser) = await _store.GetReportWithUserAsync(reportId, ct);

        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
        {
            await SendForbiddenAsync(ct, "User ID tidak ditemukan dalam token.");
            return;
        }

        // Tidak boleh kasih bintang ke laporan sendiri
        if (report.UserId == currentUserId)
        {
            await SendForbiddenAsync(ct, "Anda tidak dapat memberi rating pada laporan Anda sendiri.");
            return;
        }

        var reviewerRoleClaim = HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(reviewerRoleClaim) || !Enum.TryParse<UserRole>(reviewerRoleClaim, ignoreCase: true, out var reviewerRole))
        {
            await SendForbiddenAsync(ct, "Role tidak valid.");
            return;
        }

        var creatorRole = reportUser?.Role ?? UserRole.User;
        if (!CanRateByHierarchy(reviewerRole, creatorRole))
        {
            await SendForbiddenAsync(ct, "Anda hanya dapat memberi rating pada laporan bawahan Anda.");
            return;
        }

        // Rating hanya boleh pada laporan yang sudah disetujui (approved)
        var status = report.Status?.ToLowerInvariant() ?? "";
        if (status != "approved")
        {
            await SendForbiddenAsync(ct, "Rating hanya dapat diberikan pada laporan yang sudah disetujui.");
            return;
        }

        var updated = await _store.SetRatingAsync(reportId, req.IssueRating, req.SolutionRating);

        await SendAsync(new UpdateReportStatusResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
