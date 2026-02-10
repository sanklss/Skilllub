using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("user_roles")]
public class UserRole : BaseModel
{
    [PrimaryKey("user_id", false)]
    [Column("user_id")]
    public string UserId { get; set; } = "";

    [PrimaryKey("role_id", false)]
    [Column("role_id")]
    public string RoleId { get; set; } = "";
}