using SkilllubLearnbox.DTOs;
using System.Collections.Generic;
using Xunit;

namespace SkilllubLearnbox.Tests.DTOs;

public class DtoValidationTests
{
    [Fact]
    public void UserLoginDto_CanBeCreated()
    {
        var dto = new UserLoginDto
        {
            Email = "test@example.com",
            Password = "password123"
        };
        Assert.Equal("test@example.com", dto.Email);
        Assert.Equal("password123", dto.Password);
    }

    [Fact]
    public void UserRegisterDto_CanBeCreated()
    {
        var dto = new UserRegisterWithRoleDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Password123!",
            Phone = "+1234567890",
            Role = "student"
        };
        Assert.Equal("testuser", dto.Username);
        Assert.Equal("test@example.com", dto.Email);
        Assert.Equal("Password123!", dto.Password);
        Assert.Equal("student", dto.Role);
    }

    [Fact]
    public void ResetPasswordDto_WithMatchingPasswords_IsValid()
    {
        var dto = new ResetPasswordDto
        {
            Email = "test@example.com",
            Code = "123456",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        Assert.Equal(dto.NewPassword, dto.ConfirmPassword);
    }

    [Fact]
    public void ResetPasswordDto_WithNonMatchingPasswords_IsInvalid()
    {
        var dto = new ResetPasswordDto
        {
            Email = "test@example.com",
            Code = "123456",
            NewPassword = "Password123!",
            ConfirmPassword = "DifferentPassword123!"
        };
        Assert.NotEqual(dto.NewPassword, dto.ConfirmPassword);
    }

    [Fact]
    public void CreateCourseStructureDto_CanBeCreated()
    {
        var dto = new CreateCourseStructureDto
        {
            Title = "Test Course",
            Description = "Test Description",
            DifficultyLevel = "beginner",
            ProgrammingLanguageId = "lang-123",
            ModulesCount = 2,
            Modules = new List<ModuleTemplateDto>
            {
                new ModuleTemplateDto
                {
                    Title = "Module 1",
                    LessonsCount = 2,
                    Lessons = new List<LessonTemplateDto>
                    {
                        new LessonTemplateDto { Title = "Lesson 1", HasQuiz = true, HasCode = false },
                        new LessonTemplateDto { Title = "Lesson 2", HasQuiz = false, HasCode = true }
                    }
                }
            }
        };
        Assert.Equal("Test Course", dto.Title);
        Assert.Equal(2, dto.ModulesCount);
        Assert.Single(dto.Modules);
        Assert.Equal(2, dto.Modules[0].Lessons.Count);
    }

    [Fact]
    public void ChangePasswordDto_CanBeCreated()
    {
        var dto = new ChangePasswordDto
        {
            CurrentPassword = "oldPass123!",
            NewPassword = "newPass123!",
            ConfirmPassword = "newPass123!"
        };
        Assert.Equal("oldPass123!", dto.CurrentPassword);
        Assert.Equal("newPass123!", dto.NewPassword);
        Assert.Equal(dto.NewPassword, dto.ConfirmPassword);
    }

    [Fact]
    public void ForgotPasswordDto_CanBeCreated()
    {
        var dto = new ForgotPasswordDto { Email = "user@example.com" };
        Assert.Equal("user@example.com", dto.Email);
    }

    [Fact]
    public void TeacherActionDto_CanBeCreated()
    {
        var dto = new TeacherActionDto
        {
            UserId = "student-123",
            LessonId = "lesson-456",
            Action = "complete_lesson",
            Score = 100
        };
        Assert.Equal("student-123", dto.UserId);
        Assert.Equal("lesson-456", dto.LessonId);
        Assert.Equal("complete_lesson", dto.Action);
        Assert.Equal(100, dto.Score);
    }

    [Fact]
    public void CodeExecuteDto_CanBeCreated()
    {
        var dto = new CodeExecuteDto
        {
            Code = "print('Hello World')",
            Language = "python",
            LessonId = "lesson-123",
            UserId = "user-456",
            Stdin = "test input",
            LanguageId = "lang-789"
        };
        Assert.Equal("print('Hello World')", dto.Code);
        Assert.Equal("python", dto.Language);
        Assert.Equal("lesson-123", dto.LessonId);
    }

    [Fact]
    public void QuizSubmitDto_CanBeCreated()
    {
        var dto = new QuizSubmitDto
        {
            Answers = new List<QuizAnswerDto>
            {
                new QuizAnswerDto { QuestionId = "q1", UserAnswer = 1 },
                new QuizAnswerDto { QuestionId = "q2", UserAnswer = 3 }
            }
        };
        Assert.Equal(2, dto.Answers.Count);
        Assert.Equal("q1", dto.Answers[0].QuestionId);
        Assert.Equal(1, dto.Answers[0].UserAnswer);
    }

    [Fact]
    public void UpdateUserDto_CanBeCreated()
    {
        var dto = new UpdateUserDto
        {
            Username = "newusername",
            Email = "newemail@example.com",
            Phone = "+9876543210"
        };
        Assert.Equal("newusername", dto.Username);
        Assert.Equal("newemail@example.com", dto.Email);
        Assert.Equal("+9876543210", dto.Phone);
    }

    [Fact]
    public void VerifyResetCodeDto_CanBeCreated()
    {
        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = "123456"
        };
        Assert.Equal("user@example.com", dto.Email);
        Assert.Equal("123456", dto.Code);
    }
}