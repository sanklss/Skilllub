using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("modules")]
public class Module : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("course_id")]
    public string CourseId { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("module_order")]
    public int ModuleOrder { get; set; }
}