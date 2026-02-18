namespace SkilllubLearnbox.DTOs;

public class CodeExecuteDto
{
    public string Code { get; set; } = "";
    public string Language { get; set; } = "python";
    public string LanguageId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? Stdin { get; set; }
    public int? TimeLimit { get; set; }
}

public class CodeSubmitDto
{
    public string Code { get; set; } = "";
    public string Language { get; set; } = "python";
    public string LanguageId { get; set; } = "";
    public string? Stdin { get; set; }
}

public class CodeExecutionResultDto
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public double ExecutionTimeMs { get; set; }
    public long? MemoryKb { get; set; }
    public List<TestResultDto>? TestResults { get; set; }
    public int? Score { get; set; }
    public int? PassedTests { get; set; }
    public int? TotalTests { get; set; }
}

public class TestResultDto
{
    public int TestId { get; set; }
    public bool Passed { get; set; }
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public string ActualOutput { get; set; } = "";
    public double ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsHidden { get; set; }
    public int Weight { get; set; }
}

public class TestDto
{
    public string Id { get; set; } = "";
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public bool IsHidden { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public int Weight { get; set; } = 1;
}

public class SubmissionDto
{
    public string Id { get; set; } = "";
    public string LessonId { get; set; } = "";
    public string Language { get; set; } = "";
    public string Status { get; set; } = "";
    public int? Score { get; set; }
    public int? TestsPassed { get; set; }
    public int? TestsTotal { get; set; }
    public double? ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}