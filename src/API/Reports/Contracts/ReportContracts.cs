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
    public string Status { get; set; } = "draft"; // "draft" or "submitted"
}

public class ApproveReportRequest
{
    public string Note { get; set; } = string.Empty;
}

public class RejectReportRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class SetRatingRequest
{
    public int IssueRating { get; set; } // 1-5 rating masalah
    public int SolutionRating { get; set; } // 1-5 rating solusi
}

/// <summary>
/// SDA give solution. Per about.md §7.
/// </summary>
public class GiveSolutionRequest
{
    public string DirectorSolution { get; set; } = string.Empty;
    public string? ManagerNote { get; set; }
    /// <summary>True = director_reports (Holding), False = daily_report (Standard). .NET API currently supports Standard only.</summary>
    public bool IsHolding { get; set; }
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
    public string? DirectorSolution { get; set; }
    public bool IsAskedDirector { get; set; }
    public int? IssueRating { get; set; }
    public int? SolutionRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<ReportAttachmentResponse> Attachments { get; set; } = Array.Empty<ReportAttachmentResponse>();
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPosition { get; set; }
    public string? DepartmentName { get; set; }
    public string? CompanyName { get; set; }
    public int? DepartmentId { get; set; }
    public int? CompanyId { get; set; }
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
