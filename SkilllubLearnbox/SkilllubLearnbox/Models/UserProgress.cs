using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("user_progress")]
public class UserProgress : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("lesson_id")]
    public string? LessonId { get; set; }

    [Column("completed")]
    public bool Completed { get; set; } = false;

    [Column("best_score")]
    public int BestScore { get; set; } = 0;

    [Column("attempts_count")]
    public int AttemptsCount { get; set; } = 0;

    [Column("last_attempt")]
    public DateTime LastAttempt { get; set; } = DateTime.UtcNow;

    [Column("time_spent_ms")]
    public long TimeSpentMs { get; set; } = 0;
}