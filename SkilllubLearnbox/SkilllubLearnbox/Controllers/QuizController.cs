using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly ILogger<QuizController> _logger;
    private readonly QuizService _quizService;
    private readonly ProgressService _progressService;

    public QuizController(
        ILogger<QuizController> logger,
        QuizService quizService,
        ProgressService progressService)
    {
        _logger = logger;
        _quizService = quizService;
        _progressService = progressService;
    }

    [HttpGet("lessons/{lessonId}/questions")]
    public async Task<IActionResult> GetLessonQuestions(string lessonId)
    {
        try
        {
            _logger.LogInformation("Получение вопросов для урока: {LessonId}", lessonId);
            var questions = await _quizService.GetQuizQuestionsByLessonAsync(lessonId);

            return Ok(new
            {
                success = true,
                questions = questions,
                count = questions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении вопросов для урока {LessonId}", lessonId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("lessons/{lessonId}/submit")]
    [Authorize]
    public async Task<IActionResult> SubmitQuizAnswers(string lessonId, [FromBody] QuizSubmitDto submitDto)
    {
        try
        {
            _logger.LogInformation("Отправка ответов на вопросы урока: {LessonId}", lessonId);

            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            if (submitDto.LessonId != lessonId)
            {
                return BadRequest(new { success = false, error = "Несоответствие идентификаторов уроков" });
            }

            var result = await _quizService.SubmitQuizAnswersAsync(userId, lessonId, submitDto.Answers);

            return Ok(new
            {
                success = true,
                result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке ответов на вопросы урока {LessonId}", lessonId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}