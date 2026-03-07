using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("daily_report")]
public class DailyReport
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
    public string? Status { get; set; } = null; // pending, approved, rejected

    [Column("manager_note")]
    public string? ManagerNote { get; set; }

    [Column("director_solution")]
    public string? DirectorSolution { get; set; }

    [Column("is_asked_director")]
    public bool IsAskedDirector { get; set; }

    [Column("attachment_path")]
    public string? AttachmentPath { get; set; }

    [Column("department_id")]
    public int? DepartmentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("rating")]
    public int? Rating { get; set; } // 1-5 rating oleh SuperAdmin

    // Foreign key
    public User? User { get; set; }

    // Navigation property
    public ICollection<DailyReportAttachment> Attachments { get; set; } = new List<DailyReportAttachment>();
}

[Table("daily_report_attachments")]
public class DailyReportAttachment
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("report_id")]
    [ForeignKey(nameof(DailyReport))]
    public int ReportId { get; set; }

    [Column("attachment_path")]
    public string AttachmentPath { get; set; } = string.Empty;

    [Column("file_type")]
    public string FileType { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public DailyReport? DailyReport { get; set; }
}
