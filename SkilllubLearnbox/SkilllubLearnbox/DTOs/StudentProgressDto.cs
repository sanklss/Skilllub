using SkilllubLearnbox.DTOs;

public class StudentProgressDto
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow; // ИСПРАВЛЕНО
    public int CourseProgress { get; set; }
    public List<ModuleProgressDto> Modules { get; set; } = new();
}