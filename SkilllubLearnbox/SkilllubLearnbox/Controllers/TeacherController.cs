using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Attributes;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Services;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/teacher")]
[AuthorizeRoles("teacher", "admin")] 
public class TeacherController : ControllerBase
{
    private readonly ILogger<TeacherController> _logger;
    private readonly TeacherService _teacherService;

    public TeacherController(ILogger<TeacherController> logger, TeacherService teacherService)
    {
        _logger = logger;
        _teacherService = teacherService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var dashboard = await _teacherService.GetTeacherDashboardAsync(teacherId);

            return Ok(new
            {
                success = true,
                dashboard
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения дашборда");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("courses/{courseId}/students")]
    public async Task<IActionResult> GetCourseStudents(string courseId)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var students = await _teacherService.GetCourseStudentsAsync(courseId, teacherId);

            return Ok(new
            {
                success = true,
                students
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения студентов курса {CourseId}", courseId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("students/{studentId}/courses/{courseId}/progress")]
    public async Task<IActionResult> GetStudentProgress(string studentId, string courseId)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var courseResponse = await _teacherService.GetCourseStudentsAsync(courseId, teacherId);
            if (courseResponse == null || !courseResponse.Any(s => s.UserId == studentId))
            {
                return Forbid();
            }

            var progress = await _teacherService.GetStudentDetailedProgressAsync(studentId, courseId);

            return Ok(new
            {
                success = true,
                progress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения прогресса студента");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("action")]
    public async Task<IActionResult> PerformAction([FromBody] TeacherActionDto action)
    {
        try
        {
            var teacherId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            var result = await _teacherService.PerformTeacherActionAsync(teacherId, action);

            if (!result)
                return BadRequest(new { success = false, error = "Не удалось выполнить действие" });

            return Ok(new
            {
                success = true,
                message = "Действие выполнено успешно"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения действия");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}