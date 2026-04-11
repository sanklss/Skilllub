using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class CodeModelsTests
{
    [Fact]
    public void CodeTemplate_CanBeCreated()
    {
        var template = new CodeTemplate
        {
            Id = Guid.NewGuid().ToString(),
            LessonId = "lesson-123",
            LanguageId = "lang-456",
            TemplateCode = "# Write your code here",
            StarterCode = "def solution():",
            SolutionCode = "def solution():\n    return 42"
        };
        Assert.Contains("solution", template.StarterCode);
    }

    [Fact]
    public void Test_CanBeCreated()
    {
        var test = new Test
        {
            Id = Guid.NewGuid().ToString(),
            LessonId = "lesson-123",
            LanguageId = "lang-456",
            Input = "5\n10",
            ExpectedOutput = "15",
            TestOrder = 1,
            IsHidden = false,
            TimeoutMs = 5000,
            Weight = 1
        };
        Assert.Equal("15", test.ExpectedOutput);
        Assert.False(test.IsHidden);
    }
}