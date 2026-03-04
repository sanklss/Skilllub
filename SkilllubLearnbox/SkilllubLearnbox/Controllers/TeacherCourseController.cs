using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Attributes;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/teacher/courses")]
[AuthorizeRoles("teacher", "admin")]
public class TeacherCourseController : ControllerBase
{
    private readonly ILogger<TeacherCourseController> _logger;
    private readonly TeacherCourseService _teacherCourseService;

    public TeacherCourseController(
        ILogger<TeacherCourseController> logger,
        TeacherCourseService teacherCourseService)
    {
        _logger = logger;
        _teacherCourseService = teacherCourseService;
    }

    [HttpPost("create-template")]
    public async Task<IActionResult> CreateCourseTemplate([FromBody] CreateCourseStructureDto dto)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var result = await _teacherCourseService.CreateCourseTemplateAsync(teacherId, dto);

            return Ok(new
            {
                success = true,
                course = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания шаблона курса");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPut("{courseId}/lesson/{lessonId}/quiz")]
    public async Task<IActionResult> UpdateLessonQuiz(string courseId, string lessonId, [FromBody] QuizContentDto quiz)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var result = await _teacherCourseService.UpdateLessonQuizAsync(teacherId, courseId, lessonId, quiz);

            return Ok(new
            {
                success = true,
                message = "Тест сохранен"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления теста");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPut("{courseId}/lesson/{lessonId}/code")]
    public async Task<IActionResult> UpdateLessonCode(string courseId, string lessonId, [FromBody] CodeContentDto code)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var result = await _teacherCourseService.UpdateLessonCodeAsync(teacherId, courseId, lessonId, code);

            return Ok(new
            {
                success = true,
                message = "Кодовое задание сохранено"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления кодового задания");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("{courseId}/publish")]
    public async Task<IActionResult> PublishCourse(string courseId, [FromBody] PublishCourseDto dto)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var result = await _teacherCourseService.PublishCourseAsync(teacherId, courseId);

            return Ok(new
            {
                success = true,
                message = "Курс успешно опубликован"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка публикации курса");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("drafts")]
    public async Task<IActionResult> GetDraftCourses()
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var drafts = await _teacherCourseService.GetDraftCoursesAsync(teacherId);

            return Ok(new
            {
                success = true,
                drafts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения черновиков");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("{courseId}/structure")]
    public async Task<IActionResult> GetCourseStructure(string courseId)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var structure = await _teacherCourseService.GetCourseStructureAsync(teacherId, courseId);

            return Ok(new
            {
                success = true,
                structure
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения структуры курса");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}