using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("roles")]
public class Role
{
    [Column("id")]
    public int Id { get; set; }

    [Column("role_name")]
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
