using BCrypt.Net;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Utilities;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Exceptions;

namespace SkilllubLearnbox.Services;
public class UserService
{
    private readonly ILogger<UserService> _logger;
    private readonly ConfigHelper _config;
    private Supabase.Client _client;

    public UserService(ILogger<UserService> logger, ConfigHelper config)
    {
        _logger = logger;
        _config = config;
        _client = new Supabase.Client(_config.SupabaseUrl, _config.SupabaseKey);
    }

    public async Task<User> GetUserByEmailAsync(string email)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();
            var response = await usersTable.Get();
            var users = response.Models?.Where(u => u.Email?.ToLower() == email.ToLower()).ToList();

            return users?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пользователя по email: {Email}", email);
            return null;
        }
    }

    public async Task<User> GetUserByIdAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();
            var response = await usersTable.Get();
            var users = response.Models?.Where(u => u.Id == userId).ToList();

            return users?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пользователя по ID: {UserId}", userId);
            return null;
        }
    }

    public async Task CreateUserAsync(User user)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();
            await usersTable.Insert(user);
            _logger.LogInformation("Пользователь создан: {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пользователя: {Email}", user.Email);
            throw;
        }
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                user.LastLogin = DateTime.UtcNow;
                await _client.From<User>().Update(user);
                _logger.LogInformation("Поле last_login обновлено для пользователя: {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить last_login для пользователя {UserId}", userId);
        }
    }

    public async Task CreatePasswordResetTokenAsync(string userId, string resetCode)
    {
        try
        {
            await _client.InitializeAsync();
            var tokensTable = _client.From<PasswordResetToken>();

            var oldTokensResponse = await tokensTable.Get();
            var oldTokens = oldTokensResponse.Models?
                .Where(t => t.UserId == userId && !t.Used)
                .ToList() ?? new List<PasswordResetToken>();

            foreach (var oldToken in oldTokens)
            {
                oldToken.Used = true;
                await tokensTable.Update(oldToken);
            }

            var newToken = new PasswordResetToken
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Code = resetCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(_config.CodeExpirationHours),
                Used = false,
                Attempts = 0
            };

            await tokensTable.Insert(newToken);
            _logger.LogInformation("Код восстановления сохранен в БД для пользователя {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании токена сброса пароля для пользователя {UserId}", userId);
            throw;
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();
            var tokensTable = _client.From<PasswordResetToken>();

            var user = await GetUserByEmailAsync(email);
            if (user == null)
            {
                return new AuthResult { Success = false, Error = "Пользователь не найден" };
            }

            var tokensResponse = await tokensTable.Get();
            var validTokens = tokensResponse.Models?
                .Where(t => t.UserId == user.Id && t.Code == code && !t.Used)
                .ToList() ?? new List<PasswordResetToken>();

            if (validTokens.Count == 0)
            {
                return new AuthResult { Success = false, Error = "Код не найден или уже использован" };
            }

            var resetToken = validTokens[0];

            if (resetToken.ExpiresAt < DateTime.UtcNow)
            {
                resetToken.Used = true;
                await tokensTable.Update(resetToken);
                return new AuthResult { Success = false, Error = "Срок действия кода истек" };
            }

            if (resetToken.Attempts >= _config.MaxAttempts)
            {
                resetToken.Used = true;
                await tokensTable.Update(resetToken);
                return new AuthResult { Success = false, Error = "Превышено количество попыток ввода кода" };
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Password = hashedPassword;
            await usersTable.Update(user);

            resetToken.Used = true;
            await tokensTable.Update(resetToken);

            _logger.LogInformation("Пароль успешно сброшен для пользователя: {Email}", user.Email);

            return new AuthResult
            {
                Success = true,
                Message = "Пароль успешно изменен. Теперь вы можете войти с новым паролем."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе пароля для email: {Email}", email);
            throw;
        }
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();
            var response = await usersTable.Get();
            return response.Models?.ToList() ?? new List<User>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении всех пользователей");
            return new List<User>();
        }
    }

    public async Task DeleteUserResetTokensAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var tokensTable = _client.From<PasswordResetToken>();
            var response = await tokensTable.Get();

            var userTokens = response.Models?.Where(t => t.UserId == userId).ToList() ?? new List<PasswordResetToken>();

            foreach (var token in userTokens)
            {
                await tokensTable.Delete(token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении токенов сброса пароля пользователя {UserId}", userId);
            throw;
        }
    }
    private async Task DeleteUserRolesDirectlyAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();

            var userRolesTable = _client.From<UserRole>();
            var response = await userRolesTable.Get();
            var userRoles = response.Models?.Where(ur => ur.UserId == userId).ToList();

            _logger.LogInformation("Найдено ролей для удаления: {Count}", userRoles?.Count ?? 0);

            foreach (var userRole in userRoles ?? new List<UserRole>())
            {
                await userRolesTable.Delete(userRole);
                _logger.LogDebug("Удалена роль: UserId={UserId}, RoleId={RoleId}", userRole.UserId, userRole.RoleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении ролей пользователя {UserId}", userId);
            throw;
        }
    }

    public async Task DeleteUserAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();

            var user = await GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Пользователь {UserId} не найден для удаления", userId);
                return;
            }

            await _client.From<User>().Delete(user);
            _logger.LogInformation("Пользователь {UserId} удален из базы", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пользователя {UserId} из базы", userId);
            throw;
        }
    }

    public async Task<bool> UpdateUserAsync(string userId, UpdateUserDto dto)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();

            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return false;

            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                var existingUser = await GetUserByEmailAsync(dto.Email);
                if (existingUser != null && existingUser.Id != userId)
                    return false;
            }

            user.Username = dto.Username ?? user.Username;
            user.Email = dto.Email ?? user.Email;
            user.Phone = dto.Phone ?? user.Phone;

            await usersTable.Update(user);
            _logger.LogInformation("Пользователь {UserId} обновлен", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пользователя {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UpdateUserPasswordAsync(string userId, string newPassword)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();

            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return false;

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Password = hashedPassword;

            await usersTable.Update(user);
            _logger.LogInformation("Пароль пользователя {UserId} обновлен", userId);

            _logger.LogDebug("Новый хэш пароля для пользователя {UserId}: {PasswordHash}", userId, user.Password);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пароля пользователя {UserId}", userId);
            return false;
        }
    }


    public async Task<User?> CreateUserAsync(CreateUserDto dto)
    {
        try
        {
            await _client.InitializeAsync();
            var usersTable = _client.From<User>();

            var existingUser = await GetUserByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Пользователь с email {Email} уже существует", dto.Email);
                return null;
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = dto.Username,
                Email = dto.Email,
                Phone = dto.Phone,
                Password = hashedPassword,
                LastLogin = null
            };

            await usersTable.Insert(newUser);
            _logger.LogInformation("Пользователь создан: {Email}", dto.Email);

            _logger.LogDebug("Хэш пароля для нового пользователя {Email}: {PasswordHash}", dto.Email, newUser.Password);

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пользователя: {Email}", dto.Email);
            return null;
        }
    }

    public async Task<int> CleanupBrokenUsersAsync()
    {
        try
        {
            var allUsers = await GetAllUsersAsync();
            var brokenUsers = allUsers.Where(u =>
                string.IsNullOrEmpty(u.Email) ||
                string.IsNullOrEmpty(u.Username) ||
                string.IsNullOrEmpty(u.Password)).ToList();

            var usersTable = _client.From<User>();
            foreach (var brokenUser in brokenUsers)
            {
                await usersTable.Delete(brokenUser);
            }

            _logger.LogInformation("Очистка завершена. Удалено записей: {Count}", brokenUsers.Count);
            return brokenUsers.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке битых записей");
            throw;
        }
    }
}