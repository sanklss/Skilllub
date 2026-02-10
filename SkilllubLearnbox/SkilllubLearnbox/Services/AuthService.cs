using BCrypt.Net;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Utilities;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Exceptions;
using System.Text.RegularExpressions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SkilllubLearnbox.Services;
public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly ConfigHelper _config;
    private readonly EmailService _emailService;
    private readonly UserService _userService;
    private readonly RoleService _roleService;
    private readonly JwtService _jwtService;
    private readonly TokenService _tokenService;
    private Supabase.Client _client;

    public AuthService(ILogger<AuthService> logger, ConfigHelper config, EmailService emailService,
                      UserService userService, RoleService roleService, JwtService jwtService,
                      TokenService tokenService)
    {
        _logger = logger;
        _config = config;
        _emailService = emailService;
        _userService = userService;
        _roleService = roleService;
        _jwtService = jwtService;
        _tokenService = tokenService;
        _client = new Supabase.Client(_config.SupabaseUrl, _config.SupabaseKey);
    }

    public async Task InitializeDatabaseAsync()
    {
        await _client.InitializeAsync();
    }

    public async Task<AuthResult> LoginAsync(UserLoginDto dto)
    {
        try
        {
            _logger.LogInformation("Попытка входа: {Email}", dto.Email);

            var user = await _userService.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                return new AuthResult { Success = false, Error = "Неверный email или пароль" };
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.Password);
            if (!isPasswordValid)
            {
                return new AuthResult { Success = false, Error = "Неверный email или пароль" };
            }

            await _userService.UpdateLastLoginAsync(user.Id);
            var userRole = await _roleService.GetUserRoleAsync(user.Id);

            var accessToken = _jwtService.GenerateToken(user.Id, user.Email, user.Username, userRole);

            await _tokenService.RevokeUserTokensAsync(user.Id);

            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, null);

            _logger.LogInformation("Успешный вход: {Username}, создан новый refresh токен", user.Username);

            return new AuthResult
            {
                Success = true,
                Token = accessToken,
                RefreshToken = refreshToken.TokenHash,
                Message = $"Добро пожаловать, {user.Username}!",
                User = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    phone = user.Phone,
                    role = userRole
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при входе пользователя");
            return new AuthResult { Success = false, Error = "Ошибка сервера" };
        }
    }

    private async Task<RefreshToken?> GetActiveRefreshTokenAsync(string userId)
    {
        try
        {
            _logger.LogDebug("Поиск активного refresh токена для пользователя: {UserId}", userId);

            await _client.InitializeAsync();

            var response = await _client.From<RefreshToken>()
                .Where(x => x.UserId == userId && x.Revoked == false)
                .Get();

            var activeTokens = response.Models?
            .Where(t => t.ExpiresAt > DateTime.UtcNow).ToList();

            _logger.LogDebug("Найдено активных токенов: {Count}", activeTokens?.Count ?? 0);

            var result = activeTokens?.FirstOrDefault();
            if (result != null)
            {
                _logger.LogDebug("Найден активный токен: {TokenId}, истекает: {ExpiresAt}", result.Id, result.ExpiresAt);
            }
            else
            {
                _logger.LogDebug("Активный токен не найден");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска активного refresh токена для пользователя {UserId}", userId);
            return null;
        }
    }

    public async Task<AuthResult> RegisterAsync(UserRegisterWithRoleDto dto)
    {
        try
        {
            _logger.LogInformation("Регистрация пользователя: {Email}, Роль: {Role}", dto.Email, dto.Role);

            var existingUser = await _userService.GetUserByEmailAsync(dto.Email);
            if (existingUser != null)
                return new AuthResult { Success = false, Error = "Пользователь с таким email уже существует" };

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password, _config.BCryptWorkFactor);

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = dto.Username,
                Email = dto.Email,
                Phone = dto.Phone,
                Password = hashedPassword
            };

            await _userService.CreateUserAsync(newUser);
            _logger.LogInformation("Пользователь зарегистрирован в базе: {Email}", dto.Email);

            try
            {
                await _roleService.AssignUserRoleAsync(newUser.Id, dto.Role);
                _logger.LogInformation("Роль '{Role}' назначена пользователю {Email}", dto.Role, dto.Email);
            }
            catch (Exception roleEx)
            {
                _logger.LogError(roleEx, "Не удалось назначить роль, но пользователь создан");
            }

            try
            {
                _logger.LogInformation("Отправка приветственного письма на: {Email}", dto.Email);
                await _emailService.SendWelcomeEmailAsync(dto.Email, dto.Username);
                _logger.LogInformation("Приветственное письмо отправлено");
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Не удалось отправить приветственное письмо, но пользователь создан");
            }

            var accessToken = _jwtService.GenerateToken(newUser.Id, newUser.Email, newUser.Username, dto.Role);
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(newUser.Id, null);

            return new AuthResult
            {
                Success = true,
                Token = accessToken,
                RefreshToken = refreshToken.TokenHash,
                User = new
                {
                    id = newUser.Id,
                    username = newUser.Username,
                    email = newUser.Email,
                    phone = newUser.Phone,
                    role = dto.Role
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации пользователя");
            return new AuthResult { Success = false, Error = "Ошибка сервера" };
        }
    }

    public async Task<AuthResult> RefreshTokensAsync(string accessToken, string refreshToken)
    {
        try
        {
            _logger.LogInformation("Начало обновления токенов");

            if (!await _tokenService.ValidateRefreshTokenAsync(refreshToken))
            {
                return new AuthResult { Success = false, Error = "Невалидный refresh токен" };
            }

            var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
            if (principal == null)
            {
                return new AuthResult { Success = false, Error = "Невалидный access токен" };
            }

            var userId = principal.FindFirst("userId")?.Value;
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return new AuthResult { Success = false, Error = "Пользователь не найден" };
            }

            var userRole = await _roleService.GetUserRoleAsync(userId);

            var newAccessToken = _jwtService.GenerateToken(userId, user.Email, user.Username, userRole);

            _logger.LogInformation("Access токен обновлен для пользователя {UserId}", userId);

            return new AuthResult
            {
                Success = true,
                Token = newAccessToken,
                RefreshToken = refreshToken,
                User = new
                {
                    id = userId,
                    username = user.Username,
                    email = user.Email,
                    role = userRole
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления токенов");
            return new AuthResult { Success = false, Error = "Ошибка обновления токенов" };
        }
    }

    public async Task<AuthResult> LogoutAsync(string userId, string refreshToken = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _tokenService.RevokeTokenAsync(refreshToken);
            }
            else
            {
                await _tokenService.RevokeUserTokensAsync(userId);
            }

            _logger.LogInformation("Пользователь {UserId} вышел из системы", userId);
            return new AuthResult { Success = true, Message = "Успешный выход" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выходе пользователя {UserId}", userId);
            return new AuthResult { Success = false, Error = "Ошибка при выходе" };
        }
    }

    public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Запрос восстановления пароля для: {Email}", dto.Email);

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return new AuthResult { Success = false, Error = "Email обязателен" };
            }

            var user = await _userService.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                _logger.LogWarning("Пользователь не найден: {Email}", dto.Email);
                return new AuthResult
                {
                    Success = true,
                    Message = "Если пользователь с таким email существует, письмо с кодом отправлено"
                };
            }

            var random = new Random();
            var resetCode = random.Next(100000, 999999).ToString();

            await _userService.CreatePasswordResetTokenAsync(user.Id, resetCode);

            _logger.LogInformation("Отправка письма восстановления на: {Email}", user.Email);

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.Username, resetCode);
                _logger.LogInformation("Письмо восстановления отправлено");
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Ошибка отправки письма восстановления");
            }

            return new AuthResult
            {
                Success = true,
                Message = "Если пользователь с таким email существует, письмо с кодом отправлено"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при запросе восстановления пароля");
            return new AuthResult
            {
                Success = true,
                Message = "Если пользователь с таким email существует, письмо с кодом отправлено"
            };
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Сброс пароля для email: {Email}", dto.Email);

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return new AuthResult { Success = false, Error = "Все поля обязательны" };
            }

            if (dto.NewPassword != dto.ConfirmPassword)
            {
                return new AuthResult { Success = false, Error = "Пароли не совпадают" };
            }

            var passwordRegex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(dto.NewPassword))
            {
                return new AuthResult { Success = false, Error = "Пароль слишком простой (мин. 8 символов, буква и цифра)" };
            }

            var result = await _userService.ResetPasswordAsync(dto.Email, dto.Code, dto.NewPassword);
            return result;
        }
        catch (PostgrestException pgEx)
        {
            _logger.LogError(pgEx, "Ошибка Supabase при сбросе пароля");
            return new AuthResult { Success = false, Error = "Ошибка базы данных" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе пароля");
            return new AuthResult { Success = false, Error = "Ошибка сервера" };
        }
    }

    public async Task<AuthResult> VerifyResetCodeAsync(VerifyResetCodeDto dto)
    {
        try
        {
            _logger.LogInformation("Проверка кода восстановления для: {Email}", dto.Email);

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
            {
                return new AuthResult { Success = false, Error = "Email и код обязательны" };
            }

            var user = await _userService.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                return new AuthResult { Success = false, Error = "Пользователь не найден" };
            }

            await _client.InitializeAsync();
            var tokensTable = _client.From<PasswordResetToken>();
            var tokensResponse = await tokensTable.Get();

            var activeTokens = tokensResponse.Models?
                .Where(t => t.UserId == user.Id && !t.Used && t.ExpiresAt > DateTime.UtcNow)
                .ToList() ?? new List<PasswordResetToken>();

            var correctToken = activeTokens.FirstOrDefault(t => t.Code == dto.Code);

            if (correctToken == null)
            {
                _logger.LogWarning("Неверный код восстановления для пользователя {Email}", dto.Email);

                foreach (var token in activeTokens)
                {
                    token.Attempts++;
                    _logger.LogInformation("Увеличиваем attempts для токена {TokenId}: {Attempts}", token.Id, token.Attempts);

                    if (token.Attempts >= _config.MaxAttempts)
                    {
                        token.Used = true;
                        _logger.LogWarning("Токен {TokenId} заблокирован - превышено количество попыток", token.Id);
                    }

                    await tokensTable.Update(token);
                }

                return new AuthResult { Success = false, Error = "Неверный код восстановления" };
            }


            if (correctToken.Attempts >= _config.MaxAttempts)
            {
                correctToken.Used = true;
                await tokensTable.Update(correctToken);
                return new AuthResult { Success = false, Error = "Превышено количество попыток ввода кода" };
            }

            _logger.LogInformation("Код восстановления верный для пользователя: {Email}", user.Email);

            return new AuthResult
            {
                Success = true,
                Message = "Код верный"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке кода восстановления");
            return new AuthResult { Success = false, Error = "Ошибка сервера" };
        }
    }

    public async Task<AuthResult> ValidateTokenAsync(string token)
    {
        try
        {
            var principal = _jwtService.ValidateToken(token);
            if (principal == null)
            {
                return new AuthResult { Success = false, Error = "Невалидный токен" };
            }

            var userId = principal.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return new AuthResult { Success = false, Error = "Токен не содержит идентификатор пользователя" };
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return new AuthResult { Success = false, Error = "Пользователь не найден" };
            }

            var userRole = await _roleService.GetUserRoleAsync(userId);

            return new AuthResult
            {
                Success = true,
                User = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    phone = user.Phone,
                    role = userRole
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка валидации токена");
            return new AuthResult { Success = false, Error = "Ошибка валидации токена" };
        }
    }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Error { get; set; } = "";
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public object User { get; set; }
}