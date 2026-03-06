using Microsoft.AspNetCore.Http;

namespace API.Reports;

public class CreateReportRequest
{
    public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public TimeOnly ReportTime { get; set; } = TimeOnly.FromDateTime(DateTime.UtcNow);
    public string TaskDescription { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
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
    public string AttachmentPath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReportItemResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly ReportDate { get; set; }
    public TimeOnly ReportTime { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ManagerNote { get; set; }
    public DateTime CreatedAt { get; set; }
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

public class DetailReportResponse
{
    public ReportItemResponse Item { get; set; } = new();
}

public class UpdateReportStatusResponse
{
    public ReportItemResponse Item { get; set; } = new();
}

public class UploadAttachmentResponse
{
    public ReportItemResponse Item { get; set; } = new();
}
