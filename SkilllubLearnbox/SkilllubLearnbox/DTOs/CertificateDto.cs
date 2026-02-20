namespace SkilllubLearnbox.DTOs;

public class CertificateDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CourseId { get; set; } = "";
    public string CertificateNumber { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public string StudentName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string? PdfUrl { get; set; }
    public string TeacherName { get; set; } = "";
}

public class CreateCertificateDto
{
    public string CourseId { get; set; } = "";
    public string CertificateNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string TeacherName { get; set; } = "";

}