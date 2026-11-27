using PharmazapPlus.Shared.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmazapPlus.Shared.Models.Core;

/// <summary>
/// Armazena as credenciais e configurações de integração (WhatsApp Business API e Instagram)
/// separadas da entidade principal Farmacia, por motivos de segurança e separação de responsabilidades.
/// Relação 1:1 com Farmacia.
/// </summary>
public class FarmaciaIntegracoes : BaseEntity
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }

    // ============================================
    // WhatsApp Oficial (Meta Business API)
    // ============================================
    [MaxLength(100)]
    public string WhatsAppPhoneNumberId { get; set; } = "";

    [MaxLength(500)]
    public string WhatsAppAccessToken { get; set; } = "";

    [MaxLength(100)]
    public string WhatsAppBusinessAccountId { get; set; } = "";

    // ============================================
    // WhatsApp Não Oficial (Evolution API, Baileys)
    // ============================================
    /// <summary>
    /// URL da API não-oficial (Evolution API, Baileys, etc)
    /// </summary>
    [MaxLength(300)]
    public string? WhatsAppApiUrl { get; set; }

    /// <summary>
    /// Chave/Token para autenticação na API não-oficial
    /// </summary>
    [MaxLength(200)]
    public string? WhatsAppApiKey { get; set; }

    /// <summary>
    /// Quando ativado, registra payload completo (request/response) nas integrações de mídia com Evolution GO.
    /// </summary>
    public bool SalvarLogCompletoMidiaApiGo { get; set; }

    /// <summary>
    /// Quando ativado, permite bloquear remetentes (adicionar à lista negra). Padrão: desativado.
    /// </summary>
    public bool HabilitarBloqueioRemetente { get; set; } = false;

    /// <summary>
    /// Quando ativado, permite que atendentes criem e atualizem etiquetas na farmácia.
    /// </summary>
    public bool PermitirGerenciarEtiquetasAtendente { get; set; } = false;

    /// <summary>
    /// ID único gerado para identificar esta farmácia na API não-oficial
    /// </summary>
    [MaxLength(50)]
    public string ChaveWhatsApp { get; set; } = "";

    // ============================================
    // Instagram Business API
    // ============================================
    [MaxLength(100)]
    public string? InstagramAccountId { get; set; }

    [MaxLength(100)]
    public string? PageId { get; set; }

    [MaxLength(500)]
    public string? InstagramAccessToken { get; set; }

    // Navegação
    [ForeignKey(nameof(FarmaciaId))]
    public virtual Farmacia Farmacia { get; set; } = null!;

    public virtual ICollection<Canal>? Canais { get; set; } = new List<Canal>();
}
