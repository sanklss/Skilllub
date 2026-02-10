using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("roles")]
public class Role : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("description")]
    public string Description { get; set; } = "";
}