using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("courses")]
public class Course : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("difficulty_level")]
    public string DifficultyLevel { get; set; } = "beginner";

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }
}