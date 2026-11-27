namespace PharmazapPlus.API.Models;

public class HealthMonitorConfig
{
    public bool Enabled { get; set; } = true;
    public int CheckIntervalSeconds { get; set; } = 60;
    public int FailureThreshold { get; set; } = 3;
    public int AlertCooldownMinutes { get; set; } = 30;
    public bool NotifyViaWhatsApp { get; set; } = true;
    public bool NotifyViaEmail { get; set; } = true;
    public List<string> AdminWhatsAppNumbers { get; set; } = [];
    public List<string> AdminEmails { get; set; } = [];
    public bool FetchAdminsFromDatabase { get; set; } = true;
    public string ServerName { get; set; } = "PharmazapPlus API";

    /// <summary>
    /// FarmaciaId cujo número WhatsApp Business será usado para enviar alertas.
    /// Deixe vazio para usar o MockWhatsAppService ou configure com um ID válido em produção.
    /// </summary>
    public string SystemFarmaciaId { get; set; } = "0";
}
