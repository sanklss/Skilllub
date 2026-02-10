using SkilllubLearnbox.Attributes;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Services;
using SkilllubLearnbox.Utilities;
using Microsoft.AspNetCore.Mvc;
using Supabase;
using System.Text.RegularExpressions;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/admin")]
[AuthorizeRoles("admin", "teacher")]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly RoleService _roleService;
    private readonly UserService _userService;
    private readonly CourseService _courseService;
    private readonly Supabase.Client _client;

    public AdminController(ILogger<AdminController> logger, RoleService roleService,
                         UserService userService, CourseService courseService,
                         Supabase.Client client)
    {
        _logger = logger;
        _roleService = roleService;
        _userService = userService;
        _courseService = courseService;
        _client = client;
    }



    [HttpPost("init-roles")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> InitializeRoles()
    {
        try
        {
            await _roleService.InitializeRolesAsync();
            return Ok(new { success = true, message = "Роли инициализированы" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации ролей");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("check-roles")]
    public async Task<IActionResult> CheckRoles()
    {
        try
        {
            _logger.LogInformation("Проверяем структуру таблицы roles");

            var roles = await _roleService.GetAllRolesAsync();
            _logger.LogInformation("Найдено ролей: {Count}", roles.Count);

            foreach (var role in roles)
            {
                _logger.LogInformation("Роль: ID={Id}, Name={Name}, Description={Description}",
                    role.Id, role.Name, role.Description);
            }

            return Ok(new
            {
                success = true,
                rolesCount = roles.Count,
                roles = roles.Select(r => new { r.Id, r.Name, r.Description })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке таблицы roles");
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("check-user-roles")]
    public async Task<IActionResult> CheckUserRoles()
    {
        try
        {
            _logger.LogInformation("Проверяем таблицу user_roles");

            var userRoles = await _roleService.GetAllUserRolesAsync();
            _logger.LogInformation("Найдено записей в user_roles: {Count}", userRoles.Count);

            foreach (var userRole in userRoles)
            {
                _logger.LogInformation("Связь: UserId={UserId}, RoleId={RoleId}",
                    userRole.UserId, userRole.RoleId);
            }

            return Ok(new
            {
                success = true,
                userRolesCount = userRoles.Count,
                userRoles = userRoles.Select(ur => new { ur.UserId, ur.RoleId })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке таблицы user_roles");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("add-test-user-role")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> AddTestUserRole()
    {
        try
        {
            _logger.LogInformation("Добавляем тестовую связь пользователь-роль");

            var users = await _userService.GetAllUsersAsync();
            if (users.Count == 0)
            {
                return BadRequest(new { success = false, error = "Нет пользователей в базе" });
            }

            var user = users[0];
            var roles = await _roleService.GetAllRolesAsync();
            var studentRole = roles.FirstOrDefault(r => r.Name == "student");

            if (studentRole == null)
            {
                return BadRequest(new { success = false, error = "Роль student не найдена" });
            }

            await _roleService.AssignUserRoleAsync(user.Id, "student");

            _logger.LogInformation("Тестовая связь добавлена: UserId={UserId}, RoleId={RoleId}", user.Id, studentRole.Id);

            return Ok(new
            {
                success = true,
                message = "Тестовая связь добавлена",
                userRole = new { UserId = user.Id, RoleId = studentRole.Id }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении тестовой связи");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("cleanup-expired-tokens")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> CleanupExpiredTokens()
    {
        try
        {
            _logger.LogInformation("Очистка просроченных токенов");

            var cleanedCount = await _roleService.CleanupExpiredTokensAsync();

            return Ok(new
            {
                success = true,
                cleanedCount = cleanedCount,
                message = $"Очищено {cleanedCount} просроченных токенов"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке токенов");
            return Problem("Ошибка сервера");
        }
    }


    [HttpGet("users")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            _logger.LogInformation("Получение списка всех пользователей");

            var users = await _userService.GetAllUsersAsync();
            var userRoles = await _roleService.GetAllUserRolesAsync();
            var roles = await _roleService.GetAllRolesAsync();

            var result = users.Select(user => new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                phone = user.Phone,
                lastLogin = user.LastLogin,
                role = roles.FirstOrDefault(r =>
                    userRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == r.Id))?.Name ?? "student"
            }).ToList();

            return Ok(new
            {
                success = true,
                users = result,
                totalCount = result.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка пользователей");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPut("users/{userId}/role")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> UpdateUserRole(string userId, [FromBody] UpdateUserRoleDto dto)
    {
        try
        {
            _logger.LogInformation("Обновление роли пользователя {UserId} на {Role}", userId, dto.Role);

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            await _roleService.AssignUserRoleAsync(userId, dto.Role);

            return Ok(new
            {
                success = true,
                message = $"Роль пользователя обновлена на '{dto.Role}'"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении роли пользователя {UserId}", userId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpGet("statistics")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            _logger.LogInformation("Получение статистики платформы");

            var users = await _userService.GetAllUsersAsync();
            var roles = await _roleService.GetAllRolesAsync();
            var userRoles = await _roleService.GetAllUserRolesAsync();

            var regularUsers = users.Where(u =>
                !userRoles.Any(ur => ur.UserId == u.Id &&
                roles.Any(r => r.Id == ur.RoleId && r.Name == "admin"))
            ).ToList();

            var userStats = regularUsers.GroupBy(u =>
                roles.FirstOrDefault(r => userRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))?.Name ?? "student"
            ).ToDictionary(g => g.Key, g => g.Count());

            var activeToday = regularUsers.Count(u => u.LastLogin?.Date == DateTime.UtcNow.Date);
            var newThisWeek = regularUsers.Count(u =>
                u.LastLogin >= DateTime.UtcNow.AddDays(-7) ||
                (u.LastLogin == null && DateTime.UtcNow.AddDays(-7) <= DateTime.UtcNow)
            );

            return Ok(new
            {
                success = true,
                statistics = new
                {
                    totalUsers = regularUsers.Count,
                    usersByRole = userStats,
                    activeToday = activeToday,
                    newThisWeek = newThisWeek,
                    adminCount = users.Count - regularUsers.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики");
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("courses")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
    {
        try
        {
            _logger.LogInformation("Создание нового курса: {Title}", dto.Title);

            var course = new Course
            {
                Id = Guid.NewGuid().ToString(),
                Title = dto.Title,
                Description = dto.Description,
                DifficultyLevel = dto.DifficultyLevel,
                IsPublished = dto.IsPublished,
                CreatedBy = User.FindFirst("userId")?.Value
            };

            return Ok(new
            {
                success = true,
                message = "Курс создан успешно",
                course = new { course.Id, course.Title }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании курса");
            return Problem("Ошибка сервера");
        }
    }

    [HttpDelete("users/{userId}")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        try
        {
            _logger.LogInformation("Начало удаления пользователя {UserId}", userId);

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            _logger.LogInformation("Пользователь найден: {Username} ({Email})", user.Username, user.Email);

            _logger.LogInformation("1. Удаление refresh токенов пользователя {UserId}", userId);
            await DeleteUserRefreshTokensAsync(userId);

            _logger.LogInformation("2. Удаление токенов сброса пароля пользователя {UserId}", userId);
            await DeleteUserResetTokensAsync(userId);

            _logger.LogInformation("3. Удаление ролей пользователя {UserId}", userId);
            await _roleService.DeleteUserRolesAsync(userId);

            _logger.LogInformation("4. Удаление пользователя {UserId} из базы", userId);
            await _userService.DeleteUserAsync(userId);

            _logger.LogInformation("Пользователь {UserId} успешно удален", userId);

            return Ok(new
            {
                success = true,
                message = "Пользователь удален успешно"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при удалении пользователя {UserId}", userId);
            return StatusCode(500, new { success = false, error = "Ошибка сервера при удалении пользователя" });
        }
    }

    private async Task DeleteUserRefreshTokensAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var refreshTokensTable = _client.From<RefreshToken>();
            var response = await refreshTokensTable.Get();

            var userTokens = response.Models?.Where(rt => rt.UserId == userId).ToList() ?? new List<RefreshToken>();

            _logger.LogInformation("Найдено refresh токенов для удаления: {Count}", userTokens.Count);

            foreach (var token in userTokens)
            {
                await refreshTokensTable.Delete(token);
                _logger.LogDebug("Удален refresh токен: {TokenId}", token.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении refresh токенов пользователя {UserId}", userId);
            throw;
        }
    }

    private async Task DeleteUserResetTokensAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var resetTokensTable = _client.From<PasswordResetToken>();
            var response = await resetTokensTable.Get();

            var userTokens = response.Models?.Where(t => t.UserId == userId).ToList() ?? new List<PasswordResetToken>();

            foreach (var token in userTokens)
            {
                await resetTokensTable.Delete(token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении токенов сброса пароля пользователя {UserId}", userId);
            throw;
        }
    }

    [HttpPut("users/{userId}")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto dto)
    {
        try
        {
            _logger.LogInformation("Обновление пользователя {UserId}", userId);

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            var result = await _userService.UpdateUserAsync(userId, dto);
            if (!result)
            {
                return BadRequest(new { success = false, error = "Не удалось обновить пользователя. Возможно, email уже занят." });
            }

            return Ok(new
            {
                success = true,
                message = "Пользователь успешно обновлен"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пользователя {UserId}", userId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpPut("users/{userId}/password")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> UpdateUserPassword(string userId, [FromBody] UpdateUserPasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Обновление пароля пользователя {UserId}", userId);

            if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword != dto.ConfirmPassword)
            {
                return BadRequest(new { success = false, error = "Пароли не совпадают или пусты" });
            }

            var passwordRegex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(dto.NewPassword))
            {
                return BadRequest(new { success = false, error = "Пароль слишком простой (мин. 8 символов, буква и цифра)" });
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            var result = await _userService.UpdateUserPasswordAsync(userId, dto.NewPassword);

            if (!result)
            {
                return BadRequest(new { success = false, error = "Не удалось обновить пароль" });
            }

            _logger.LogInformation("Пароль пользователя {UserId} успешно обновлен", userId);

            return Ok(new
            {
                success = true,
                message = "Пароль пользователя успешно обновлен"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пароля пользователя {UserId}", userId);
            return Problem("Ошибка сервера");
        }
    }

    [HttpPost("users")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        try
        {
            _logger.LogInformation("Создание нового пользователя: {Email}", dto.Email);

            if (string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest(new { success = false, error = "Все обязательные поля должны быть заполнены" });
            }

            var passwordRegex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(dto.Password))
            {
                return BadRequest(new { success = false, error = "Пароль слишком простой (мин. 8 символов, буква и цифра)" });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = dto.Username,
                Email = dto.Email,
                Phone = dto.Phone,
                Password = hashedPassword
            };

            await _userService.CreateUserAsync(user);

            await _roleService.AssignUserRoleAsync(user.Id, dto.Role);

            _logger.LogInformation("Пользователь успешно создан: {Email}", dto.Email);

            return Ok(new
            {
                success = true,
                message = "Пользователь успешно создан",
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    phone = user.Phone,
                    role = dto.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пользователя!");
            return Problem("Ошибка сервера");
        }
    }


    [HttpPost("users/{userId}/revoke-sessions")]
    [AuthorizeRoles("admin")]
    public async Task<IActionResult> RevokeUserSessions(string userId)
    {
        try
        {
            _logger.LogInformation("Принудительное завершение сессий пользователя {UserId}", userId);

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Пользователь не найден" });
            }

            await DeleteUserRefreshTokensAsync(userId);

            _ = Task.Run(async () =>
            {
                await Task.Delay(1000); 
                await SessionEventsController.NotifyUserSessionRevoked(userId);
            });

            _logger.LogInformation("Все сессии пользователя {UserId} завершены", userId);

            return Ok(new
            {
                success = true,
                message = $"Все сессии пользователя {user.Username} завершены"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении сессий пользователя {UserId}", userId);
            return Problem("Ошибка сервера");
        }
    }



    public class UpdateUserRoleDto
    {
        public string Role { get; set; } = "";
    }

    public class CreateCourseDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string DifficultyLevel { get; set; } = "beginner";
        public bool IsPublished { get; set; } = false;
    }
}