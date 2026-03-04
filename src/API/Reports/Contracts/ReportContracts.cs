using Microsoft.AspNetCore.Http;

namespace API.Reports;

public enum ReportStatus
{
    Pending,
    Approved,
    Rejected
}

public class CreateReportRequest
{
    public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ListReportsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ApproveReportRequest
{
    public string Note { get; set; } = string.Empty;
}

public class RejectReportRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class UploadAttachmentRequest
{
    public int Id { get; set; }
    public IFormFile File { get; set; } = default!;
}

public class ReportUserResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool? MfaEnabled { get; set; }
}

public class ReportAttachmentResponse
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public class ReportItemResponse
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
    public ReportUserResponse DecidedBy { get; set; } = new();
    public IReadOnlyList<ReportAttachmentResponse> Attachments { get; set; } = Array.Empty<ReportAttachmentResponse>();
}

public class CreateReportResponse
{
    public ReportItemResponse Item { get; set; } = new();
}

public class ListReportsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ReportItemResponse> Items { get; set; } = Array.Empty<ReportItemResponse>();
}

public class UpdateReportStatusResponse
{
    public ReportItemResponse Item { get; set; } = new();
}

public class UploadAttachmentResponse
{
    public ReportItemResponse Item { get; set; } = new();
}
