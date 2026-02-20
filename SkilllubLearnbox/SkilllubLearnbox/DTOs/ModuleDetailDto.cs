public class ModuleDetailDto
{
    public string ModuleId { get; set; }
    public string ModuleTitle { get; set; }
    public int ModuleOrder { get; set; }
    public bool IsCompleted { get; set; }
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public List<LessonDetailDto> Lessons { get; set; }
}