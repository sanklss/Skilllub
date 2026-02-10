namespace SkilllubLearnbox.DTOs;

public class EnrollCourseDto
{
    public string CourseId { get; set; } = "";
}

public class UserCourseWithDetailsDto
{
    public string CourseId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Progress { get; set; }
    public bool Completed { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastAccessed { get; set; }
}