using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

[Table("notifications")]
public class Notification
{
    [Column("id")]
    [Key]
    public int Id { get; set; }

    [Column("recipient_user_id")]
    public int RecipientUserId { get; set; }

    [Column("sender_type")]
    public string SenderType { get; set; } = "system";

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("reference_id")]
    public int? ReferenceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
