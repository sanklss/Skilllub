using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using SkilllubLearnbox.Utilities;


namespace SkilllubLearnbox.Services;
public class JwtService
{
    private readonly ConfigHelper _config;
    private readonly ILogger<JwtService> _logger;

    public JwtService(ConfigHelper config, ILogger<JwtService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string GenerateToken(string userId, string email, string username, string role, int? customExpirationMinutes = null)
    {
        try
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));
            if (string.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
            if (string.IsNullOrEmpty(role)) throw new ArgumentNullException(nameof(role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.JwtSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expirationMinutes = customExpirationMinutes ?? _config.JwtExpirationMinutes;

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("userId", userId),
            new Claim("tokenType", "access")
        };

            var token = new JwtSecurityToken(
                issuer: _config.JwtIssuer,
                audience: _config.JwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации JWT токена для пользователя {UserId}", userId);
            throw;
        }
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config.JwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _config.JwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка валидации JWT токена");
            return null;
        }
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        try
        {
            _logger.LogDebug("Извлечение данных из просроченного токена");

            var tokenHandler = new JwtSecurityTokenHandler();

            if (!tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Токен невозможно прочитать");
                return null;
            }

            var key = Encoding.UTF8.GetBytes(_config.JwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _config.JwtAudience,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            _logger.LogDebug("Данные из токена успешно извлечены");
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка извлечения данных из просроченного токена");
            return null;
        }
    }

    public string GetUserIdFromToken(string token)
    {
        try
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst("userId")?.Value;
        }
        catch
        {
            return null;
        }
    }

    public string GetUserRoleFromToken(string token)
    {
        try
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(ClaimTypes.Role)?.Value;
        }
        catch
        {
            return null;
        }
    }
}