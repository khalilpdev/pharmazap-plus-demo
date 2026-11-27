using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using System.Text.Json.Serialization;

namespace PharmazapPlus.Shared.Models.Mensagens;

/// <summary>
/// DTO para desserializar a resposta da API GET /api/atendimentos.
/// A API retorna uma projeção anônima em vez do objeto Atendimento completo.
/// </summary>
public record AtendimentoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("whatsAppId")]
    public string? WhatsAppId { get; set; }

    [JsonPropertyName("canal")]
    public string? Canal { get; set; }

    [JsonPropertyName("isGrupo")]
    public bool IsGrupo { get; set; }

    [JsonPropertyName("dataInicio")]
    public DateTime DataInicio { get; set; }

    [JsonPropertyName("dataFim")]
    public DateTime? DataFim { get; set; }

    [JsonPropertyName("clienteNome")]
    public string? ClienteNome { get; set; }

    [JsonPropertyName("atendenteNome")]
    public string? AtendenteNome { get; set; }

    [JsonPropertyName("atendenteId")]
    public int? AtendenteId { get; set; }

    [JsonPropertyName("totalMensagens")]
    public int TotalMensagens { get; set; }

    [JsonPropertyName("ultimaMensagem")]
    public string? UltimaMensagem { get; set; }

    [JsonPropertyName("ultimaMensagemEm")]
    public DateTime? UltimaMensagemEm { get; set; }

    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    [JsonPropertyName("etiquetaId")]
    public int? EtiquetaId { get; set; }

    [JsonPropertyName("etiquetaNome")]
    public string? EtiquetaNome { get; set; }

    [JsonPropertyName("etiquetaCor")]
    public string? EtiquetaCor { get; set; }

    /// <summary>
    /// Converte o DTO para um objeto Atendimento populando os campos relevantes.
    /// </summary>
    public Atendimento ToAtendimento()
    {
        var atendimento = new Atendimento
        {
            Id = Id,
            WhatsAppId = WhatsAppId ?? string.Empty,
            DataInicio = DataInicio,
            DataFim = DataFim,
            EtiquetaId = EtiquetaId,
            AtendenteId = AtendenteId,
            GrupoNome = GroupName,
            SenderName = SenderName,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!
        };

        if (!string.IsNullOrWhiteSpace(Canal) && Enum.TryParse<CanalEnum>(Canal, out var canalEnum))
        {
            atendimento.CanalEnum = canalEnum;
        }

        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<AtendimentoStatusEnum>(Status, out var statusEnum))
        {
            atendimento.StatusEnum = statusEnum;
        }

        if (!string.IsNullOrEmpty(ClienteNome) || !string.IsNullOrEmpty(WhatsAppId))
        {
            atendimento.Cliente = new Cliente
            {
                Nome = ClienteNome ?? WhatsAppId?.FormatContactId(atendimento.CanalEnum) ?? string.Empty,
                WhatsAppId = WhatsAppId ?? string.Empty,
                Farmacia = null!
            };
        }

        if (!string.IsNullOrEmpty(AtendenteNome))
        {
            atendimento.Atendente = new UsuarioFarmacia
            {
                Nome = AtendenteNome,
                Email = string.Empty,
                Telefone = string.Empty,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                Turno = string.Empty,
                Farmacia = null!
            };
        }

        if (!string.IsNullOrEmpty(UltimaMensagem))
        {
            atendimento.Mensagens = new List<Mensagem>
            {
                new Mensagem
                {
                    Text = UltimaMensagem,
                    ReceivedAt = UltimaMensagemEm ?? DataInicio,
                    Direction = MessageDirection.Inbound
                }
            };
        }

        if (EtiquetaId.HasValue)
        {
            atendimento.Etiqueta = new Etiqueta
            {
                Id = EtiquetaId.Value,
                Nome = EtiquetaNome ?? string.Empty,
                Cor = EtiquetaCor ?? Consts.EtiquetaCores.Padrao,
                Farmacia = null!
            };
        }

        return atendimento;
    }
}
