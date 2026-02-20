public class TeacherCourseDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int StudentCount { get; set; }
    public int ModulesCount { get; set; }
    public int LessonsCount { get; set; }
    public double AverageProgress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // ИСПРАВЛЕНО
}