using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using SkilllubLearnbox.Utilities;

namespace SkilllubLearnbox.Services;
public class EmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly ConfigHelper _config;

    public EmailService(ILogger<EmailService> logger, ConfigHelper config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task SendPasswordResetEmailAsync(string email, string username, string resetCode)
    {
        try
        {
            _logger.LogInformation("Попытка отправки письма на: {Email}", email);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config.FromName, _config.FromEmail));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = _config.PasswordResetSubject;

            var emailBody = _config.PasswordResetTemplate
                .Replace("{username}", username)
                .Replace("{resetCode}", resetCode);

            message.Body = new TextPart("plain") { Text = emailBody };

            using (var smtpClient = new SmtpClient())
            {
                _logger.LogInformation("Подключение к SMTP...");
                await smtpClient.ConnectAsync(_config.SmtpHost, _config.SmtpPort, SecureSocketOptions.StartTls);

                _logger.LogInformation("Аутентификация...");
                await smtpClient.AuthenticateAsync(_config.FromEmail, _config.AppPassword);

                _logger.LogInformation("Отправка письма...");
                await smtpClient.SendAsync(message);

                await smtpClient.DisconnectAsync(true);

                _logger.LogInformation("Письмо восстановления отправлено на: {Email}", email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки письма на {Email}", email);
            throw;
        }
    }

    public async Task SendWelcomeEmailAsync(string email, string username)
    {
        try
        {
            _logger.LogInformation("Отправка приветственного письма на: {Email}", email);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config.FromName, _config.FromEmail));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = _config.WelcomeSubject;

            var emailBody = _config.WelcomeTemplate.Replace("{username}", username);
            message.Body = new TextPart("plain") { Text = emailBody };

            using (var smtpClient = new SmtpClient())
            {
                await smtpClient.ConnectAsync(_config.SmtpHost, _config.SmtpPort, SecureSocketOptions.StartTls);
                await smtpClient.AuthenticateAsync(_config.FromEmail, _config.AppPassword);
                await smtpClient.SendAsync(message);
                await smtpClient.DisconnectAsync(true);

                _logger.LogInformation("Приветственное письмо отправлено на: {Email}", email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки приветственного письма на {Email}", email);
            throw;
        }
    }
}