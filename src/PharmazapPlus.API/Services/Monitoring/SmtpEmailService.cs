using Microsoft.Extensions.Options;
using PharmazapPlus.API.Models;
using System.Net;
using System.Net.Mail;

namespace PharmazapPlus.API.Services.Monitoring;

public class SmtpEmailService : IEmailService
{
    private readonly EmailConfig _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailConfig> config, ILogger<SmtpEmailService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        if (string.IsNullOrEmpty(_config.SmtpHost) || string.IsNullOrEmpty(_config.Username))
        {
            _logger.LogWarning("Serviço de e-mail não configurado. E-mail para {To} não enviado", to);
            return false;
        }

        try
        {
#pragma warning disable SYSLIB0006 // SmtpClient is obsolete — replace with MailKit in production
            using var client = new SmtpClient(_config.SmtpHost, _config.SmtpPort)
            {
                Credentials = new NetworkCredential(_config.Username, _config.Password),
                EnableSsl = _config.EnableSsl
            };
#pragma warning restore SYSLIB0006

            var from = string.IsNullOrEmpty(_config.FromName)
                ? new MailAddress(_config.FromAddress)
                : new MailAddress(_config.FromAddress, _config.FromName);

            using var message = new MailMessage(from, new MailAddress(to))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("E-mail enviado para {To}: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail para {To}", to);
            return false;
        }
    }
}
