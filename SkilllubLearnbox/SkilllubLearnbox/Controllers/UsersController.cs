using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly Supabase.Client _client;

    public UsersController(ILogger<UsersController> logger, Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        try
        {
            _logger.LogInformation("Получение пользователя с ID: {UserId}", userId);

            await _client.InitializeAsync();

            var response = await _client
                .From<User>()
                .Filter("id", Operator.Equals, userId)
                .Get();

            var user = response.Models?.FirstOrDefault();

            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            return Ok(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения пользователя {UserId}", userId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}