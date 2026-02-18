using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/code")]
[Authorize]
public class CodeController : ControllerBase
{
    private readonly ILogger<CodeController> _logger;
    private readonly CodeExecutionService _codeExecutionService;
    private readonly CourseService _courseService;

    public CodeController(
        ILogger<CodeController> logger,
        CodeExecutionService codeExecutionService,
        CourseService courseService)
    {
        _logger = logger;
        _codeExecutionService = codeExecutionService;
        _courseService = courseService;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCode([FromBody] CodeExecuteDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            dto.UserId = userId;

            var lesson = await _courseService.GetLessonByIdAsync(dto.LessonId, userId);
            if (lesson == null)
            {
                return NotFound(new { success = false, error = "Урок не найден или недоступен" });
            }

            var result = await _codeExecutionService.ExecuteCodeAsync(dto);

            return Ok(new
            {
                success = true,
                result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing code");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("lessons/{lessonId}/run-tests")]
    public async Task<IActionResult> RunLessonTests(string lessonId, [FromBody] CodeSubmitDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;

            _logger.LogWarning("========== ВХОДНЫЕ ДАННЫЕ ==========");
            _logger.LogWarning("LessonId: {LessonId}", lessonId);
            _logger.LogWarning("Code length: {CodeLength}", dto.Code?.Length ?? 0);
            _logger.LogWarning("Language: {Language}", dto.Language);
            _logger.LogWarning("Stdin (как строка): '{Stdin}'", dto.Stdin);
            _logger.LogWarning("Stdin длина: {Length}", dto.Stdin?.Length ?? 0);
            _logger.LogWarning("Stdin байты: {Bytes}",
                string.Join(",", System.Text.Encoding.UTF8.GetBytes(dto.Stdin ?? "")));
            _logger.LogWarning("======================================");

            var lesson = await _courseService.GetLessonByIdAsync(lessonId, userId);
            if (lesson == null)
            {
                return NotFound(new { success = false, error = "Урок не найден или недоступен" });
            }

            var result = await _codeExecutionService.RunCodeTestsAsync(
                lessonId,
                dto.Code,
                dto.Language,
                userId,
                dto.Stdin ?? "" 
            );

            var allTestsPassed = result?.PassedTests == result?.TotalTests && result?.TotalTests > 0;

            return Ok(new
            {
                success = true,
                result,
                lessonCompleted = allTestsPassed,
                message = allTestsPassed ? "Задание выполнено! Урок завершен." : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tests for lesson {LessonId}", lessonId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("submissions/{lessonId}")]
    public async Task<IActionResult> GetUserSubmissions(string lessonId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var submissions = await _codeExecutionService.GetUserSubmissionsAsync(userId, lessonId);

            return Ok(new
            {
                success = true,
                submissions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting submissions");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}