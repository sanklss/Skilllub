using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("submission_tests")]
public class SubmissionTest : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public string Id { get; set; } = "";

    [Column("submission_id")]
    public string SubmissionId { get; set; } = "";

    [Column("test_id")]
    public string TestId { get; set; } = "";

    [Column("passed")]
    public bool Passed { get; set; }

    [Column("actual_output")]
    public string? ActualOutput { get; set; }

    [Column("expected_output")]
    public string ExpectedOutput { get; set; } = "";

    [Column("execution_time_ms")]
    public int? ExecutionTimeMs { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}