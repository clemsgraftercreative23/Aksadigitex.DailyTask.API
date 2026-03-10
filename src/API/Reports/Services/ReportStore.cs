#nullable enable
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Dapper;

namespace API.Reports;

/// <summary>
/// Scope & urgency check result for review operations.
/// </summary>
public record ReviewCheckResult(bool Allowed, string? ErrorMessage);

public class ReportStore
{
    private readonly AppDbContext _context;

    public ReportStore(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get report with User for scope validation (department_id, company_id).
    /// </summary>
    public async Task<(DailyReport? Report, User? ReportUser)> GetReportWithUserAsync(int reportId, CancellationToken ct = default)
    {
        var report = await _context.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return (null, null);

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
        return (report, user);
    }

    /// <summary>
    /// Get total nominal from daily_report_finance_detail (for urgency check).
    /// </summary>
    public async Task<decimal> GetReportTotalNominalAsync(int reportId, CancellationToken ct = default)
    {
        var connection = _context.Database.GetDbConnection();
        var sql = "SELECT COALESCE(SUM(total_price), 0) FROM daily_report_finance_detail WHERE report_id = @ReportId";
        var result = await connection.QueryFirstOrDefaultAsync<decimal>(sql, new { ReportId = reportId });
        return result;
    }

    /// <summary>
    /// Get SDA threshold range (first active SDA's notif_threshold_min/max).
    /// </summary>
    public async Task<(decimal Min, decimal Max)> GetSdaThresholdRangeAsync(CancellationToken ct = default)
    {
        var sda = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == (int)UserRole.SuperDuperAdmin && u.IsActive)
            .Select(u => new { u.NotifThresholdMin, u.NotifThresholdMax })
            .FirstOrDefaultAsync(ct);
        return sda != null ? (sda.NotifThresholdMin, sda.NotifThresholdMax) : (0, 1000000);
    }

