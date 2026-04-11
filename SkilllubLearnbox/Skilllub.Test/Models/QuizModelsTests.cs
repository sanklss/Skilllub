using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class QuizModelsTests
{
    [Fact]
    public void QuizQuestion_CanBeCreated()
    {
        var question = new QuizQuestion
        {
            Id = Guid.NewGuid().ToString(),
            LessonId = "lesson-123",
            QuestionText = "What is 2+2?",
            Option1 = "3",
            Option2 = "4",
            Option3 = "5",
            Option4 = "6",
            CorrectOption = 2,
            Explanation = "2+2 equals 4"
        };
        Assert.Equal("What is 2+2?", question.QuestionText);
        Assert.Equal(2, question.CorrectOption);
    }

    [Fact]
    public void QuizAttempt_CanBeCreated()
    {
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            LessonId = "lesson-456",
            Score = 85,
            IsPassed = true,
            Answers = "[]",
            CreatedAt = DateTime.UtcNow
        };
        Assert.Equal(85, attempt.Score);
        Assert.True(attempt.IsPassed);
    }
}