using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Shared.Models.Core;

/// <summary>
/// Representa um canal de integração (WhatsApp Oficial/Não Oficial, Instagram)
/// Uma farmácia pode ter múltiplos canais da mesma combinação Tipo+Integração
/// </summary>
public class Canal : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int FarmaciaIntegracosId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = "";

    /// <summary>
    /// Tipo do canal: 1 = WhatsApp, 2 = Instagram
    /// </summary>
    [Required]
    public int Tipo { get; set; }

    /// <summary>
    /// Apenas para WhatsApp: true = Oficial (Meta), false = Não Oficial (Evolution, Baileys)
    /// </summary>
    public bool IsOfficial { get; set; } = true;

    // ============================================
    // WhatsApp Oficial (Meta Business API)
    // ============================================
    [MaxLength(100)]
    public string? PhoneNumberId { get; set; }

    [MaxLength(100)]
    public string? BusinessAccountId { get; set; }

    // ============================================
    // WhatsApp Não Oficial (Evolution API, Baileys)
    // ============================================
    [MaxLength(300)]
    public string? ApiUrl { get; set; }

    [MaxLength(200)]
    public string? ApiKey { get; set; }

    [MaxLength(50)]
    public string? InstanceName { get; set; }

    // ============================================
    // Instagram
    // ============================================
    [MaxLength(100)]
    public string? InstagramAccountId { get; set; }

    // ============================================
    // Compartilhado (Oficial e Não Oficial)
    // ============================================
    /// <summary>
    /// Access Token para WhatsApp Oficial ou pode ser usado em Não Oficial conforme config da API
    /// </summary>
    [MaxLength(500)]
    public string? AccessToken { get; set; }

    public MetaConfigSource MetaConfigSource { get; set; } = MetaConfigSource.PharmazapCentral;

    public MetaAppMode MetaAppMode { get; set; } = MetaAppMode.Production;

    [MaxLength(100)]
    public string? MetaAppId { get; set; }

    [MaxLength(200)]
    public string? MetaAppSecret { get; set; }

    [ForeignKey(nameof(FarmaciaIntegracosId))]
    public virtual FarmaciaIntegracoes? FarmaciaIntegracoes { get; set; }
}
