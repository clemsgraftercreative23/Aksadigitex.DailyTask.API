#nullable enable
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Dapper;
using API.Services;
using Microsoft.Extensions.Logging;

namespace API.Reports;

/// <summary>
/// Scope & urgency check result for review operations.
/// </summary>
public record ReviewCheckResult(bool Allowed, string? ErrorMessage);

public class ReportStore
{
    private readonly AppDbContext _context;
    private readonly IFirebasePushService _fcm;
    private readonly ILogger<ReportStore> _logger;

    public ReportStore(AppDbContext context, IFirebasePushService fcm, ILogger<ReportStore> logger)
    {
        _context = context;
        _fcm = fcm;
        _logger = logger;
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
        if (user is null)
        {
            // Fallback: user_id might reference director_users table
            var directorUser = await _context.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
            if (directorUser is not null)
            {
                // Map DirectorUser to a User object for compatibility with downstream code
                user = new User
                {
                    Id = directorUser.Id,
                    FullName = directorUser.FullName,
                    Email = directorUser.Email,
                    RoleId = directorUser.RoleId,
                    CompanyId = directorUser.CompanyId,
                    IsActive = directorUser.IsActive,
                    CreatedAt = directorUser.CreatedAt,
                };
            }
        }
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
        if (u != null) return (u.DepartmentId, u.CompanyId, u.FullName);

        // Fallback: check director_users table
        var du = await _context.DirectorUsers.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.CompanyId, x.FullName })
            .FirstOrDefaultAsync(ct);
        return du != null ? (null, du.CompanyId, du.FullName) : (null, null, "");
    }

    /// <summary>
    /// Check if reviewer can approve/reject per about.md: scope + urgency.
    /// </summary>
    public async Task<ReviewCheckResult> CanReviewAsync(int reportId, UserRole reviewerRole, int? reviewerDepartmentId, int? reviewerCompanyId, bool isApprove, CancellationToken ct = default)
    {
        var (report, reportUser) = await GetReportWithUserAsync(reportId, ct);
        if (report is null || reportUser is null) return new ReviewCheckResult(false, "Laporan tidak ditemukan.");

        // Hierarchy: user → admin_divisi, super_admin, SDA; admin_divisi → super_admin, SDA; super_admin → SDA only
        var creatorRole = reportUser.Role;
        if (creatorRole == UserRole.AdminDivisi && reviewerRole == UserRole.AdminDivisi)
            return new ReviewCheckResult(false, "Admin Divisi tidak dapat menyetujui laporan Admin Divisi.");
        if (creatorRole == UserRole.SuperAdmin && reviewerRole != UserRole.SuperDuperAdmin)
            return new ReviewCheckResult(false, "Laporan Super Admin hanya dapat disetujui oleh Super Duper Admin.");
        if (creatorRole == UserRole.SuperDuperAdmin && reviewerRole != UserRole.SuperDuperAdmin)
            return new ReviewCheckResult(false, "Laporan Super Duper Admin hanya dapat disetujui oleh Super Duper Admin.");

        // Scope: admin_divisi → divisi sendiri; super_admin → perusahaan sendiri; SDA → semua
        if (reviewerRole == UserRole.AdminDivisi)
        {
            var reportDeptId = report.DepartmentId ?? reportUser.DepartmentId;
            if (reportDeptId != reviewerDepartmentId)
                return new ReviewCheckResult(false, "Akses ditolak (Divisi berbeda).");
        }
        else if (reviewerRole == UserRole.SuperAdmin)
        {
            if (reportUser.CompanyId != reviewerCompanyId)
                return new ReviewCheckResult(false, "Akses ditolak (Perusahaan berbeda).");
        }
        // SuperDuperAdmin: no scope restriction

        // Urgency rule (approve only): is_asked_director OR high value → SuperAdmin atau SuperDuperAdmin
        if (isApprove && reviewerRole != UserRole.SuperDuperAdmin && reviewerRole != UserRole.SuperAdmin)
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
                return new ReviewCheckResult(false, $"Laporan Urgensi ({msg}) hanya dapat disetujui oleh Super Admin atau Super Duper Admin.");
            }
        }

        return new ReviewCheckResult(true, null);
    }

    public async Task<ReviewCheckResult> CanReviewDirectorReportAsync(int reportId, UserRole reviewerRole, int? reviewerCompanyId, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return new ReviewCheckResult(false, "Laporan tidak ditemukan.");

        var reportUser = await _context.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
        if (reportUser is null) return new ReviewCheckResult(false, "Laporan tidak ditemukan.");

        var creatorRole = reportUser.Role;
        if (creatorRole == UserRole.AdminDivisi && reviewerRole == UserRole.AdminDivisi)
            return new ReviewCheckResult(false, "Admin Divisi tidak dapat menyetujui laporan Admin Divisi.");
        if (creatorRole == UserRole.SuperAdmin && reviewerRole != UserRole.SuperDuperAdmin)
            return new ReviewCheckResult(false, "Laporan Super Admin hanya dapat disetujui oleh Super Duper Admin.");
        if (creatorRole == UserRole.SuperDuperAdmin && reviewerRole != UserRole.SuperDuperAdmin)
            return new ReviewCheckResult(false, "Laporan Super Duper Admin hanya dapat disetujui oleh Super Duper Admin.");

        if ((reviewerRole is UserRole.AdminDivisi or UserRole.SuperAdmin) && reportUser.CompanyId != reviewerCompanyId)
            return new ReviewCheckResult(false, "Akses ditolak (Perusahaan berbeda).");

        return new ReviewCheckResult(true, null);
    }

    public async Task<DailyReport?> CreateAsync(int userId, DateOnly reportDate, TimeOnly reportTime, string taskDescription, string issue, string solution, string result, string status = "draft", int? departmentId = null, CancellationToken ct = default)
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
        await _context.SaveChangesAsync(ct);
        if (normalizedStatus == "submitted")
        {
            try
            {
                await NotifySuperiorOnReportSubmittedAsync(report.Id, report.UserId, ct);
            }
            catch (Exception ex)
            {
                // Best practice: side-effect failure must not break the main request.
                _logger.LogError(ex,
                    "NotifySuperiorOnReportSubmittedAsync failed for reportId={ReportId}, creatorUserId={CreatorUserId}",
                    report.Id, report.UserId);
            }
        }
        return report;
    }

    public async Task<DailyReport?> SubmitAsync(int id, int userId, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (report is null) return null;
        report.Status = "submitted";
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync(ct);
        try
        {
            await NotifySuperiorOnReportSubmittedAsync(report.Id, report.UserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifySuperiorOnReportSubmittedAsync failed for submit reportId={ReportId}, creatorUserId={CreatorUserId}",
                report.Id, report.UserId);
        }
        return report;
    }

    /// <summary>
    /// Notify superior when report is submitted. User→admin_divisi, admin_divisi→super_admin, super_admin→super_duper_admin.
    /// </summary>
    private async Task NotifySuperiorOnReportSubmittedAsync(int reportId, int creatorUserId, CancellationToken ct = default)
    {
        try
        {
            var creator = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId, ct);
            if (creator is null)
            {
                // Fallback: check director_users for creator info
                var dirCreator = await _context.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId, ct);
                if (dirCreator is null) return;
                creator = new User
                {
                    Id = dirCreator.Id, FullName = dirCreator.FullName, Email = dirCreator.Email,
                    RoleId = dirCreator.RoleId, CompanyId = dirCreator.CompanyId, IsActive = dirCreator.IsActive,
                };
            }

            var creatorName = creator.FullName ?? creator.Email ?? "User";
            var issueText = ""; // Will be populated from report
            var report = await _context.DailyReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report != null)
            {
                issueText = (report.Issue ?? report.TaskDescription ?? "").Length > 60
                    ? (report.Issue ?? report.TaskDescription ?? "").Substring(0, 60) + "..."
                    : (report.Issue ?? report.TaskDescription ?? "");
            }

            var msg = $"Laporan baru dari {creatorName}: \"{issueText}\"";
            var title = "LKH Baru Dikirim";
            var type = "laporan_submitted";

            if (creator.Role == UserRole.User)
            {
                var admins = await _context.Users.AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.AdminDivisi && u.DepartmentId == creator.DepartmentId && u.CompanyId == creator.CompanyId && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);
                foreach (var a in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = a.Id,
                        SenderType = "system",
                        Message = msg,
                        Type = type,
                        ReferenceId = reportId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(a.FcmToken, title, msg, type, reportId, ct);
                }
            }
            else if (creator.Role == UserRole.AdminDivisi)
            {
                var superAdmins = await _context.Users.AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.SuperAdmin && u.CompanyId == creator.CompanyId && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);
                foreach (var sa in superAdmins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = sa.Id,
                        SenderType = "system",
                        Message = msg,
                        Type = type,
                        ReferenceId = reportId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(sa.FcmToken, title, msg, type, reportId, ct);
                }
            }
            else if (creator.Role == UserRole.SuperAdmin)
            {
                var sdas = await _context.Users.AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.SuperDuperAdmin && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);
                foreach (var s in sdas)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = s.Id,
                        SenderType = "system",
                        Message = msg,
                        Type = type,
                        ReferenceId = reportId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(s.FcmToken, title, msg, type, reportId, ct);
                }
            }
            // SuperDuperAdmin: no superior to notify

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifySuperiorOnReportSubmittedAsync failed. reportId={ReportId}, creatorUserId={CreatorUserId}",
                reportId, creatorUserId);
            // swallow: report creation/submit must succeed even if notifications fail.
        }
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

        // First commit the main state change so approval does not fail if notification insert fails.
        await _context.SaveChangesAsync(ct);

        try
        {
            var approveMsg = $"Laporan Anda telah disetujui (Approved) oleh {reviewerName}.";
            await InsertNotificationForRecipientAsync(
                report.UserId, approveMsg, "laporan_diapprove", id, ct);

            var recipient = await _context.Users.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct)
                ?? await _context.DirectorUsers.AsNoTracking()
                    .Where(u => u.Id == report.UserId)
                    .Select(u => u.FcmToken)
                    .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipient,
                "Laporan Disetujui",
                approveMsg,
                "laporan_diapprove",
                id,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveAsync notification/FCM failed. reportId={ReportId}, reviewerName={ReviewerName}", id, reviewerName);
        }

        return report;
    }

    public async Task<DirectorReport?> ApproveDirectorReportAsync(int id, string note, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return null;

        report.Status = "approved";
        report.ManagerNote = note;
        _context.DirectorReports.Update(report);
        await _context.SaveChangesAsync(ct);

        try
        {
            var approveMsg = $"Laporan Anda telah disetujui (Approved) oleh {reviewerName}.";
            _context.DirectorNotifications.Add(new DirectorNotification
            {
                RecipientUserId = report.UserId,
                SenderType = "system",
                Message = approveMsg,
                Type = "laporan_diapprove",
                ReferenceId = id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(ct);

            var recipient = await _context.DirectorUsers.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipient,
                "Laporan Disetujui",
                approveMsg,
                "laporan_diapprove",
                id,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveDirectorReportAsync notification/FCM failed. reportId={ReportId}, reviewerName={ReviewerName}", id, reviewerName);
        }

        return report;
    }

    public async Task<DailyReport?> RejectAsync(int id, string reason, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return null;

        report.Status = "rejected";
        report.ManagerNote = reason;
        _context.DailyReports.Update(report);

        await _context.SaveChangesAsync(ct);

        try
        {
            var rejectMsg = $"Laporan Anda ditolak/perlu revisi. Catatan: {reason}";
            await InsertNotificationForRecipientAsync(
                report.UserId, rejectMsg, "laporan_direview", id, ct);

            var recipientReject = await _context.Users.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct)
                ?? await _context.DirectorUsers.AsNoTracking()
                    .Where(u => u.Id == report.UserId)
                    .Select(u => u.FcmToken)
                    .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipientReject,
                "Laporan Ditolak",
                rejectMsg,
                "laporan_direview",
                id,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectAsync notification/FCM failed. reportId={ReportId}", id);
        }

        return report;
    }

    public async Task<DirectorReport?> RejectDirectorReportAsync(int id, string reason, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (report is null) return null;

        report.Status = "rejected";
        report.ManagerNote = reason;
        _context.DirectorReports.Update(report);
        await _context.SaveChangesAsync(ct);

        try
        {
            var rejectMsg = $"Laporan Anda ditolak/perlu revisi. Catatan: {reason}";
            _context.DirectorNotifications.Add(new DirectorNotification
            {
                RecipientUserId = report.UserId,
                SenderType = "system",
                Message = rejectMsg,
                Type = "laporan_direview",
                ReferenceId = id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(ct);

            var recipientReject = await _context.DirectorUsers.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipientReject,
                "Laporan Ditolak",
                rejectMsg,
                "laporan_direview",
                id,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectDirectorReportAsync notification/FCM failed. reportId={ReportId}", id);
        }

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

        await _context.SaveChangesAsync(ct);

        try
        {
            var solMsg = "SDA telah memberikan solusi untuk laporan Anda.";
            await InsertNotificationForRecipientAsync(
                report.UserId, solMsg, "solution_ready", reportId, ct);

            var recipientSolution = await _context.Users.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct)
                ?? await _context.DirectorUsers.AsNoTracking()
                    .Where(u => u.Id == report.UserId)
                    .Select(u => u.FcmToken)
                    .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipientSolution,
                "Solusi Tersedia",
                solMsg,
                "solution_ready",
                reportId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GiveSolutionAsync notification/FCM failed. reportId={ReportId}", reportId);
        }

        return report;
    }

    public async Task<DirectorReport?> GiveSolutionDirectorReportAsync(int reportId, string directorSolution, string? managerNote, string reviewerName, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports.Include(r => r.Attachments).FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return null;

        report.DirectorSolution = directorSolution;
        report.ManagerNote = managerNote ?? report.ManagerNote;
        _context.DirectorReports.Update(report);
        await _context.SaveChangesAsync(ct);

        try
        {
            var solMsg = "SDA telah memberikan solusi untuk laporan Anda.";
            _context.DirectorNotifications.Add(new DirectorNotification
            {
                RecipientUserId = report.UserId,
                SenderType = "system",
                Message = solMsg,
                Type = "solution_ready",
                ReferenceId = reportId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(ct);

            var recipientSolution = await _context.DirectorUsers.AsNoTracking()
                .Where(u => u.Id == report.UserId)
                .Select(u => u.FcmToken)
                .FirstOrDefaultAsync(ct);
            await _fcm.SendAsync(recipientSolution,
                "Solusi Tersedia",
                solMsg,
                "solution_ready",
                reportId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GiveSolutionDirectorReportAsync notification/FCM failed. reportId={ReportId}", reportId);
        }

        return report;
    }

    /// <summary>
    /// Ask director: set is_asked_director, notify admin_divisi or SDA. Per about.md §6.
    /// </summary>
    public async Task<bool> AskDirectorAsync(int reportId, int currentUserId, UserRole currentRole, string currentUserName, int? departmentId, int? companyId, CancellationToken ct = default)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return false;

        try
        {
            report.IsAskedDirector = true;
            _context.DailyReports.Update(report);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskDirectorAsync failed to update report.IsAskedDirector. reportId={ReportId}", reportId);
            return false;
        }

        try
        {
            var reportUserRaw = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
            string? reportUserFullName = reportUserRaw?.FullName;
            if (reportUserRaw is null)
            {
                var dirUser = await _context.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
                reportUserFullName = dirUser?.FullName;
            }

            var issueText = (report.Issue ?? report.TaskDescription ?? "").Length > 80
                ? (report.Issue ?? report.TaskDescription ?? "").Substring(0, 80) + "..."
                : (report.Issue ?? report.TaskDescription ?? "");

            var msgUser = $"🟡 [BANTUAN] {reportUserFullName ?? ""} meminta bantuan untuk laporan: \"{issueText}\"";
            var prefix = currentRole == UserRole.AdminDivisi ? "🔴 [URGENT] Admin " : "🔴 [URGENT] ";
            var msgAdmin = $"{prefix}{currentUserName} meminta solusi: \"{issueText}\"";

            if (currentRole == UserRole.User)
            {
                var admins = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.AdminDivisi && u.DepartmentId == departmentId && u.CompanyId == companyId && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);

                foreach (var a in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = a.Id,
                        SenderType = "system",
                        Message = msgUser,
                        Type = "urgent_solution",
                        ReferenceId = reportId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(a.FcmToken,
                        "Permintaan Bantuan",
                        msgUser,
                        "urgent_solution",
                        reportId,
                        ct);
                }
            }
            else
            {
                var sdas = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.SuperDuperAdmin && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);

                foreach (var s in sdas)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = s.Id,
                        SenderType = "system",
                        Message = msgAdmin,
                        Type = "urgent_solution",
                        ReferenceId = reportId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(s.FcmToken,
                        "Permintaan Solusi Urgent",
                        msgAdmin,
                        "urgent_solution",
                        reportId,
                        ct);
                }
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskDirectorAsync notification/FCM failed. reportId={ReportId}", reportId);
            // The main state change is already saved; notification errors must not break the request.
        }

        return true;
    }

    public async Task<bool> AskDirectorReportAsync(int reportId, int currentUserId, UserRole currentRole, string currentUserName, int? companyId, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return false;

        try
        {
            report.IsAskedDirector = true;
            _context.DirectorReports.Update(report);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskDirectorReportAsync failed to update report.IsAskedDirector. reportId={ReportId}", reportId);
            return false;
        }

        try
        {
            var reportUser = await _context.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
            var issueText = (report.Issue ?? report.TaskDescription ?? "").Length > 80
                ? (report.Issue ?? report.TaskDescription ?? "").Substring(0, 80) + "..."
                : (report.Issue ?? report.TaskDescription ?? "");

            var msgUser = $"[BANTUAN] {reportUser?.FullName ?? ""} meminta bantuan untuk laporan: \"{issueText}\"";
            var prefix = currentRole == UserRole.AdminDivisi ? "[URGENT] Admin " : "[URGENT] ";
            var msgAdmin = $"{prefix}{currentUserName} meminta solusi: \"{issueText}\"";

            if (currentRole == UserRole.User)
            {
                var admins = await _context.DirectorUsers
                    .AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.AdminDivisi && u.CompanyId == companyId && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);

                foreach (var a in admins)
                {
                    _context.DirectorNotifications.Add(new DirectorNotification
                    {
                        RecipientUserId = a.Id,
                        SenderType = "system",
                        Message = msgUser,
                        Type = "urgent_solution",
                        ReferenceId = reportId,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(a.FcmToken,
                        "Permintaan Bantuan",
                        msgUser,
                        "urgent_solution",
                        reportId,
                        ct);
                }
            }
            else
            {
                var sdas = await _context.DirectorUsers
                    .AsNoTracking()
                    .Where(u => u.RoleId == (int)UserRole.SuperDuperAdmin && u.IsActive)
                    .Select(u => new { u.Id, u.FcmToken })
                    .ToListAsync(ct);

                foreach (var s in sdas)
                {
                    _context.DirectorNotifications.Add(new DirectorNotification
                    {
                        RecipientUserId = s.Id,
                        SenderType = "system",
                        Message = msgAdmin,
                        Type = "urgent_solution",
                        ReferenceId = reportId,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _fcm.SendAsync(s.FcmToken,
                        "Permintaan Solusi Urgent",
                        msgAdmin,
                        "urgent_solution",
                        reportId,
                        ct);
                }
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskDirectorReportAsync notification/FCM failed. reportId={ReportId}", reportId);
        }

        return true;
    }

    public async Task<DailyReport?> SetRatingAsync(int id, int? taskRating, int? issueRating, int? solutionRating)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id);
        if (report is null)
            return null;

        if (taskRating.HasValue)
            report.TaskRating = taskRating.Value;

        if (issueRating.HasValue)
            report.IssueRating = issueRating.Value;
        
        if (solutionRating.HasValue)
            report.SolutionRating = solutionRating.Value;
        
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync();
        return report;
    }

    /// <summary>
    /// Update manager note (catatan). CEO (SuperDuperAdmin) only.
    /// </summary>
    public async Task<DailyReport?> UpdateManagerNoteAsync(int reportId, string managerNote, CancellationToken ct = default)
    {
        var report = await _context.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null)
            return null;

        report.ManagerNote = managerNote;
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync(ct);
        return report;
    }

    public async Task<DirectorReport?> UpdateDirectorManagerNoteAsync(int reportId, string managerNote, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null)
            return null;

        report.ManagerNote = managerNote;
        _context.DirectorReports.Update(report);
        await _context.SaveChangesAsync(ct);
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
    // ───────────────────────────────────────────────────────
    //  DirectorReport CRUD — parallel to DailyReport methods
    // ───────────────────────────────────────────────────────

    /// <summary>
    /// Create a director_reports entry. DirectorUser FK is safe here.
    /// </summary>
    public async Task<DirectorReport?> CreateDirectorReportAsync(
        int userId, DateOnly reportDate, TimeOnly reportTime,
        string taskDescription, string issue, string solution, string result,
        string status = "draft", CancellationToken ct = default)
    {
        var normalizedStatus = status?.ToLowerInvariant() == "submitted" ? "submitted" : "draft";
        var report = new DirectorReport
        {
            UserId = userId,
            ReportDate = reportDate,
            ReportTime = reportTime,
            TaskDescription = taskDescription,
            Issue = issue,
            Solution = solution,
            Result = result,
            Status = normalizedStatus,
            CreatedAt = DateTime.UtcNow
        };

        _context.DirectorReports.Add(report);
        await _context.SaveChangesAsync(ct);

        if (normalizedStatus == "submitted")
        {
            try { await NotifyDirectorSuperiorAsync(report, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NotifyDirectorSuperiorAsync failed for directorReportId={ReportId}, userId={UserId}",
                    report.Id, report.UserId);
            }
        }
        return report;
    }

    public async Task<DirectorReport?> GetDirectorReportByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.DirectorReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<DirectorReport?> SubmitDirectorReportAsync(int id, int userId, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (report is null) return null;

        report.Status = "submitted";
        _context.DirectorReports.Update(report);
        await _context.SaveChangesAsync(ct);

        try { await NotifyDirectorSuperiorAsync(report, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifyDirectorSuperiorAsync failed for directorReportId={ReportId}, userId={UserId}",
                report.Id, report.UserId);
        }
        return report;
    }

    public async Task<DirectorReportAttachment?> AddDirectorAttachmentAsync(int reportId, string attachmentPath, CancellationToken ct = default)
    {
        var report = await _context.DirectorReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return null;

        var attachment = new DirectorReportAttachment
        {
            ReportId = reportId,
            AttachmentPath = attachmentPath,
            CreatedAt = DateTime.UtcNow
        };
        _context.DirectorReportAttachments.Add(attachment);
        await _context.SaveChangesAsync(ct);
        return attachment;
    }

    // ───────────────────────────────────────────────────────
    //  Notification routing helper
    // ───────────────────────────────────────────────────────

    /// <summary>
    /// Insert a notification to the correct table based on whether recipientUserId
    /// belongs to users or director_users.
    /// </summary>
    public async Task InsertNotificationForRecipientAsync(
        int recipientUserId, string message, string type, int? referenceId,
        CancellationToken ct = default)
    {
        // Check if recipient is a regular user (FK-safe for notifications table)
        var isRegularUser = await _context.Users.AsNoTracking()
            .AnyAsync(u => u.Id == recipientUserId, ct);

        if (isRegularUser)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientUserId = recipientUserId,
                SenderType = "system",
                Message = message,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            // Assume director_users — FK-safe for director_notifications table
            _context.DirectorNotifications.Add(new DirectorNotification
            {
                RecipientUserId = recipientUserId,
                SenderType = "system",
                Message = message,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Simple notification for submitted director reports (e.g., notify SDA).
    /// </summary>
    private async Task NotifyDirectorSuperiorAsync(DirectorReport report, CancellationToken ct)
    {
        var creator = await _context.DirectorUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == report.UserId, ct);
        if (creator is null) return;

        var desc = report.TaskDescription ?? "";
        var snippet = desc.Length > 80
            ? desc.Substring(0, 80) + "..."
            : desc;

        // Notify SDA users in the same company
        var sdaUsers = await _context.DirectorUsers.AsNoTracking()
            .Where(u => u.CompanyId == creator.CompanyId
                        && u.IsActive
                        && u.RoleId >= (int)UserRole.SuperAdmin
                        && u.Id != creator.Id)
            .Select(u => new { u.Id, u.FcmToken })
            .ToListAsync(ct);

        foreach (var sda in sdaUsers)
        {
            _context.DirectorNotifications.Add(new DirectorNotification
            {
                RecipientUserId = sda.Id,
                SenderType = "system",
                Message = $"📝 {creator.FullName} mengirim laporan: \"{snippet}\"",
                Type = "laporan_direview",
                ReferenceId = report.Id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });

            if (!string.IsNullOrEmpty(sda.FcmToken))
            {
                try
                {
                    await _fcm.SendAsync(sda.FcmToken, "Laporan Baru (Holding)", $"{creator.FullName}: \"{snippet}\"", "laporan_direview", report.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FCM push failed for directorUser {SdaId}", sda.Id);
                }
            }
        }
        await _context.SaveChangesAsync(ct);
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
            TaskRating = report.TaskRating,
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

    /// <summary>
    /// Maps a DirectorReport to the shared ReportItemResponse contract.
    /// DirectorReport has no rating columns, so they default to null.
    /// </summary>
    public static ReportItemResponse ToResponse(this DirectorReport report)
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
            TaskRating = null,
            IssueRating = null,
            SolutionRating = null,
            CreatedAt = report.CreatedAt,
            Attachments = report.Attachments
                .Select(a => new ReportAttachmentResponse
                {
                    Id = a.Id,
                    AttachmentPath = a.AttachmentPath,
                    FileType = string.Empty,
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
