namespace SkilllubLearnbox.DTOs;

public class TeacherActionDto
{
    public string UserId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public string Action { get; set; } = ""; // "complete", "reset", "mark_theory", etc.
    public int? Score { get; set; }
}