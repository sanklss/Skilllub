using SkilllubLearnbox.Models;
using System;
using Xunit;

namespace SkilllubLearnbox.Tests.Models;

public class RefreshTokenModelTests
{
    [Fact]
    public void RefreshToken_CanBeCreated()
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-123",
            TokenHash = "hash123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            Revoked = false
        };
        Assert.False(token.Revoked);
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
    }
}