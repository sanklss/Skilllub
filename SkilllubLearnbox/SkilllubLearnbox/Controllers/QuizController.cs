using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;
using System.Security.Claims;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/quiz")]
[Authorize]
public class QuizController : ControllerBase
{
    private readonly ILogger<QuizController> _logger;
    private readonly QuizService _quizService;

    public QuizController(ILogger<QuizController> logger, QuizService quizService)
    {
        _logger = logger;
        _quizService = quizService;
    }

    [HttpGet("lessons/{lessonId}/questions")]
    public async Task<IActionResult> GetQuizQuestions(string lessonId)
    {
        try
        {
            var (success, questions, error) = await _quizService.GetQuizQuestionsAsync(lessonId);

            if (!success)
            {
                return Ok(new { success = false, error = error });
            }

            return Ok(new { success = true, questions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quiz questions");
            return Ok(new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("lessons/{lessonId}/submit")]
    public async Task<IActionResult> SubmitQuizAnswers(string lessonId, [FromBody] QuizSubmitDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var result = await _quizService.SubmitQuizAnswersAsync(userId, lessonId, dto.Answers);

            return Ok(new
            {
                success = true,
                result = new
                {
                    result.Score,
                    result.TotalQuestions,
                    result.CorrectAnswers,
                    result.IsPassed,
                    result.Message,
                    result.QuestionResults
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting quiz");
            return Ok(new { success = false, error = "Ошибка сервера" });
        }
    }
}