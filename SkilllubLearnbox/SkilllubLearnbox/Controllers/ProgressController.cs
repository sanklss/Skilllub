using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProgressController : ControllerBase
{
    private readonly ILogger<ProgressController> _logger;
    private readonly ProgressService _progressService;
    private readonly CourseService _courseService;

    public ProgressController(
        ILogger<ProgressController> logger,
        ProgressService progressService,
        CourseService courseService)
    {
        _logger = logger;
        _progressService = progressService;
        _courseService = courseService;
    }

    [HttpPost("enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollInCourse([FromBody] EnrollCourseDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var result = await _progressService.EnrollUserInCourseAsync(userId, dto.CourseId);

            if (result)
            {
                await _progressService.InitializeModuleProgress(userId, dto.CourseId);

                return Ok(new
                {
                    success = true,
                    message = "Вы успешно записались на курс"
                });
            }
            else
            {
                return BadRequest(new { success = false, error = "Не удалось записаться на курс" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при записи на курс");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("check-enrollment/{courseId}")]
    [Authorize]
    public async Task<IActionResult> CheckEnrollment(string courseId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var isEnrolled = await _progressService.IsUserEnrolledInCourseAsync(userId, courseId);
            var progress = 0;

            if (isEnrolled)
            {
                progress = await _progressService.GetUserCourseProgressAsync(userId, courseId);
            }

            return Ok(new
            {
                success = true,
                isEnrolled,
                progress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке записи на курс");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("my-courses")]
    [Authorize]
    public async Task<IActionResult> GetMyCourses()
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var userCourses = await _progressService.GetUserCoursesAsync(userId);
            var allCourses = await _courseService.GetAllCoursesAsync();

            var result = userCourses.Select(uc =>
            {
                var course = allCourses.FirstOrDefault(c => c.Id == uc.CourseId);
                return new UserCourseWithDetailsDto
                {
                    CourseId = uc.CourseId,
                    Title = course?.Title ?? "Неизвестный курс",
                    Description = course?.Description ?? "",
                    Progress = uc.Progress,
                    Completed = uc.Completed,
                    EnrolledAt = uc.EnrolledAt,
                    LastAccessed = uc.LastAccessed
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                courses = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов пользователя");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("lesson/{lessonId}/complete")]
    [Authorize]
    public async Task<IActionResult> CompleteLesson(string lessonId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            await _progressService.UpdateUserProgressAsync(userId, lessonId);
            await _progressService.CompleteModuleIfAllLessonsDoneAsync(userId, lessonId);

            return Ok(new
            {
                success = true,
                message = "Прогресс обновлен"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса урока");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("lesson/{lessonId}/status")]
    [Authorize]
    public async Task<IActionResult> GetLessonStatus(string lessonId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            await _progressService.CheckAndUpdateUserProgress(userId, lessonId);
            var isCompleted = await _progressService.IsLessonCompletedAsync(userId, lessonId);

            return Ok(new
            {
                success = true,
                completed = isCompleted,
                lessonId = lessonId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке статуса урока");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("module/{moduleId}/status")]
    [Authorize]
    public async Task<IActionResult> GetModuleStatus(string moduleId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });
            }

            var isAccessible = await _progressService.IsModuleAccessibleAsync(userId, moduleId);
            var isCompleted = await _progressService.IsModuleCompletedAsync(userId, moduleId);

            return Ok(new
            {
                success = true,
                isAccessible,
                isCompleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса модуля");
            return Problem("Ошибка сервера");
        }
    }
}