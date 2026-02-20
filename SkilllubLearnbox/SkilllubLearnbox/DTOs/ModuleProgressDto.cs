using System.Collections.Generic;

namespace SkilllubLearnbox.DTOs;

public class ModuleProgressDto
{
    public string ModuleId { get; set; } = "";
    public string ModuleTitle { get; set; } = "";
    public int ModuleOrder { get; set; }
    public bool IsCompleted { get; set; }
    public List<LessonProgressDto> Lessons { get; set; } = new();
}