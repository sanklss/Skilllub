using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("username")]
    public string Username { get; set; } = "";

    [Column("email")]
    public string Email { get; set; } = "";

    [Column("phone")]
    public string Phone { get; set; } = "";

    [Column("password")]
    public string Password { get; set; } = "";

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }
}