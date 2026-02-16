namespace SkilllubLearnbox.DTOs;

public class LessonDto
{
    public string Id { get; set; } = "";
    public string ModuleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public int Order { get; set; }
    public string Difficulty { get; set; } = "easy";
    public bool IsTheoryCompleted { get; set; }
    public bool IsPracticeCompleted { get; set; }
    public bool IsCompleted { get; set; }
    public bool HasQuiz { get; set; }
    public bool HasCodeExercise { get; set; }

    [Obsolete("Use IsCompleted instead")]
    public bool IsCompletedLegacy
    {
        get => IsCompleted;
        set => IsCompleted = value;
    }
}