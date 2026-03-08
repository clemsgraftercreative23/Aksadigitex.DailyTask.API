using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("departments")]
public class Department
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("department_name")]
    public string DepartmentName { get; set; } = string.Empty;
}
