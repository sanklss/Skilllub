using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Services;
using SkilllubLearnbox.DTOs;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly ILogger<DebugController> _logger;
    private readonly UserService _userService;

    public DebugController(ILogger<DebugController> logger, UserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    [HttpPost("check-user")]
    public async Task<IActionResult> CheckUser([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Поиск пользователя: {Email}", dto.Email);

            var allUsers = await _userService.GetAllUsersAsync();
            _logger.LogInformation("Всего пользователей в базе: {Count}", allUsers.Count);

            foreach (var user in allUsers)
            {
                _logger.LogInformation("Пользователь: ID={Id}, Email={Email}, Username={Username}",
                    user.Id, user.Email, user.Username);
            }

            var normalizedEmail = dto.Email.ToLower().Trim();
            var foundUsers = allUsers.Where(u => u.Email?.ToLower() == normalizedEmail).ToList();

            _logger.LogInformation("Найдено пользователей с email {Email}: {Count}",
                normalizedEmail, foundUsers.Count);

            return Ok(new
            {
                totalUsers = allUsers.Count,
                foundUsers = foundUsers.Count,
                users = allUsers.Select(u => new { u.Id, u.Email, u.Username }),
                found = foundUsers.Select(u => new { u.Id, u.Email, u.Username })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке пользователей");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("cleanup-broken-users")]
    public async Task<IActionResult> CleanupBrokenUsers()
    {
        try
        {
            _logger.LogInformation("Начинаем очистку битых записей пользователей");

            var deletedCount = await _userService.CleanupBrokenUsersAsync();

            return Ok(new
            {
                success = true,
                deletedCount = deletedCount,
                message = $"Удалено {deletedCount} битых записей"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке битых записей");
            return Problem("Ошибка сервера");
        }
    }
}