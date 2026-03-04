using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Domain;

namespace API.Reports;

public class ReportStore
{
    private readonly object _sync = new();
    private int _nextReportId = 1;
    private int _nextAttachmentId = 1;
    private readonly List<ReportRecord> _items = new();

    public ReportRecord Create(DateOnly reportDate, string title, string content, string createdByEmail)
    {
        lock (_sync)
        {
            var record = new ReportRecord
            {
                Id = _nextReportId++,
                ReportDate = reportDate,
                Title = title,
                Content = content,
                CreatedByEmail = createdByEmail,
                CreatedAtUtc = DateTime.UtcNow,
                Status = ReportStatus.Pending
            };

            _items.Add(record);
            return record;
        }
    }

    public (IReadOnlyList<ReportRecord> Items, int TotalCount) List(int page, int pageSize)
    {
        lock (_sync)
        {
            var ordered = _items
                .OrderByDescending(x => x.Id)
                .ToList();

            var totalCount = ordered.Count;
            var pageItems = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pageItems, totalCount);
        }
    }

    public ReportRecord GetById(int id)
    {
        lock (_sync)
        {
            return _items.FirstOrDefault(x => x.Id == id);
        }
    }

    public ReportRecord Approve(int id, string note, ReportUserResponse approver)
    {
        lock (_sync)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item is null)
                return null;

            item.Status = ReportStatus.Approved;
            item.ApprovalNote = note;
            item.RejectReason = string.Empty;
            item.DecisionAtUtc = DateTime.UtcNow;
            item.DecidedBy = approver;
            return item;
        }
    }

    public ReportRecord Reject(int id, string reason, ReportUserResponse approver)
    {
        lock (_sync)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item is null)
                return null;

            item.Status = ReportStatus.Rejected;
            item.ApprovalNote = string.Empty;
            item.RejectReason = reason;
            item.DecisionAtUtc = DateTime.UtcNow;
            item.DecidedBy = approver;
            return item;
        }
    }

    public ReportRecord AddAttachment(int reportId, string fileName, string contentType, long fileSize)
    {
        lock (_sync)
        {
            var item = _items.FirstOrDefault(x => x.Id == reportId);
            if (item is null)
                return null;

            item.Attachments.Add(new ReportAttachmentRecord
            {
                Id = _nextAttachmentId++,
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileSize,
                UploadedAtUtc = DateTime.UtcNow
            });

            return item;
        }
    }
}

public static class ReportMappingExtensions
{
    public static ReportItemResponse ToResponse(this ReportRecord record)
    {
        return new ReportItemResponse
        {
            Id = record.Id,
            ReportDate = record.ReportDate,
            Title = record.Title,
            Content = record.Content,
            Status = record.Status,
            CreatedByEmail = record.CreatedByEmail,
            CreatedAtUtc = record.CreatedAtUtc,
            ApprovalNote = record.ApprovalNote,
            RejectReason = record.RejectReason,
            DecisionAtUtc = record.DecisionAtUtc,
            DecidedBy = record.DecidedBy,
            Attachments = record.Attachments
                .Select(a => new ReportAttachmentResponse
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    UploadedAtUtc = a.UploadedAtUtc
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

public class ReportRecord
{
    public int Id { get; set; }
    public DateOnly ReportDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public string CreatedByEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string ApprovalNote { get; set; } = string.Empty;
    public string RejectReason { get; set; } = string.Empty;
    public DateTime? DecisionAtUtc { get; set; }
    public ReportUserResponse DecidedBy { get; set; }
    public List<ReportAttachmentRecord> Attachments { get; set; } = new();
}

public class ReportAttachmentRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
