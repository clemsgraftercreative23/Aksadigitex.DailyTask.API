
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("users")]
public class User {
    [Column("id")]
    public int Id {get;set;}

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    public string Email {get;set;}="";

    [Column("password_hash")]
    public string PasswordHash {get;set;}="";

    [Column("role_id")]
    public int RoleId { get; set; } = (int)UserRole.User;

    [NotMapped]
    public UserRole Role
    {
        get => Enum.IsDefined(typeof(UserRole), RoleId) ? (UserRole)RoleId : UserRole.User;
        set => RoleId = (int)value;
    }

    [Column("position")]
    public string? Position { get; set; }

    [Column("company_id")]
    public int? CompanyId { get; set; }

    [Column("department_id")]
    public int? DepartmentId { get; set; }

    [Column("is_active")]
    public bool IsActive {get;set;}

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("mfa_secret")]
    public string? MfaSecret { get; set; }

    [Column("mfa_enabled")]
    public bool? MfaEnabled {get;set;}

    [Column("high_value_threshold")]
    public decimal? HighValueThreshold { get; set; }

    public Role? RoleRef { get; set; }
}
