using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;
[Table("programming_languages")]
public class ProgrammingLanguage : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public new string Id { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("file_extension")]
    public string FileExtension { get; set; } = "";

    [Column("monaco_language_id")]
    public string MonacoLanguageId { get; set; } = "";

    [Column("enabled")]
    public bool Enabled { get; set; } = true;
}