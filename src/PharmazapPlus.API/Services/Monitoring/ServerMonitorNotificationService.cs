using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Models;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Services.Monitoring;

public class ServerMonitorNotificationService : IServerMonitorNotificationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmailService _emailService;
    private readonly HealthMonitorConfig _config;
    private readonly ILogger<ServerMonitorNotificationService> _logger;

    public ServerMonitorNotificationService(
        IServiceProvider serviceProvider,
        IEmailService emailService,
        IOptions<HealthMonitorConfig> config,
        ILogger<ServerMonitorNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _emailService = emailService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task NotifyServerDown(string serverName, IEnumerable<string> failedChecks, DateTime detectedAt)
    {
        var failedCheckList = failedChecks.ToList();
        var checksText = failedCheckList.Count > 0
            ? string.Join(", ", failedCheckList)
            : "desconhecido";

        var whatsAppMessage =
            $"🚨 *ALERTA: Servidor Fora do Ar*\n\n" +
            $"Servidor: {serverName}\n" +
            $"Hora: {detectedAt:dd/MM/yyyy HH:mm:ss} UTC\n" +
            $"Verificações com falha: {checksText}\n\n" +
            $"Verifique imediatamente o servidor de produção.";

        var emailSubject = $"🚨 ALERTA: {serverName} — Servidor Fora do Ar";
        var emailBody =
            $"ALERTA DE SERVIDOR\n\n" +
            $"O servidor '{serverName}' está com problemas.\n\n" +
            $"Hora da detecção: {detectedAt:dd/MM/yyyy HH:mm:ss} UTC\n" +
            $"Verificações com falha: {checksText}\n\n" +
            $"Por favor, verifique imediatamente o servidor de produção.\n\n" +
            $"-- PharmazapPlus Monitor";

        await SendNotificationsToAdmins(whatsAppMessage, emailSubject, emailBody);
    }

    public async Task NotifyServerRecovered(string serverName, TimeSpan downtime)
    {
        var downtimeText = FormatDowntime(downtime);

        var whatsAppMessage =
            $"✅ *RECUPERADO: Servidor Online*\n\n" +
            $"Servidor: {serverName}\n" +
            $"Hora da recuperação: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC\n" +
            $"Tempo de indisponibilidade: {downtimeText}";

        var emailSubject = $"✅ RECUPERADO: {serverName} — Servidor Online";
        var emailBody =
            $"RECUPERAÇÃO DE SERVIDOR\n\n" +
            $"O servidor '{serverName}' voltou a funcionar normalmente.\n\n" +
            $"Hora da recuperação: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC\n" +
            $"Tempo de indisponibilidade: {downtimeText}\n\n" +
            $"-- PharmazapPlus Monitor";

        await SendNotificationsToAdmins(whatsAppMessage, emailSubject, emailBody);
    }

    private async Task SendNotificationsToAdmins(string whatsAppMessage, string emailSubject, string emailBody)
    {
        var (whatsAppNumbers, emails) = await GetAdminContactsAsync();

        if (_config.NotifyViaWhatsApp && whatsAppNumbers.Count > 0)
        {
            using var scope = _serviceProvider.CreateScope();
            var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            foreach (var number in whatsAppNumbers)
            {
                try
                {
                    var result = await whatsAppService.SendTextMessage(_config.SystemFarmaciaId, number, whatsAppMessage);
                    if (result.Success)
                        _logger.LogInformation("Alerta enviado via WhatsApp para {Number}", number);
                    else
                        _logger.LogWarning("Falha ao enviar alerta WhatsApp para {Number}: {Error}", number, result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar alerta WhatsApp para {Number}", number);
                }
            }
        }

        if (_config.NotifyViaEmail && emails.Count > 0)
        {
            foreach (var email in emails)
            {
                try
                {
                    await _emailService.SendEmailAsync(email, emailSubject, emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar e-mail de alerta para {Email}", email);
                }
            }
        }
    }

    internal async Task<(List<string> WhatsAppNumbers, List<string> Emails)> GetAdminContactsAsync()
    {
        var whatsAppNumbers = new List<string>(_config.AdminWhatsAppNumbers ?? []);
        var emails = new List<string>(_config.AdminEmails ?? []);

        if (_config.FetchAdminsFromDatabase)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PharmazapDbContext>();

                var admins = await dbContext.UsuariosFarmacia
                    .Where(u => u.Ativo &&
                               (u.Role == UserRoles.Gestor || u.Role == UserRoles.Administrador))
                    .Select(u => new { u.Telefone, u.Email })
                    .ToListAsync();

                foreach (var admin in admins)
                {
                    if (!string.IsNullOrWhiteSpace(admin.Telefone))
                    {
                        var cleanPhone = CleanPhoneNumber(admin.Telefone);
                        if (!whatsAppNumbers.Contains(cleanPhone))
                            whatsAppNumbers.Add(cleanPhone);
                    }

                    if (!string.IsNullOrWhiteSpace(admin.Email) && !emails.Contains(admin.Email))
                        emails.Add(admin.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar contatos dos administradores no banco de dados");
            }
        }

        return (whatsAppNumbers, emails);
    }

    /// <summary>
    /// Normalizes a phone number to E.164 format without the '+' prefix,
    /// assuming Brazilian numbers (country code 55) when no country code is present.
    /// </summary>
    internal static string CleanPhoneNumber(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // Add Brazil country code if not already present
        if (digits.Length == 11 || digits.Length == 10)
            digits = "55" + digits;

        return digits;
    }

    internal static string FormatDowntime(TimeSpan downtime)
    {
        if (downtime.TotalMinutes < 1)
            return $"{(int)downtime.TotalSeconds} segundo(s)";
        if (downtime.TotalHours < 1)
            return $"{(int)downtime.TotalMinutes} minuto(s) e {downtime.Seconds} segundo(s)";
        return $"{(int)downtime.TotalHours}h {downtime.Minutes}m {downtime.Seconds}s";
    }
}
