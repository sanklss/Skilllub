using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/courses")]
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
            var courses = await _courseService.GetAllCoursesAsync();
            return Ok(new { success = true, courses });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("{courseId}")]
    public async Task<IActionResult> GetCourse(string courseId)
    {
        try
        {
            var course = await _courseService.GetCourseByIdAsync(courseId);

            if (course == null)
            {
                return NotFound(new { success = false, error = "Курс не найден" });
            }

            var courseDto = new CourseDto
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                DifficultyLevel = course.DifficultyLevel,
                IsPublished = course.IsPublished,
                CreatedBy = course.CreatedBy,
                ProgrammingLanguageId = course.ProgrammingLanguageId,
                ProgrammingLanguageName = course.ProgrammingLanguageName
            };

            return Ok(new { success = true, course = courseDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курса {CourseId}", courseId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("{courseId}/modules")]
    public async Task<IActionResult> GetCourseModules(string courseId, [FromQuery] string userId = null)
    {
        try
        {
            var modules = await _courseService.GetCourseModulesAsync(courseId, userId);
            return Ok(new { success = true, modules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модулей курса {CourseId}", courseId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("modules/{moduleId}/lessons")]
    public async Task<IActionResult> GetModuleLessons(string moduleId, [FromQuery] string userId = null)
    {
        try
        {
            var lessons = await _courseService.GetModuleLessonsAsync(moduleId, userId);
            return Ok(new { success = true, lessons });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении уроков модуля {ModuleId}", moduleId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("lessons/{lessonId}")]
    public async Task<IActionResult> GetLesson(string lessonId, [FromQuery] string userId = null)
    {
        try
        {
            var lesson = await _courseService.GetLessonByIdAsync(lessonId, userId);

            if (lesson == null)
            {
                return NotFound(new { success = false, error = "Урок не найден или недоступен" });
            }

            return Ok(new { success = true, lesson });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении урока {LessonId}", lessonId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("{courseId}/preload")]
    public async Task<IActionResult> PreloadCourseData(string courseId, [FromQuery] string userId = null)
    {
        try
        {
            await _courseService.PreloadCourseDataAsync(courseId, userId);

            return Ok(new
            {
                success = true,
                message = "Данные курса предзагружены в кэш"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при предзагрузке данных курса");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("lessons/{lessonId}/code-template/{languageId}")]
    public async Task<IActionResult> GetLessonCodeTemplate(string lessonId, string languageId, [FromQuery] string userId = null)
    {
        try
        {
            var template = await _courseService.GetLessonCodeTemplateAsync(lessonId, languageId, userId);

            if (template == null)
            {
                return NotFound(new { success = false, error = "Шаблон кода не найден" });
            }

            return Ok(new { success = true, template });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении шаблона кода для урока {LessonId}", lessonId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("debug/{courseId}")]
    public async Task<IActionResult> DebugCourse(string courseId)
    {
        try
        {
            var course = await _courseService.GetCourseByIdAsync(courseId);

            return Ok(new
            {
                success = true,
                course = new
                {
                    id = course?.Id,
                    title = course?.Title,
                    created_by = course?.CreatedBy  
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}