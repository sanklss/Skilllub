using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("user_courses")]
public class UserCourse : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("course_id")]
    public string CourseId { get; set; } = "";

    [Column("enrolled_at")]
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    [Column("progress")]
    public int Progress { get; set; }

    [Column("completed")]
    public bool Completed { get; set; }

    [Column("last_accessed")]
    public DateTime? LastAccessed { get; set; }
}