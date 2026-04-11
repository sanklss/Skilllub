using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class CourseModelTests
{
    [Fact]
    public void Course_CanBeCreated()
    {
        var course = new Course
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Course",
            Description = "Test Description",
            DifficultyLevel = "intermediate",
            IsPublished = true,
            CreatedBy = "teacher-123",
            CreatedAt = DateTime.UtcNow,
            ProgrammingLanguageId = "lang-456",
            ProgrammingLanguageName = "Python"
        };
        Assert.Equal("Test Course", course.Title);
        Assert.True(course.IsPublished);
        Assert.Equal("Python", course.ProgrammingLanguageName);
    }

    [Fact]
    public void Course_DefaultIsPublished_False()
    {
        var course = new Course();
        Assert.False(course.IsPublished);
    }
}