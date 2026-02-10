using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Services;
using System.Text.RegularExpressions;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AuthService _authService;
    private readonly UserService _userService;
    private readonly TokenService _tokenService;

    public AuthController(ILogger<AuthController> logger, AuthService authService, UserService userService, TokenService tokenService)
    {
        _logger = logger;
        _authService = authService;
        _userService = userService;
        _tokenService = tokenService;
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Смена пароля для авторизованного пользователя");

            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, error = "Пользователь не найден" });
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new { success = false, error = "Пользователь не найден" });
            }

            bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.Password);
            if (!isCurrentPasswordValid)
            {
                return BadRequest(new { success = false, error = "Текущий пароль неверен" });
            }

            if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword != dto.ConfirmPassword)
            {
                return BadRequest(new { success = false, error = "Пароли не совпадают" });
            }

            var passwordRegex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(dto.NewPassword))
            {
                return BadRequest(new { success = false, error = "Пароль слишком простой (мин. 8 символов, буква и цифра)" });
            }

            var result = await _userService.UpdateUserPasswordAsync(userId, dto.NewPassword);

            if (!result)
            {
                return BadRequest(new { success = false, error = "Не удалось обновить пароль" });
            }

            _logger.LogInformation("Пароль успешно изменен для пользователя: {Email}", user.Email);

            return Ok(new
            {
                success = true,
                message = "Пароль успешно изменен"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при смене пароля");
            return BadRequest(new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegisterWithRoleDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    token = result.Token,
                    refreshToken = result.RefreshToken,
                    user = result.User
                });
            }
            else
            {
                return BadRequest(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации пользователя");
            return BadRequest(new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    token = result.Token,
                    refreshToken = result.RefreshToken,
                    user = result.User
                });
            }
            else
            {
                return Unauthorized(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при входе пользователя");
            return BadRequest(new { success = false, error = "Ошибка сервера при авторизации" });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        try
        {
            var result = await _authService.RefreshTokensAsync(dto.AccessToken, dto.RefreshToken);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    token = result.Token,
                    refreshToken = result.RefreshToken,
                    user = result.User
                });
            }
            else
            {
                return Unauthorized(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении токена");
            return Unauthorized(new { success = false, error = "Ошибка обновления токена" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto)
    {
        try
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, error = "Невалидный пользователь" });
            }

            var result = await _authService.LogoutAsync(userId, dto.RefreshToken);

            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message });
            }
            else
            {
                return BadRequest(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выходе");
            return BadRequest(new { success = false, error = "Ошибка при выходе" });
        }
    }

    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequestDto dto)
    {
        try
        {
            var success = await _tokenService.RevokeTokenAsync(dto.RefreshToken);

            if (success)
            {
                return Ok(new { success = true, message = "Токен отозван" });
            }
            else
            {
                return BadRequest(new { success = false, error = "Токен не найден" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отзыве токена");
            return BadRequest(new { success = false, error = "Ошибка при отзыве токена" });
        }
    }

    [HttpPost("validate-token")]
    public async Task<IActionResult> ValidateToken()
    {
        try
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, error = "Токен не предоставлен" });
            }

            var result = await _authService.ValidateTokenAsync(token);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    user = result.User
                });
            }
            else
            {
                return Unauthorized(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при валидации токена");
            return Unauthorized(new { success = false, error = "Ошибка валидации токена" });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            var result = await _authService.ForgotPasswordAsync(dto);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message
                });
            }
            else
            {
                return BadRequest(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при запросе восстановления пароля");
            return Ok(new
            {
                success = true,
                message = "Если пользователь с таким email существует, письмо с кодом отправлено"
            });
        }
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
    {
        try
        {
            _logger.LogInformation("Проверка кода восстановления для: {Email}", dto.Email);

            var result = await _authService.VerifyResetCodeAsync(dto);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message
                });
            }
            else
            {
                return BadRequest(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода восстановления");
            return BadRequest(new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(dto);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message
                });
            }
            else
            {
                return BadRequest(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе пароля");
            return BadRequest(new { success = false, error = "Ошибка сервера" });
        }
    }

    [HttpPost("test-new-password")]
    public async Task<IActionResult> TestNewPassword([FromBody] UserLoginDto dto)
    {
        try
        {
            _logger.LogInformation("Тестирование нового пароля для: {Email}:", dto.Email);

            var user = await _userService.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized(new { success = false, error = "Пользователь не найден" });
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);
            _logger.LogInformation("Результат проверки пароля: {IsValid}", isPasswordValid);

            return Ok(new
            {
                success = isPasswordValid,
                message = isPasswordValid ? "Пароль верный!" : "Пароль неверный",
                isPasswordCorrect = isPasswordValid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при тестировании пароля");
            return BadRequest(new { success = false, error = "Ошибка сервера" });
        }
    }
}