namespace PharmazapPlus.API.Models;

public class EmailConfig
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "alerts@pharmazapplus.com";
    public string FromName { get; set; } = "PharmazapPlus Monitor";
}
