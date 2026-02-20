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


}