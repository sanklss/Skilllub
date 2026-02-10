using SkilllubLearnbox.Models;
using SkilllubLearnbox.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Supabase;

namespace SkilllubLearnbox.Services;
public class RoleService
{
    private readonly ILogger<RoleService> _logger;
    private readonly ConfigHelper _config;
    private Supabase.Client _client;
    private readonly IMemoryCache _cache;

    public RoleService(ILogger<RoleService> logger, ConfigHelper config, IMemoryCache cache)
    {
        _logger = logger;
        _config = config;
        _cache = cache;
        _client = new Supabase.Client(_config.SupabaseUrl, _config.SupabaseKey);
    }

    public async Task AssignUserRoleAsync(string userId, string roleName)
    {
        try
        {
            _logger.LogInformation("Назначаем роль '{RoleName}' пользователю {UserId}", roleName, userId);

            var rolesTable = _client.From<Role>();
            var userRolesTable = _client.From<UserRole>();

            var roleResponse = await rolesTable.Filter("name", Supabase.Postgrest.Constants.Operator.Equals, roleName).Get();
            var roles = roleResponse.Models?.ToList() ?? new List<Role>();

            if (roles.Count == 0)
            {
                _logger.LogWarning("Роль '{RoleName}' не найдена, используем роль 'student' по умолчанию", roleName);

                roleResponse = await rolesTable.Filter("name", Supabase.Postgrest.Constants.Operator.Equals, "student").Get();
                roles = roleResponse.Models?.ToList() ?? new List<Role>();

                if (roles.Count == 0)
                {
                    _logger.LogError("Роль 'student' также не найдена! Создайте роли в базе данных");
                    return;
                }
                roleName = "student";
            }

            var role = roles[0];
            _logger.LogInformation("Найдена роль: {RoleId} - {RoleName}", role.Id, role.Name);

            var existingRolesResponse = await userRolesTable
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Get();

            var existingRoles = existingRolesResponse.Models?.ToList() ?? new List<UserRole>();
            foreach (var existingRole in existingRoles)
            {
                await userRolesTable.Delete(existingRole);
            }

            var newUserRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };

            await userRolesTable.Insert(newUserRole);

            var userRoleCacheKey = $"user_role_{userId}";
            _cache.Remove(userRoleCacheKey);

            _logger.LogInformation("Роль '{RoleName}' успешно назначена пользователю", roleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при назначении роли пользователю {UserId}", userId);
            throw;
        }
    }

    public async Task<string> GetUserRoleAsync(string userId)
    {
        try
        {
            var cacheKey = $"user_role_{userId}";

            if (_cache.TryGetValue(cacheKey, out string cachedRole))
            {
                _logger.LogInformation("Роль пользователя {UserId} загружена из кэша: {Role}", userId, cachedRole);
                return cachedRole;
            }

            _logger.LogInformation("Загрузка роли пользователя {UserId} из базы данных", userId);

            var userRolesTable = _client.From<UserRole>();
            var rolesTable = _client.From<Role>();

            var userRolesResponse = await userRolesTable
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Get();

            var userRoles = userRolesResponse.Models?.ToList() ?? new List<UserRole>();

            if (userRoles.Count == 0)
            {
                _logger.LogWarning("У пользователя {UserId} нет назначенных ролей", userId);
                return "student";
            }

            var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
            var rolesResponse = await rolesTable.Get();
            var allRoles = rolesResponse.Models?.ToList() ?? new List<Role>();

            var userRole = allRoles.FirstOrDefault(r => roleIds.Contains(r.Id));
            var roleName = userRole?.Name ?? "student";

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

            _cache.Set(cacheKey, roleName, cacheOptions);
            _logger.LogInformation("Роль пользователя {UserId} сохранена в кэш: {Role}", userId, roleName);

            return roleName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении роли пользователя {UserId}", userId);
            return "student";
        }
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        try
        {
            var cacheKey = "all_roles";

            if (_cache.TryGetValue(cacheKey, out List<Role> cachedRoles))
            {
                _logger.LogInformation("Роли загружены из кэша");
                return cachedRoles;
            }

            _logger.LogInformation("Загрузка ролей из базы данных");
            await _client.InitializeAsync();

            var rolesTable = _client.From<Role>();
            var response = await rolesTable.Get();
            var roles = response.Models?.ToList() ?? new List<Role>();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

            _cache.Set(cacheKey, roles, cacheOptions);

            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении всех ролей");
            return new List<Role>();
        }
    }

    public async Task<List<UserRole>> GetAllUserRolesAsync()
    {
        try
        {
            await _client.InitializeAsync();
            var userRolesTable = _client.From<UserRole>();
            var response = await userRolesTable.Get();
            return response.Models?.ToList() ?? new List<UserRole>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении всех связей пользователь-роль");
            return new List<UserRole>();
        }
    }

    public async Task InitializeRolesAsync()
    {
        try
        {
            _logger.LogInformation("Инициализация ролей в базе данных");

            var rolesTable = _client.From<Role>();
            var existingRoles = await GetAllRolesAsync();

            var rolesToCreate = new List<Role>();

            if (!existingRoles.Any(r => r.Name == "student"))
            {
                rolesToCreate.Add(new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "student",
                    Description = "Студент - может проходить курсы"
                });
            }

            if (!existingRoles.Any(r => r.Name == "teacher"))
            {
                rolesToCreate.Add(new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "teacher",
                    Description = "Преподаватель - может создавать курсы"
                });
            }

            if (!existingRoles.Any(r => r.Name == "admin"))
            {
                rolesToCreate.Add(new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "admin",
                    Description = "Администратор - полный доступ к системе"
                });
            }

            if (rolesToCreate.Count > 0)
            {
                await rolesTable.Insert(rolesToCreate);

                _cache.Remove("all_roles");

                _logger.LogInformation("Создано ролей: {Count}", rolesToCreate.Count);
            }
            else
            {
                _logger.LogInformation("Роли уже существуют");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации ролей");
            throw;
        }
    }
    public async Task DeleteUserRolesAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var userRolesTable = _client.From<UserRole>();
            var response = await userRolesTable.Get();

            var userRoles = response.Models?.Where(ur => ur.UserId == userId).ToList() ?? new List<UserRole>();

            foreach (var userRole in userRoles)
            {
                await userRolesTable.Delete(userRole);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении ролей пользователя {UserId}", userId);
            throw;
        }
    }
    public async Task<int> CleanupExpiredTokensAsync()
    {
        try
        {
            await _client.InitializeAsync();
            var tokensTable = _client.From<PasswordResetToken>();
            var response = await tokensTable.Get();
            var allTokens = response.Models?.ToList() ?? new List<PasswordResetToken>();

            var expiredTokens = allTokens.Where(t => t.ExpiresAt < DateTime.UtcNow && !t.Used).ToList();

            foreach (var token in expiredTokens)
            {
                token.Used = true;
                await tokensTable.Update(token);
            }

            _logger.LogInformation("Очищено просроченных токенов: {Count}", expiredTokens.Count);
            return expiredTokens.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке токенов");
            throw;
        }
    }
}