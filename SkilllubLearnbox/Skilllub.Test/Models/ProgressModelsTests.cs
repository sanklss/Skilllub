using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class ProgressModelsTests
{
    [Fact]
    public void UserProgress_CanBeCreated()
    {
        var progress = new UserProgress
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            LessonId = "lesson-456",
            Completed = true,
            TheoryCompleted = true,
            QuizCompleted = true,
            CodeCompleted = false,
            BestScore = 85,
            AttemptsCount = 2,
            LastAttempt = DateTime.UtcNow
        };
        Assert.True(progress.Completed);
        Assert.Equal(85, progress.BestScore);
        Assert.Equal(2, progress.AttemptsCount);
    }

    [Fact]
    public void UserCourse_CanBeCreated()
    {
        var userCourse = new UserCourse
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            CourseId = "course-456",
            Progress = 75,
            Completed = false,
            EnrolledAt = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow
        };
        Assert.Equal(75, userCourse.Progress);
        Assert.False(userCourse.Completed);
    }
}