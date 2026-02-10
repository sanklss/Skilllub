using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Services;
using SkilllubLearnbox.Utilities;


namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ILogger<CoursesController> _logger;
    private readonly CourseService _courseService;

    public CoursesController(ILogger<CoursesController> logger, CourseService courseService)
    {
        _logger = logger;
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCourses()
    {
        try
        {
            _logger.LogInformation("Получение списка курсов");
            var courses = await _courseService.GetAllCoursesAsync();

            return Ok(new
            {
                success = true,
                courses = courses
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("{courseId}")]
    public async Task<IActionResult> GetCourse(string courseId)
    {
        try
        {
            _logger.LogInformation("Получение курса: {CourseId}", courseId);
            var course = await _courseService.GetCourseByIdAsync(courseId);

            if (course == null)
            {
                return NotFound(new { success = false, error = "Курс не найден" });
            }

            return Ok(new
            {
                success = true,
                course = course
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курса {CourseId}", courseId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("{courseId}/modules")]
    public async Task<IActionResult> GetCourseModules(string courseId)
    {
        try
        {
            _logger.LogInformation("Получение модулей курса: {CourseId}", courseId);
            var modules = await _courseService.GetCourseModulesAsync(courseId);

            return Ok(new
            {
                success = true,
                modules = modules
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модулей курса {CourseId}", courseId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("modules/{moduleId}/lessons")]
    public async Task<IActionResult> GetModuleLessons(string moduleId)
    {
        try
        {
            _logger.LogInformation("Получение уроков модуля: {ModuleId}", moduleId);
            var lessons = await _courseService.GetModuleLessonsAsync(moduleId);

            return Ok(new
            {
                success = true,
                lessons = lessons
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении уроков модуля {ModuleId}", moduleId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("lessons/{lessonId}")]
    public async Task<IActionResult> GetLesson(string lessonId)
    {
        try
        {
            _logger.LogInformation("Получение урока: {LessonId}", lessonId);
            var lesson = await _courseService.GetLessonByIdAsync(lessonId);

            if (lesson == null)
            {
                return NotFound(new { success = false, error = "Урок не найден" });
            }

            return Ok(new
            {
                success = true,
                lesson = lesson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении урока {LessonId}", lessonId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("lessons/{lessonId}/code-template/{languageId}")]
    public async Task<IActionResult> GetLessonCodeTemplate(string lessonId, string languageId)
    {
        try
        {
            _logger.LogInformation("Получение шаблона кода для урока: {LessonId}, язык: {LanguageId}", lessonId, languageId);
            var template = await _courseService.GetLessonCodeTemplateAsync(lessonId, languageId);

            if (template == null)
            {
                return NotFound(new { success = false, error = "Шаблон кода не найден" });
            }

            return Ok(new
            {
                success = true,
                template = template
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении шаблона кода для урока {LessonId}", lessonId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}