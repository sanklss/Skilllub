using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Attributes;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;
using System.Security.Claims;
using static Supabase.Postgrest.Constants;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/achievements")]
[Authorize]
public class AchievementsController : ControllerBase
{
    private readonly ILogger<AchievementsController> _logger;
    private readonly Supabase.Client _client;

    public AchievementsController(ILogger<AchievementsController> logger, Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    [HttpGet("certificates")]
    public async Task<IActionResult> GetUserCertificates()
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            await _client.InitializeAsync();

            var response = await _client
                .From<UserCertificate>()
                .Filter("user_id", Operator.Equals, userId)
                .Order("issued_at", Constants.Ordering.Descending)
                .Get();

            var certificates = response.Models?.Select(c => new CertificateDto
            {
                Id = c.Id,
                UserId = c.UserId,
                CourseId = c.CourseId,
                CertificateNumber = c.CertificateNumber,
                IssuedAt = c.IssuedAt,
                StudentName = c.StudentName,
                CourseName = c.CourseName,
                TeacherName = c.TeacherName, 
                PdfUrl = c.PdfUrl
            }).ToList() ?? new List<CertificateDto>();

            return Ok(new
            {
                success = true,
                certificates
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения сертификатов");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("certificates")]
    public async Task<IActionResult> SaveCertificate([FromBody] CreateCertificateDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            await _client.InitializeAsync();

            var existingResponse = await _client
                .From<UserCertificate>()
                .Filter("user_id", Operator.Equals, userId)
                .Filter("course_id", Operator.Equals, dto.CourseId)
                .Get();

            if (existingResponse.Models?.Any() == true)
            {
                return Ok(new { success = true, message = "Сертификат уже существует" });
            }

            var certificate = new UserCertificate
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CourseId = dto.CourseId,
                CertificateNumber = dto.CertificateNumber,
                IssuedAt = DateTime.UtcNow,
                StudentName = dto.StudentName,
                CourseName = dto.CourseName,
                TeacherName = dto.TeacherName  
            };

            await _client.From<UserCertificate>().Insert(certificate);

            return Ok(new
            {
                success = true,
                certificate = new CertificateDto
                {
                    Id = certificate.Id,
                    UserId = certificate.UserId,
                    CourseId = certificate.CourseId,
                    CertificateNumber = certificate.CertificateNumber,
                    IssuedAt = certificate.IssuedAt,
                    StudentName = certificate.StudentName,
                    CourseName = certificate.CourseName,
                    TeacherName = certificate.TeacherName  
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения сертификата");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpGet("certificates/{certificateId}")]
    public async Task<IActionResult> GetCertificate(string certificateId)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = "Пользователь не авторизован" });

            await _client.InitializeAsync();

            var response = await _client
                .From<UserCertificate>()
                .Filter("id", Operator.Equals, certificateId)
                .Filter("user_id", Operator.Equals, userId)
                .Get();

            var certificate = response.Models?.FirstOrDefault();
            if (certificate == null)
                return NotFound(new { success = false, error = "Сертификат не найден" });

            return Ok(new
            {
                success = true,
                certificate = new CertificateDto
                {
                    Id = certificate.Id,
                    UserId = certificate.UserId,
                    CourseId = certificate.CourseId,
                    CertificateNumber = certificate.CertificateNumber,
                    IssuedAt = certificate.IssuedAt,
                    StudentName = certificate.StudentName,
                    CourseName = certificate.CourseName,
                    TeacherName = certificate.TeacherName,  
                    PdfUrl = certificate.PdfUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения сертификата");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}