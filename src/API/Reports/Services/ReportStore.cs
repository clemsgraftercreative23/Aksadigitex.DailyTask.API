using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace API.Reports;

public class ReportStore
{
    private readonly AppDbContext _context;

    public ReportStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DailyReport?> CreateAsync(int userId, DateOnly reportDate, TimeOnly reportTime, string taskDescription, string issue, string solution, string result, string status = "draft")
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

    public async Task<DailyReport?> ApproveAsync(int id, string note)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id);
        if (report is null)
            return null;

        report.Status = "approved";
        report.ManagerNote = note;
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<DailyReport?> RejectAsync(int id, string reason)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id);
        if (report is null)
            return null;

        report.Status = "rejected";
        report.ManagerNote = reason;
        _context.DailyReports.Update(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<DailyReport?> SetRatingAsync(int id, int rating)
    {
        var report = await _context.DailyReports.FirstOrDefaultAsync(x => x.Id == id);
        if (report is null)
            return null;

        report.Rating = rating;
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
            Rating = report.Rating,
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
