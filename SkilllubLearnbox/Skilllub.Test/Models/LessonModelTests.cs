using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class LessonModelTests
{
    [Fact]
    public void Lesson_CanBeCreated()
    {
        var lesson = new Lesson
        {
            Id = Guid.NewGuid().ToString(),
            ModuleId = "module-123",
            Title = "Introduction",
            Description = "First lesson",
            Content = "<p>Lesson content</p>",
            LessonOrder = 1,
            Difficulty = "easy",
            HasQuiz = true,
            HasCode = false,
            CreatedAt = DateTime.UtcNow
        };
        Assert.Equal("Introduction", lesson.Title);
        Assert.True(lesson.HasQuiz);
        Assert.False(lesson.HasCode);
    }

    [Fact]
    public void Lesson_DefaultValues_AreFalse()
    {
        var lesson = new Lesson();
        Assert.False(lesson.HasQuiz);
        Assert.False(lesson.HasCode);
    }
}