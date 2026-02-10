using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("code_templates")]
public class CodeTemplate : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("lesson_id")]
    public string LessonId { get; set; } = "";

    [Column("language_id")]
    public string LanguageId { get; set; } = "";

    [Column("template_code")]
    public string TemplateCode { get; set; } = "";

    [Column("starter_code")]
    public string StarterCode { get; set; } = "";

    [Column("solution_code")]
    public string SolutionCode { get; set; } = "";
}