namespace SkilllubLearnbox.Utilities;

public class ConfigHelper
{
    private readonly IConfiguration _configuration;

    public ConfigHelper(IConfiguration configuration)
    {
        _configuration = configuration;

        CheckRequiredEnvironmentVariables();
    }

    private void CheckRequiredEnvironmentVariables()
    {
        var requiredVars = new[] { "JWT_SECRET", "SUPABASE_URL", "SUPABASE_KEY" };

        foreach (var varName in requiredVars)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
            {
                throw new InvalidOperationException(
                    $"Необходимо установить переменную окружения: {varName}. " +
                    $"Добавь ее в файл .env в корне проекта");
            }
        }
    }

    public string JwtSecret => Environment.GetEnvironmentVariable("JWT_SECRET")!;
    public string SupabaseUrl => Environment.GetEnvironmentVariable("SUPABASE_URL")!;
    public string SupabaseKey => Environment.GetEnvironmentVariable("SUPABASE_KEY")!;

    public string SmtpHost => Environment.GetEnvironmentVariable("SMTP_HOST") ?? _configuration["EmailSettings:SmtpHost"]!;
    public int SmtpPort => int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? _configuration["EmailSettings:Port"]!);
    public string FromEmail => Environment.GetEnvironmentVariable("FROM_EMAIL") ?? _configuration["EmailSettings:FromEmail"]!;
    public string AppPassword => Environment.GetEnvironmentVariable("APP_PASSWORD") ?? _configuration["EmailSettings:AppPassword"]!;

    public string FromName => _configuration["EmailSettings:FromName"]!;
    public int CodeExpirationHours => int.Parse(_configuration["PasswordReset:CodeExpirationHours"]!);
    public int MaxAttempts => int.Parse(_configuration["PasswordReset:MaxAttempts"]!);
    public string JwtIssuer => _configuration["JwtSettings:Issuer"]!;
    public string JwtAudience => _configuration["JwtSettings:Audience"]!;
    public int JwtExpirationMinutes => int.Parse(_configuration["JwtSettings:ExpirationMinutes"]!);
    public int RefreshTokenExpirationDays => int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");
    public int RefreshTokenLength => int.Parse(_configuration["JwtSettings:RefreshTokenLength"] ?? "64");
    public string[] AllowedOrigins => _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!;
    public string[] AllowedMethods => _configuration.GetSection("Cors:AllowedMethods").Get<string[]>()!;
    public string[] AllowedHeaders => _configuration.GetSection("Cors:AllowedHeaders").Get<string[]>()!;
    public string ApplicationName => _configuration["Application:Name"]!;
    public string ApplicationVersion => _configuration["Application:Version"]!;
    public string SupportEmail => _configuration["Application:SupportEmail"]!;
    public string Website => _configuration["Application:Website"]!;
    public int BCryptWorkFactor => int.Parse(_configuration["Security:BCryptWorkFactor"]!);
    public string WelcomeSubject => _configuration["EmailTemplates:Welcome:Subject"]!;
    public string WelcomeTemplate => _configuration["EmailTemplates:Welcome:Template"]!;
    public string PasswordResetSubject => _configuration["EmailTemplates:PasswordReset:Subject"]!;
    public string PasswordResetTemplate => _configuration["EmailTemplates:PasswordReset:Template"]!;
}