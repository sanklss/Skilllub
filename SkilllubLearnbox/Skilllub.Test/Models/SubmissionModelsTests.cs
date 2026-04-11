using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class SubmissionModelsTests
{
    [Fact]
    public void Submission_CanBeCreated()
    {
        var submission = new Submission
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            LessonId = "lesson-456",
            LanguageId = "lang-789",
            Code = "print('Hello')",
            Status = "success",
            Score = 100,
            TestsPassed = 5,
            TestsTotal = 5,
            ExecutionTimeMs = 150,
            CreatedAt = DateTime.UtcNow
        };
        Assert.Equal("success", submission.Status);
        Assert.Equal(100, submission.Score);
        Assert.Equal(5, submission.TestsPassed);
    }
}