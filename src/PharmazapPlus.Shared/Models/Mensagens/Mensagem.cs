using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PharmazapPlus.Shared.Models.Mensagens;

public class Mensagem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string? MessageId { get; set; } // ID da API do WhatsApp

    public int AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    // Direção e tipo
    public MessageDirection Direction { get; set; }
    public MessageType Type { get; set; }
    public MessageStatus Status { get; set; }

    // Conteúdo
    [MaxLength(5000)]
    public string? Text { get; set; }

    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    [MaxLength(50)]
    public string? MediaType { get; set; }

    [MaxLength(500)]
    public string? MediaCaption { get; set; }

    public long? MediaSize { get; set; }

    // Template (se aplicável)
    [MaxLength(100)]
    public string? TemplateName { get; set; }

    [MaxLength(10)]
    public string? TemplateLanguage { get; set; }

    public string? TemplateVariables { get; set; } // JSON

    // Erros
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public int? ErrorCode { get; set; }

    // Timestamps
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    // Metadados
    [Column(TypeName = "jsonb")]
    public string? MetadataJson { get; set; }

    [NotMapped]
    public Dictionary<string, object> Metadata
    {
        get
        {
            try
            {
                return string.IsNullOrEmpty(MetadataJson)
                    ? new Dictionary<string, object>()
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
        set
        {
            MetadataJson = JsonSerializer.Serialize(value ?? new Dictionary<string, object>());
        }
    }

    // Propriedades calculadas
    [NotMapped]
    public bool IsFromCustomer => Direction == MessageDirection.Inbound;

    [NotMapped]
    public string? SenderName
    {
        get
        {
            var meta = Metadata;
            if (meta.TryGetValue("senderName", out var val))
                return val?.ToString();
            return null;
        }
    }

    [NotMapped]
    public bool IsToCustomer => Direction == MessageDirection.Outbound;

    [NotMapped]
    public bool HasMedia => !string.IsNullOrEmpty(MediaUrl);

    [NotMapped]
    public bool IsTemplate => Type == MessageType.Template;

    /// <summary>
    /// Returns a displayable text for conversation list previews and similar UI.
    /// If <see cref="Text"/> is present, it is returned as-is.
    /// For media messages without text a type indicator is derived from <see cref="MediaType"/>
    /// and <see cref="Type"/> (e.g. "[Imagem]", "[Áudio]").
    /// Returns <c>null</c> for truly blank messages (no text, no media).
    /// </summary>
    [NotMapped]
    public string? DisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Text)) return Text;
            if (HasMedia || !string.IsNullOrWhiteSpace(MediaType))
            {
                var mediaKind = (MediaType ?? string.Empty).Trim().ToLowerInvariant();
                if (mediaKind.Contains("image") || Type == MessageType.Image || Type == MessageType.Sticker)
                    return "[Imagem]";
                if (mediaKind.Contains("video") || Type == MessageType.Video)
                    return "[Vídeo]";
                if (mediaKind.Contains("audio") || Type == MessageType.Audio)
                    return "[Áudio]";
                if (mediaKind.Contains("document") || Type == MessageType.Document)
                    return "[Documento]";
                return "[Mídia]";
            }
            return null;
        }
    }

    [NotMapped]
    public List<string> TemplateVariablesList
    {
        get
        {
            try
            {
                return string.IsNullOrEmpty(TemplateVariables)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(TemplateVariables) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set
        {
            TemplateVariables = JsonSerializer.Serialize(value ?? new List<string>());
        }
    }
}