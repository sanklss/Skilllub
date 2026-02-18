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

    [Column("theory_completed")]
    public bool TheoryCompleted { get; set; } = false;

    [Column("quiz_completed")]
    public bool QuizCompleted { get; set; } = false;

    [Column("code_completed")]
    public bool CodeCompleted { get; set; } = false;

    [Obsolete("Используйте QuizCompleted и CodeCompleted вместо PracticeCompleted")]
    [Column("practice_completed")]
    public bool PracticeCompleted { get; set; } = false;

    [Column("best_score")]
    public int BestScore { get; set; } = 0;

    [Column("attempts_count")]
    public int AttemptsCount { get; set; } = 0;

    [Column("last_attempt")]
    public DateTime LastAttempt { get; set; } = DateTime.UtcNow;

    [Column("time_spent_ms")]
    public long TimeSpentMs { get; set; } = 0;
}