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
