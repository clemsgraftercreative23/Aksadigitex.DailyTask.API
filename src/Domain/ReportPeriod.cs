using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("report_periods")]
public class ReportPeriod
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("period_name")]
    public string PeriodName { get; set; } = string.Empty;

    [Column("company_id")]
    public int? CompanyId { get; set; }

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Column("deadline")]
    public string? Deadline { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
