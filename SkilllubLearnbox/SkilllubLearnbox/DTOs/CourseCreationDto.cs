namespace SkilllubLearnbox.DTOs;

public class CreateCourseStructureDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string DifficultyLevel { get; set; } = "beginner";
    public int ModulesCount { get; set; } = 1;
    public List<ModuleTemplateDto> Modules { get; set; } = new();
    public string ProgrammingLanguageId { get; set; } = "11111111-1111-1111-1111-111111111111";
}

public class ModuleTemplateDto
{
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public int LessonsCount { get; set; } = 1;
    public List<LessonTemplateDto> Lessons { get; set; } = new();
}

public class LessonTemplateDto
{
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public bool HasTheory { get; set; } = true;
    public bool HasQuiz { get; set; } = false;
    public bool HasCode { get; set; } = false;
    public QuizContentDto? QuizContent { get; set; }
    public CodeContentDto? CodeContent { get; set; }
}

public class QuizContentDto
{
    public string QuestionText { get; set; } = "";
    public string Option1 { get; set; } = "";
    public string Option2 { get; set; } = "";
    public string Option3 { get; set; } = "";
    public string Option4 { get; set; } = "";
    public int CorrectOption { get; set; } = 1;
    public string Explanation { get; set; } = "";
}

public class CodeContentDto
{
    public string TaskDescription { get; set; } = "";
    public string StarterCode { get; set; } = "def solution():\n    # Напишите ваш код здесь\n    pass";
    public string SolutionCode { get; set; } = "";
    public List<TestCaseDto> TestCases { get; set; } = new();
}

public class TestCaseDto
{
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public bool IsHidden { get; set; } = false;
    public int TimeoutMs { get; set; } = 5000;
}

public class CourseTemplateResponseDto
{
    public string CourseId { get; set; } = "";
    public string Title { get; set; } = "";
    public int ModulesCount { get; set; }
    public int LessonsCount { get; set; }
    public bool IsDraft { get; set; } = true;
    public string Message { get; set; } = "";
}

public class PublishCourseDto
{
    public string CourseId { get; set; } = "";
}