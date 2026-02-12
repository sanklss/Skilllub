using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("submissions")]
public class Submission : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("lesson_id")]
    public string LessonId { get; set; } = "";

    [Column("language_id")]
    public string LanguageId { get; set; } = "";

    [Column("code")]
    public string Code { get; set; } = "";

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("result")]
    public string? Result { get; set; }

    [Column("output")]
    public string? Output { get; set; }

    [Column("error")]
    public string? Error { get; set; }

    [Column("execution_time_ms")]
    public int? ExecutionTimeMs { get; set; }

    [Column("memory_kb")]
    public int? MemoryKb { get; set; }

    [Column("tests_passed")]
    public int? TestsPassed { get; set; }

    [Column("tests_total")]
    public int? TestsTotal { get; set; }

    [Column("score")]
    public int? Score { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}