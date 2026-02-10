using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("refresh_tokens")]
public class RefreshToken : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("token_hash")]
    public string TokenHash { get; set; } = "";

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("revoked")]
    public bool Revoked { get; set; }
}