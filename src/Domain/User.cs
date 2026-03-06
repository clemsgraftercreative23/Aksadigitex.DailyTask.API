
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("users")]
public class User {
    [Column("id")]
    public int Id {get;set;}
    [Column("email")]
    public string Email {get;set;}="";
    [Column("password_hash")]
    public string PasswordHash {get;set;}="";
    [Column("is_active")]
    public bool IsActive {get;set;}
    [Column("mfa_enabled")]
    public bool? MfaEnabled {get;set;}
    [Column("role")]
    public UserRole Role {get;set;} = UserRole.User;
}
