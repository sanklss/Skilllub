using System.Security.Cryptography;
using System.Text;
using SkilllubLearnbox.Models;
using SkilllubLearnbox.Utilities;
using Microsoft.Extensions.Logging;
using Supabase;
namespace SkilllubLearnbox.Services;
public class TokenService
{
    private readonly ILogger<TokenService> _logger;
    private readonly ConfigHelper _config;
    private readonly Supabase.Client _client;

    public TokenService(ILogger<TokenService> logger, ConfigHelper config, Supabase.Client client)
    {
        _logger = logger;
        _config = config;
        _client = client;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string HashToken(string token)
    {
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha512.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(string userId, string oldTokenToReplace = null)
    {
        try
        {
            _logger.LogDebug("CreateRefreshTokenAsync начат для пользователя {UserId}", userId);

            await _client.InitializeAsync();

            var refreshToken = GenerateRefreshToken();
            var hashedToken = HashToken(refreshToken);

            _logger.LogDebug("Новый токен создан: {TokenPrefix}...", refreshToken.Substring(0, 20));

            if (!string.IsNullOrEmpty(oldTokenToReplace))
            {
                _logger.LogDebug("Отзываем явно указанный старый токен");
                await RevokeTokenAsync(oldTokenToReplace);
            }

            var newToken = new RefreshToken
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TokenHash = hashedToken,
                ExpiresAt = DateTime.UtcNow.AddDays(_config.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };

            var response = await _client.From<RefreshToken>().Insert(newToken);

            if (response.Models?.Count > 0)
            {
                _logger.LogDebug("Токен успешно сохранен в БД");

                return new RefreshToken
                {
                    Id = newToken.Id,
                    UserId = newToken.UserId,
                    TokenHash = refreshToken,
                    ExpiresAt = newToken.ExpiresAt,
                    CreatedAt = newToken.CreatedAt,
                    Revoked = newToken.Revoked
                };
            }
            else
            {
                throw new Exception("Не удалось сохранить refresh токен в базу данных");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания refresh токена для пользователя {UserId}", userId);
            throw;
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
                .Where(t => t.ExpiresAt > DateTime.UtcNow)
                .ToList();

            _logger.LogDebug("Найдено активных токенов: {Count}", activeTokens?.Count ?? 0);

            var result = activeTokens?.FirstOrDefault();
            if (result != null)
            {
                _logger.LogDebug("Найден активный токен: {TokenId}", result.Id);
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

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        try
        {
            _logger.LogDebug("GetRefreshTokenAsync начат для токена: {TokenPrefix}...", token?.Substring(0, 20));

            await _client.InitializeAsync();
            var hashedToken = HashToken(token);

            var response = await _client.From<RefreshToken>()
                .Filter("token_hash", Supabase.Postgrest.Constants.Operator.Equals, hashedToken)
                .Get();

            _logger.LogDebug("Найдено токенов: {Count}", response.Models?.Count);

            var result = response.Models?.FirstOrDefault();
            if (result != null)
            {
                _logger.LogDebug("Токен найден: {TokenId}", result.Id);
            }
            else
            {
                _logger.LogDebug("Токен не найден");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения токена");
            return null;
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(string token)
    {
        var storedToken = await GetRefreshTokenAsync(token);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh токен не найден");
            return false;
        }

        if (storedToken.Revoked)
        {
            _logger.LogWarning("Refresh токен отозван");
            return false;
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh токен истек");
            return false;
        }

        return true;
    }

    public async Task<bool> RevokeTokenAsync(string token)
    {
        try
        {
            _logger.LogDebug("RevokeTokenAsync начат для токена: {TokenPrefix}...", token?.Substring(0, 20));

            var storedToken = await GetRefreshTokenAsync(token);
            if (storedToken == null)
            {
                _logger.LogWarning("Токен для отзыва не найден в БД");
                return false;
            }

            _logger.LogDebug("Найден токен для отзыва: {TokenId}, текущий revoked: {Revoked}", storedToken.Id, storedToken.Revoked);

            storedToken.Revoked = true;

            var response = await _client.From<RefreshToken>().Update(storedToken);

            if (response.Models?.Count > 0)
            {
                _logger.LogDebug("Токен успешно отозван");
                return true;
            }
            else
            {
                _logger.LogWarning("Токен не отозван, Models count: {Count}", response.Models?.Count);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отзыва токена");
            return false;
        }
    }

    public async Task RevokeUserTokensAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Отзываем все токены пользователя {UserId}", userId);

            var response = await _client.From<RefreshToken>()
                .Where(x => x.UserId == userId && x.Revoked == false)
                .Get();

            var activeTokens = response.Models?.ToList() ?? new List<RefreshToken>();

            _logger.LogInformation("Найдено активных токенов для отзыва: {Count}", activeTokens.Count);

            foreach (var token in activeTokens)
            {
                token.Revoked = true;
                await _client.From<RefreshToken>().Update(token);
                _logger.LogDebug("Токен {TokenId} отозван", token.Id);
            }

            _logger.LogInformation("Отозвано {Count} токенов пользователя {UserId}", activeTokens.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отзыва токенов пользователя {UserId}", userId);
            throw;
        }
    }
}