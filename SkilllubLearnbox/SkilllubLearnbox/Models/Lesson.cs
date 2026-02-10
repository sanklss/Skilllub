using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("lessons")]
public class Lesson : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("module_id")]
    public string ModuleId { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("content")]
    public string Content { get; set; } = "";

    [Column("lesson_order")]
    public int LessonOrder { get; set; }

    [Column("difficulty")]
    public string Difficulty { get; set; } = "easy";
}