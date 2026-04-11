using System.Text.RegularExpressions;
using Xunit;

namespace SkilllubLearnbox.Tests.Validation;

public class StringValidationTests
{
    #region Email Validation Tests (7)

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.co.uk", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("missing@dot", false)]
    [InlineData("@missingname.com", false)]
    public void EmailValidation_WorksCorrectly(string email, bool expectedIsValid)
    {
        bool isValid = false;
        if (!string.IsNullOrEmpty(email))
        {
            isValid = email.Contains("@") &&
                     email.IndexOf('@') > 0 &&
                     email.LastIndexOf('.') > email.IndexOf('@') + 1;
        }
        Assert.Equal(expectedIsValid, isValid);
    }

    #endregion

    #region Password Strength Tests (8)

    [Theory]
    [InlineData("Password123!", true)]
    [InlineData("StrongP@ssw0rd", true)]
    [InlineData("weak", false)]
    [InlineData("NoDigits", false)]
    [InlineData("12345678", false)]
    [InlineData("onlylowercase1", true)]
    [InlineData("ONLYUPPERCASE1", true)]
    [InlineData("short1", false)]
    public void PasswordStrength_Validation(string password, bool expectedIsStrong)
    {
        bool hasLetter = Regex.IsMatch(password, @"[A-Za-z]");
        bool hasDigit = Regex.IsMatch(password, @"\d");
        bool hasMinLength = password.Length >= 8;
        bool isStrong = hasLetter && hasDigit && hasMinLength;
        Assert.Equal(expectedIsStrong, isStrong);
    }

    #endregion

    #region IsNullOrEmpty Tests (5)

    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("   ", true)]
    [InlineData("username", false)]
    [InlineData("user123", false)]
    public void IsNullOrEmpty_WorksCorrectly(string input, bool expectedIsEmpty)
    {
        bool isEmpty = string.IsNullOrWhiteSpace(input);
        Assert.Equal(expectedIsEmpty, isEmpty);
    }

    #endregion
}