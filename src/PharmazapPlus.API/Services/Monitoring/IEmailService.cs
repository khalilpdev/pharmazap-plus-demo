namespace PharmazapPlus.API.Services.Monitoring;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
}
