using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class ModuleModelTests
{
    [Fact]
    public void Module_CanBeCreated()
    {
        var module = new Module
        {
            Id = Guid.NewGuid().ToString(),
            CourseId = "course-123",
            Title = "Module 1",
            Description = "First module",
            ModuleOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        Assert.Equal("Module 1", module.Title);
        Assert.Equal(1, module.ModuleOrder);
    }
}