    /// <summary>
    /// Get current user's department, company, fullname for scope check.
    /// </summary>
    public async Task<(int? DepartmentId, int? CompanyId, string FullName)> GetReviewerContextAsync(int userId, CancellationToken ct = default)
    {
        var u = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.DepartmentId, x.CompanyId, x.FullName })
            .FirstOrDefaultAsync(ct);
        return u != null ? (u.DepartmentId, u.CompanyId, u.FullName) : (null, null, "");
    }

    /// <summary>
    /// Check if reviewer can approve/reject per about.md: scope + urgency.
    /// </summary>
    public async Task<ReviewCheckResult> CanReviewAsync(int reportId, UserRole reviewerRole, int? reviewerDepartmentId, int? reviewerCompanyId, bool isApprove, CancellationToken ct = default)
    {
        var (report, reportUser) = await GetReportWithUserAsync(reportId, ct);
        if (report is null || reportUser is null) return new ReviewCheckResult(false, "Laporan tidak ditemukan.");

        // Scope: admin_divisi → divisi sendiri; super_admin → perusahaan sendiri; SDA → semua
        if (reviewerRole == UserRole.AdminDivisi)
        {
            if (report.DepartmentId != reviewerDepartmentId)
                return new ReviewCheckResult(false, "Akses ditolak (Divisi berbeda).");
        }
        else if (reviewerRole == UserRole.SuperAdmin)
        {
            if (reportUser.CompanyId != reviewerCompanyId)
                return new ReviewCheckResult(false, "Akses ditolak (Perusahaan berbeda).");
        }
        // SuperDuperAdmin: no scope restriction

        // Urgency rule (approve only): is_asked_director OR high value → only SDA
        if (isApprove && reviewerRole != UserRole.SuperDuperAdmin)
        {
            var total = await GetReportTotalNominalAsync(reportId, ct);
            var (sdaMin, sdaMax) = await GetSdaThresholdRangeAsync(ct);
            var isHighValue = total > 0 && total >= sdaMin && total <= sdaMax;
            var isAskedDir = report.IsAskedDirector;

            if (isAskedDir || isHighValue)
            {
                var msg = isHighValue
                    ? $"Nominal Rp {total:N0} melebihi batas"
                    : "Permintaan bantuan ke CEO";
                return new ReviewCheckResult(false, $"Laporan Urgensi ({msg}) hanya dapat disetujui oleh Super Duper Admin.");
            }
        }

        return new ReviewCheckResult(true, null);
    }

    public async Task<DailyReport?> CreateAsync(int userId, DateOnly reportDate, TimeOnly reportTime, string taskDescription, string issue, string solution, string result, string status = "draft", int? departmentId = null)
    {
        var normalizedStatus = status?.ToLowerInvariant() == "submitted" ? "submitted" : "draft";
        var report = new DailyReport
        {
            UserId = userId,
            ReportDate = reportDate,
            ReportTime = reportTime,
            TaskDescription = taskDescription,
            Issue = issue,
            Solution = solution,
            Result = result,
            Status = normalizedStatus,
            DepartmentId = departmentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.DailyReports.Add(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<DailyReport?> SubmitAsync(int id, int userId)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (report is null) return null;
        report.Status = "submitted";
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<DailyReport?> GetByIdAsync(int id)
    {
        return await _context.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<(List<DailyReport> Items, int TotalCount)> ListAsync(int page, int pageSize)
    {
        var query = _context.DailyReports
            .Include(r => r.Attachments)
            .OrderByDescending(x => x.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<DailyReport?> ApproveAsync(int id, string note, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return null;

        report.Status = "approved";
        report.ManagerNote = note;
        _context.DailyReports.Update(report);

        var notif = new Notification
        {
            RecipientUserId = report.UserId,
            SenderType = "system",
            Message = $"Laporan Anda telah disetujui (Approved) oleh {reviewerName}.",
            Type = "laporan_diapprove",
            ReferenceId = id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync(ct);
        return report;
    }

    public async Task<DailyReport?> RejectAsync(int id, string reason, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return null;

        report.Status = "rejected";
        report.ManagerNote = reason;
        _context.DailyReports.Update(report);

        var notif = new Notification
        {
            RecipientUserId = report.UserId,
            SenderType = "system",
            Message = $"Laporan Anda ditolak/perlu revisi. Catatan: {reason}",
            Type = "laporan_direview",
            ReferenceId = id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync(ct);
        return report;
    }

    /// <summary>
    /// SDA gives director_solution for Standard report (daily_report). Per about.md §7.
    /// </summary>
    public async Task<DailyReport?> GiveSolutionAsync(int reportId, string directorSolution, string? managerNote, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.Include(r => r.Attachments).FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return null;

        report.DirectorSolution = directorSolution;
        report.ManagerNote = managerNote ?? report.ManagerNote;
        _context.DailyReports.Update(report);

        var notif = new Notification
        {
            RecipientUserId = report.UserId,
            SenderType = "system",
            Message = "SDA telah memberikan solusi untuk laporan Anda.",
            Type = "solution_ready",
            ReferenceId = reportId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync(ct);
        return report;
    }

    /// <summary>
    /// Ask director: set is_asked_director, notify admin_divisi or SDA. Per about.md §6.
    /// </summary>
    public async Task<bool> AskDirectorAsync(int reportId, int currentUserId, UserRole currentRole, string currentUserName, int? departmentId, int? companyId, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return false;

        report.IsAskedDirector = true;
        _context.DailyReports.Update(report);

        var reportUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);

        var issueText = (report.Issue ?? report.TaskDescription ?? "").Length > 80
            ? (report.Issue ?? report.TaskDescription ?? "").Substring(0, 80) + "..."
            : (report.Issue ?? report.TaskDescription ?? "");

        if (currentRole == UserRole.User)
        {
            var admins = await _context.Users
                .AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.AdminDivisi && u.DepartmentId == departmentId && u.CompanyId == companyId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);
            foreach (var adminId in admins)
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientUserId = adminId,
                    SenderType = "system",
                    Message = $"🟡 [BANTUAN] {reportUser?.FullName ?? ""} meminta bantuan untuk laporan: \"{issueText}\"",
                    Type = "urgent_solution",
                    ReferenceId = reportId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            var sdas = await _context.Users
                .AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.SuperDuperAdmin && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);
            var prefix = currentRole == UserRole.AdminDivisi ? "🔴 [URGENT] Admin " : "🔴 [URGENT] ";
            foreach (var sdaId in sdas)
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientUserId = sdaId,
                    SenderType = "system",
                    Message = $"{prefix}{currentUserName} meminta solusi: \"{issueText}\"",
                    Type = "urgent_solution",
                    ReferenceId = reportId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DailyReport?> SetRatingAsync(int id, int issueRating, int solutionRating)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id);
        if (report is null)
            return null;

        report.IssueRating = issueRating;
        report.SolutionRating = solutionRating;
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<DailyReportAttachment?> AddAttachmentAsync(int reportId, string attachmentPath, string fileType)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == reportId);
        if (report is null)
            return null;

        var attachment = new DailyReportAttachment
        {
            ReportId = reportId,
            AttachmentPath = attachmentPath,
            FileType = fileType,
            CreatedAt = DateTime.UtcNow
        };

        _context.DailyReportAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        return attachment;
    }
}

public static class ReportMappingExtensions
{
    public static ReportItemResponse ToResponse(this DailyReport report)
    {
        return new ReportItemResponse
        {
            Id = report.Id,
            UserId = report.UserId,
            ReportDate = report.ReportDate,
            ReportTime = report.ReportTime,
            TaskDescription = report.TaskDescription,
            Issue = report.Issue,
            Solution = report.Solution,
            Result = report.Result,
            Status = report.Status,
            ManagerNote = report.ManagerNote,
            DirectorSolution = report.DirectorSolution,
            IsAskedDirector = report.IsAskedDirector,
            IssueRating = report.IssueRating,
            SolutionRating = report.SolutionRating,
            CreatedAt = report.CreatedAt,
            Attachments = report.Attachments
                .Select(a => new ReportAttachmentResponse
                {
                    Id = a.Id,
                    AttachmentPath = a.AttachmentPath,
                    FileType = a.FileType,
                    CreatedAt = a.CreatedAt
                })
                .ToList()
        };
    }

    public static bool CanApproveOrReject(this ClaimsPrincipal user, ReportApprovalOptions options)
    {
        var roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var emails = user.Claims
            .Where(c => c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email || c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var roleAllowed = options.AllowedRoles.Any(r => roles.Contains(r));
        var emailAllowed = options.AllowedEmails.Any(e => emails.Contains(e));

        return roleAllowed || emailAllowed;
    }

    public static string GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? user.FindFirstValue("email")
            ?? string.Empty;
    }

    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub");

        return int.TryParse(value, out var id) ? id : null;
    }

    public static ReportUserResponse ToReportUser(this User user)
    {
        return new ReportUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            IsActive = user.IsActive,
            MfaEnabled = user.MfaEnabled
        };
    }
}
