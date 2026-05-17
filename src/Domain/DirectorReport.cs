using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

/// <summary>
/// Maps to "director_reports" table. Parallel to DailyReport but with FK to director_users.
/// </summary>
[Table("director_reports")]
public class DirectorReport
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("report_date")]
    public DateOnly ReportDate { get; set; }

    [Column("report_time")]
    public TimeOnly ReportTime { get; set; }

    [Column("task_description")]
    public string TaskDescription { get; set; } = string.Empty;

    [Column("issue")]
    public string Issue { get; set; } = string.Empty;

    [Column("solution")]
    public string Solution { get; set; } = string.Empty;

    [Column("result")]
    public string Result { get; set; } = string.Empty;

    [Column("status")]
    public string? Status { get; set; } = null;

    [Column("manager_note")]
    public string? ManagerNote { get; set; }

    [Column("director_solution")]
    public string? DirectorSolution { get; set; }

    [Column("is_asked_director")]
    public bool IsAskedDirector { get; set; }

    [Column("attachment_path")]
    public string? AttachmentPath { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public ICollection<DirectorReportAttachment> Attachments { get; set; } = new List<DirectorReportAttachment>();
}

/// <summary>
/// Maps to "director_report_attachments" table.
/// </summary>
[Table("director_report_attachments")]
public class DirectorReportAttachment
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("report_id")]
    [ForeignKey(nameof(DirectorReport))]
    public int ReportId { get; set; }

    [Column("attachment_path")]
    public string AttachmentPath { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public DirectorReport? DirectorReport { get; set; }
}
