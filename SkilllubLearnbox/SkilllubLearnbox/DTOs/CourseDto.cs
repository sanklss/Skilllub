namespace SkilllubLearnbox.DTOs;

public class CourseDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string DifficultyLevel { get; set; } = "";
    public bool IsPublished { get; set; }
    public string? CreatedBy { get; set; }
    public string? ProgrammingLanguageId { get; set; }
    public string? ProgrammingLanguageName { get; set; }
}