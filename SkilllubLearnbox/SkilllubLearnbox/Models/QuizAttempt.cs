using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("quiz_attempts")]
public class QuizAttempt : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("lesson_id")]
    public string LessonId { get; set; } = "";

    [Column("score")]
    public double Score { get; set; }

    [Column("is_passed")]
    public bool IsPassed { get; set; }

    [Column("answers")]
    public string Answers { get; set; } = ""; 

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}