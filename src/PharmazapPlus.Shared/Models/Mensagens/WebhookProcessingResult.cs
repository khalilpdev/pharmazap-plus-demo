namespace PharmazapPlus.Shared.Models.Mensagens;

public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Mensagem? ProcessedMessage { get; set; }
    public Atendimento? Atendimento { get; set; }
    public bool NewAtendimentoCreated { get; set; }
    public bool NewClientCreated { get; set; }
}
