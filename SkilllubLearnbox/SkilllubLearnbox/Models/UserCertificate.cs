using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SkilllubLearnbox.Models;

[Table("certificates")]
public class UserCertificate : BaseModel 
{
    [PrimaryKey("id")]
    [Column("id")]
    public string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("course_id")]
    public string CourseId { get; set; } = "";

    [Column("certificate_number")]
    public string CertificateNumber { get; set; } = "";

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("student_name")]
    public string StudentName { get; set; } = "";

    [Column("course_name")]
    public string CourseName { get; set; } = "";

    [Column("pdf_url")]
    public string? PdfUrl { get; set; }

    [Column("teacher_name")]
    public string TeacherName { get; set; } = "";
}