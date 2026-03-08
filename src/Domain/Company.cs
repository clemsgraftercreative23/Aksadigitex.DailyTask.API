using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("companies")]
public class Company
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Column("company_code")]
    public string? CompanyCode { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
