using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class UserModelTests
{
    [Fact]
    public void User_CanBeCreated()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            Email = "test@example.com",
            Password = "hashedpassword",
            Phone = "+1234567890",
            LastLogin = DateTime.UtcNow
        };
        Assert.NotNull(user.Id);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public void User_WithAllFields_PropertiesWorkCorrectly()
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = id,
            Username = "fulluser",
            Email = "full@example.com",
            Password = "hash123",
            Phone = "+123",
            LastLogin = now
        };
        Assert.Equal(id, user.Id);
        Assert.Equal("fulluser", user.Username);
        Assert.Equal("full@example.com", user.Email);
        Assert.Equal("hash123", user.Password);
        Assert.Equal("+123", user.Phone);
        Assert.Equal(now, user.LastLogin);
    }
}