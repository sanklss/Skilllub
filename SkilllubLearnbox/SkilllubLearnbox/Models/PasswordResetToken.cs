using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("password_reset_tokens")]
public class PasswordResetToken : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("code")]
    public string Code { get; set; } = "";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Column("used")]
    public bool Used { get; set; }

    [Column("attempts")]
    public int Attempts { get; set; }
}