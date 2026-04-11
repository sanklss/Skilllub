using Xunit;

namespace SkilllubLearnbox.Tests.Services;

public class PasswordHashingTests
{
    [Fact]
    public void BCrypt_ValidPassword_VerifiesCorrectly()
    {
        var password = "MySecurePassword123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
        Assert.True(isValid);
    }

    [Fact]
    public void BCrypt_InvalidPassword_FailsVerification()
    {
        var password = "CorrectPassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var isValid = BCrypt.Net.BCrypt.Verify(wrongPassword, hash);
        Assert.False(isValid);
    }

    [Fact]
    public void BCrypt_SamePassword_DifferentHashes()
    {
        var password = "SamePassword123!";
        var hash1 = BCrypt.Net.BCrypt.HashPassword(password);
        var hash2 = BCrypt.Net.BCrypt.HashPassword(password);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void BCrypt_EmptyPassword_GeneratesHash()
    {
        var password = "";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
        Assert.NotNull(hash);
        Assert.True(isValid);
    }

    [Fact]
    public void BCrypt_VeryLongPassword_WorksCorrectly()
    {
        var password = new string('A', 1000) + "123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
        Assert.True(isValid);
    }
}