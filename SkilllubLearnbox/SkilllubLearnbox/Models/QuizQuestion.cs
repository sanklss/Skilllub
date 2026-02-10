using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("quiz_questions")]
public class QuizQuestion : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("lesson_id")]
    public string LessonId { get; set; } = "";

    [Column("question_text")]
    public string QuestionText { get; set; } = "";

    [Column("option1")]
    public string Option1 { get; set; } = "";

    [Column("option2")]
    public string Option2 { get; set; } = "";

    [Column("option3")]
    public string Option3 { get; set; } = "";

    [Column("option4")]
    public string Option4 { get; set; } = "";

    [Column("correct_option")]
    public int CorrectOption { get; set; }

    [Column("explanation")]
    public string Explanation { get; set; } = "";
}