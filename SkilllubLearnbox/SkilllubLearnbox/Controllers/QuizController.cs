using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/quiz")]
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
}