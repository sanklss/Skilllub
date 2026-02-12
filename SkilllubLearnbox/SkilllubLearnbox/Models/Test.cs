using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("tests")]
public class Test : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public string Id { get; set; } = "";

    [Column("lesson_id")]
    public string? LessonId { get; set; }

    [Column("language_id")]
    public string? LanguageId { get; set; }

    [Column("input")]
    public string Input { get; set; } = "";

    [Column("expected_output")]
    public string ExpectedOutput { get; set; } = "";

    [Column("test_order")]
    public int TestOrder { get; set; }

    [Column("is_hidden")]
    public bool IsHidden { get; set; } = false;

    [Column("timeout_ms")]
    public int TimeoutMs { get; set; } = 5000;

    [Column("weight")]
    public int Weight { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}