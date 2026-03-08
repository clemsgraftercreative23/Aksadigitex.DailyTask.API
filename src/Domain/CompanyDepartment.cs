using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("company_departments")]
public class CompanyDepartment
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("department_id")]
    public int DepartmentId { get; set; }
}
