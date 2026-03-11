
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
    public int RoleId { get; set; } = 1;

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

    [Column("last_mfa_verified_at")]
    public DateTime? LastMfaVerifiedAt { get; set; }

    [Column("last_active_at")]
    public DateTime? LastActiveAt { get; set; }

    [Column("notif_threshold_min")]
    public decimal NotifThresholdMin { get; set; }

    [Column("notif_threshold_max")]
    public decimal NotifThresholdMax { get; set; } = 1000000;

    [Column("urgency_email")]
    public string? UrgencyEmail { get; set; }

    [Column("enable_urgensi")]
    public bool EnableUrgensi { get; set; } = true;

    [Column("fcm_token")]
    public string? FcmToken { get; set; }

    public Role? RoleRef { get; set; }
}
