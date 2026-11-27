using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Web.Services;

public enum ChannelType
{
    WhatsApp = 1,
    Instagram = 2
}

public class ChannelCreateRequest : IValidatableObject
{
    [Required(ErrorMessage = "Nome do canal é obrigatório")]
    [StringLength(120, ErrorMessage = "Nome do canal deve ter no máximo 120 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tipo do canal é obrigatório")]
    public ChannelType Type { get; set; } = ChannelType.WhatsApp;

    public bool IsOfficial { get; set; } = true;

    public MetaConfigSource MetaConfigSource { get; set; } = MetaConfigSource.Own;

    public MetaAppMode MetaAppMode { get; set; }

    [StringLength(100, ErrorMessage = "Meta App ID deve ter no máximo 100 caracteres")]
    public string? MetaAppId { get; set; }

    [StringLength(200, ErrorMessage = "Meta App Secret deve ter no máximo 200 caracteres")]
    public string? MetaAppSecret { get; set; }

    public bool HasPersistedMetaAppSecret { get; set; }

    [StringLength(500, ErrorMessage = "Access Token deve ter no máximo 500 caracteres")]
    public string AccessToken { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Phone Number ID deve ter no máximo 100 caracteres")]
    public string? PhoneNumberId { get; set; }

    [StringLength(100, ErrorMessage = "Business Account ID deve ter no máximo 100 caracteres")]
    public string? BusinessAccountId { get; set; }

    [StringLength(100, ErrorMessage = "Instagram Business ID deve ter no máximo 100 caracteres")]
    public string? InstagramBusinessId { get; set; }

    [StringLength(100, ErrorMessage = "Username do Instagram deve ter no máximo 100 caracteres")]
    public string? InstagramUsername { get; set; }

    [StringLength(200, ErrorMessage = "Password do Instagram deve ter no máximo 200 caracteres")]
    public string? InstagramPassword { get; set; }

    [StringLength(300, ErrorMessage = "URL da API deve ter no máximo 300 caracteres")]
    public string? ApiUrl { get; set; }

    [StringLength(200, ErrorMessage = "API Key deve ter no máximo 200 caracteres")]
    public string? ApiKey { get; set; }

    public bool HasPersistedApiKey { get; set; }

    public bool HasStoredInstagramPassword { get; set; }

    [StringLength(50, ErrorMessage = "Nome da instância deve ter no máximo 50 caracteres")]
    public string? InstanceName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == ChannelType.WhatsApp)
        {
            if (IsOfficial)
            {
                if (!Enum.IsDefined(MetaConfigSource))
                    yield return new ValidationResult("Selecione a origem da configuração Meta", [nameof(MetaConfigSource)]);
                if (MetaConfigSource == MetaConfigSource.PharmazapCentral)
                {
                    if (!Enum.IsDefined(MetaAppMode))
                        yield return new ValidationResult("Selecione o ambiente Meta", [nameof(MetaAppMode)]);
                }
                else if (MetaConfigSource == MetaConfigSource.Own)
                {
                    if (string.IsNullOrWhiteSpace(MetaAppId))
                        yield return new ValidationResult("Meta App ID é obrigatório para usar suas próprias configurações", [nameof(MetaAppId)]);
                    if (string.IsNullOrWhiteSpace(MetaAppSecret) && !HasPersistedMetaAppSecret)
                        yield return new ValidationResult("Meta App Secret é obrigatório para usar suas próprias configurações", [nameof(MetaAppSecret)]);
                }
                if (string.IsNullOrWhiteSpace(PhoneNumberId))
                    yield return new ValidationResult("Phone Number ID é obrigatório para WhatsApp Oficial", [nameof(PhoneNumberId)]);
                if (string.IsNullOrWhiteSpace(AccessToken))
                    yield return new ValidationResult("Access Token é obrigatório para WhatsApp Oficial", [nameof(AccessToken)]);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(InstanceName))
                    yield return new ValidationResult("Nome da instância é obrigatório para WhatsApp Não Oficial", [nameof(InstanceName)]);
                else
                {
                    var trimmed = InstanceName.Trim();

                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Any(ch => !char.IsDigit(ch)))
                    {
                        yield return new ValidationResult(
                            "Identificador da instância deve conter apenas números (DDI + DDD + telefone)",
                            [nameof(InstanceName)]);
                    }
                    else if (trimmed.Length < 8)
                    {
                        yield return new ValidationResult("Identificador da instância deve ter pelo menos 8 dígitos", [nameof(InstanceName)]);
                    }
                }
            }
        }

        if (Type == ChannelType.Instagram)
        {
            if (!IsOfficial)
                yield return new ValidationResult("Instagram deve usar integração oficial da Meta", [nameof(IsOfficial)]);
            if (!Enum.IsDefined(MetaConfigSource))
                yield return new ValidationResult("Selecione a origem da configuração Meta", [nameof(MetaConfigSource)]);
            if (MetaConfigSource == MetaConfigSource.PharmazapCentral)
            {
                if (!Enum.IsDefined(MetaAppMode))
                    yield return new ValidationResult("Selecione o ambiente Meta", [nameof(MetaAppMode)]);
            }
            else if (MetaConfigSource == MetaConfigSource.Own)
            {
                if (string.IsNullOrWhiteSpace(MetaAppId))
                    yield return new ValidationResult("Meta App ID é obrigatório para usar suas próprias configurações", [nameof(MetaAppId)]);
                if (string.IsNullOrWhiteSpace(MetaAppSecret) && !HasPersistedMetaAppSecret)
                    yield return new ValidationResult("Meta App Secret é obrigatório para usar suas próprias configurações", [nameof(MetaAppSecret)]);
            }

            if (string.IsNullOrWhiteSpace(InstagramBusinessId))
                yield return new ValidationResult("Instagram Business ID é obrigatório para integração oficial da Meta", [nameof(InstagramBusinessId)]);
            if (string.IsNullOrWhiteSpace(AccessToken))
                yield return new ValidationResult("Access Token é obrigatório para integração oficial da Meta", [nameof(AccessToken)]);
        }
    }
}

public class ChannelListItem
{
    public int Id { get; set; }
    public int Type { get; set; }
    public bool IsOfficial { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Configured { get; set; }
    public string? Summary { get; set; }
    public string? InstanceName { get; set; }
    public string? EvolutionStatus { get; set; }
}

public class ChannelDetailDto
{
    public int Id { get; set; }
    public int Type { get; set; }
    public bool IsOfficial { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public MetaConfigSource MetaConfigSource { get; set; } = MetaConfigSource.Own;
    public MetaAppMode MetaAppMode { get; set; } = MetaAppMode.Production;
    public string? MetaAppId { get; set; }
    [JsonIgnore] public string? MetaAppSecret { get; set; }
    public bool HasPersistedMetaAppSecret { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? InstagramBusinessId { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? InstanceName { get; set; }
    public bool HasPersistedApiKey { get; set; }
    public string? InstagramUsername { get; set; }
    [JsonIgnore] public string? InstagramPassword { get; set; }
    public bool HasInstagramPassword { get; set; }
}
