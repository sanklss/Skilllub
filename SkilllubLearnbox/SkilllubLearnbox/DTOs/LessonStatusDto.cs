namespace SkilllubLearnbox.DTOs;

public class LessonStatusDto
{
    public string LessonId { get; set; } = "";
    public bool TheoryCompleted { get; set; }
    public bool QuizCompleted { get; set; }
    public bool CodeCompleted { get; set; }
    public bool IsCompleted { get; set; }
    public int BestScore { get; set; }
    public int AttemptsCount { get; set; }
    public bool HasQuiz { get; set; }
    public bool HasCodeExercise { get; set; }

    [Obsolete("Используйте QuizCompleted и CodeCompleted")]
    public bool PracticeCompleted
    {
        get => QuizCompleted || CodeCompleted;
        set { }
    }
}