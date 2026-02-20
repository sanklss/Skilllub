public class StudentDetailedProgressDto
{
    public string UserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public int TotalModules { get; set; }
    public int CompletedModules { get; set; }
    public List<ModuleDetailDto> Modules { get; set; }
}