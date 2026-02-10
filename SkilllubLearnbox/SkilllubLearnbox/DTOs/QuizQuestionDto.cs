namespace SkilllubLearnbox.DTOs;

public class QuizQuestionDto
{
    public string Id { get; set; } = "";
    public string LessonId { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public string Option1 { get; set; } = "";
    public string Option2 { get; set; } = "";
    public string Option3 { get; set; } = "";
    public string Option4 { get; set; } = "";
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = "";
}

public class QuizAnswerDto
{
    public string QuestionId { get; set; } = "";
    public int UserAnswer { get; set; }
}

public class QuizSubmitDto
{
    public string LessonId { get; set; } = "";
    public List<QuizAnswerDto> Answers { get; set; } = new List<QuizAnswerDto>();
}

public class QuizResultDto
{
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public List<QuestionResultDto> QuestionResults { get; set; } = new List<QuestionResultDto>();
    public bool IsPassed { get; set; }
    public string Message { get; set; } = "";
}

public class QuestionResultDto
{
    public string QuestionId { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int UserAnswer { get; set; }
    public int CorrectAnswer { get; set; }
    public string Explanation { get; set; } = "";
}