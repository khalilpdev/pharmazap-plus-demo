using System.Text.Json.Serialization;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services;

internal class MensagemApiDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }

    [JsonPropertyName("direcao")]
    public string? Direcao { get; set; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("mediaUrl")]
    public string? MediaUrl { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("mediaCaption")]
    public string? MediaCaption { get; set; }

    [JsonPropertyName("mediaSize")]
    public long? MediaSize { get; set; }

    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; set; }

    [JsonPropertyName("sentAt")]
    public DateTime? SentAt { get; set; }

    [JsonPropertyName("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }

    [JsonPropertyName("readAt")]
    public DateTime? ReadAt { get; set; }

    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    public Mensagem ToMensagem()
    {
        var direction = ParseDirection(Direcao, SentAt, DeliveredAt, ReadAt);
        var mensagem = new Mensagem
        {
            Id = Guid.TryParse(Id, out var guid) ? guid : Guid.NewGuid(),
            MessageId = MessageId,
            Direction = direction,
            Type = ParseType(Tipo),
            Status = ParseStatus(Status, direction),
            Text = Text,
            MediaUrl = MediaUrl,
            MediaType = MediaType,
            MediaCaption = MediaCaption,
            MediaSize = MediaSize,
            ReceivedAt = ReceivedAt,
            SentAt = SentAt,
            DeliveredAt = DeliveredAt,
            ReadAt = ReadAt
        };

        if (!string.IsNullOrWhiteSpace(GroupName) || !string.IsNullOrWhiteSpace(SenderName))
        {
            var metadata = mensagem.Metadata;
            if (!string.IsNullOrWhiteSpace(GroupName))
                metadata[Consts.EvolutionGoWebhook.GroupNameMetadataKey] = GroupName!;
            if (!string.IsNullOrWhiteSpace(SenderName))
                metadata[Consts.EvolutionGoWebhook.SenderNameMetadataKey] = SenderName!;
            mensagem.Metadata = metadata;
        }

        return mensagem;
    }

    private static MessageDirection ParseDirection(
        string? direcao,
        DateTime? sentAt,
        DateTime? deliveredAt,
        DateTime? readAt)
    {
        if (Enum.TryParse<MessageDirection>(direcao, ignoreCase: true, out var direction))
            return direction;

        return sentAt.HasValue || deliveredAt.HasValue || readAt.HasValue
            ? MessageDirection.Outbound
            : MessageDirection.Inbound;
    }

    private static MessageType ParseType(string? tipo)
    {
        return Enum.TryParse<MessageType>(tipo, ignoreCase: true, out var messageType)
            ? messageType
            : MessageType.Text;
    }

    private static MessageStatus ParseStatus(string? status, MessageDirection direction)
    {
        return Enum.TryParse<MessageStatus>(status, ignoreCase: true, out var messageStatus)
            ? messageStatus
            : direction == MessageDirection.Outbound ? MessageStatus.Enviada : MessageStatus.Recebida;
    }
}
