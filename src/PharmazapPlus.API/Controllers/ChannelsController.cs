using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using QRCoder;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Sockets;
using System.Security.Authentication;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/channels")]
[Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Desenvolvedor}")]
public class ChannelsController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<ChannelsController> _logger;
    private readonly ICryptoService _cryptoService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment? _webHostEnvironment;

    public ChannelsController(PharmazapDbContext context, ILogger<ChannelsController> logger, ICryptoService cryptoService, IHttpClientFactory httpClientFactory, IConfiguration configuration, IWebHostEnvironment? webHostEnvironment = null)
    {
        _context = context;
        _logger = logger;
        _cryptoService = cryptoService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetChannels()
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia == null)
                return NotFound(new { success = false, error = "Farmácia não encontrada" });

            if (farmacia.Integracoes == null)
                return Ok(new { success = true, data = Array.Empty<object>() });

            await _context.Entry(farmacia.Integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            var canais = farmacia.Integracoes.Canais ?? new List<Canal>();
            var channels = canais.Select(c =>
            {
                var storedInstanceName = c.Tipo == Consts.CanalTipo.WhatsApp && !c.IsOfficial
                    ? GetStoredInstanceName(c, farmacia.Integracoes)
                    : string.Empty;

                return new
                {
                    id = c.Id,
                    name = c.Nome,
                    type = c.Tipo,
                    isOfficial = c.IsOfficial,
                    icon = c.Tipo == 1 ? "WhatsApp" : "Instagram",
                    configured = IsChannelConfigured(c),
                    summary = c.Tipo == 1 && c.IsOfficial ? c.PhoneNumberId
                        : c.Tipo == 1 && !c.IsOfficial
                            ? (string.IsNullOrWhiteSpace(storedInstanceName) ? null : GetInstanceDisplayName(storedInstanceName))
                            : c.Tipo == 2 ? c.InstagramAccountId : null,
                    instanceName = c.Tipo == 1 && !c.IsOfficial && !string.IsNullOrWhiteSpace(storedInstanceName) ? storedInstanceName : null,
                    evolutionStatus = c.Tipo == 1 && !c.IsOfficial ? (string?)null : null
                };
            }).ToList();

            return Ok(new { success = true, data = channels });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar canais");
            return StatusCode(500, new { success = false, error = "Erro ao listar canais" });
        }
    }

    [HttpGet("{type}/{isOfficial}")]
    public async Task<IActionResult> GetChannel(int type, bool isOfficial)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia?.Integracoes == null)
                return NotFound(new { success = false, error = "Integrações não encontradas" });

            await _context.Entry(farmacia.Integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            var canal = farmacia.Integracoes.Canais?.FirstOrDefault(c => c.Tipo == type && c.IsOfficial == isOfficial);
            
            // Retorna sucesso mesmo se não encontrar (para diferençar de erro)
            if (canal == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        type = type,
                        isOfficial = isOfficial,
                        name = GetDefaultChannelName(type, isOfficial),
                        metaConfigSource = MetaConfigSource.Own,
                        metaAppId = "",
                        metaAppSecret = "",
                        hasPersistedMetaAppSecret = false,
                        phoneNumberId = "",
                        businessAccountId = "",
                        apiUrl = "",
                        apiKey = "",
                        instanceName = "",
                        instagramBusinessId = "",
                        instagramAccountId = "",
                        accessToken = "",
                        metaAppMode = MetaAppMode.Production,
                        hasPersistedApiKey = false
                    }
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = canal.Id,
                    name = canal.Nome,
                    type = canal.Tipo,
                    isOfficial = canal.IsOfficial,
                    metaConfigSource = canal.MetaConfigSource,
                    metaAppId = canal.MetaConfigSource == MetaConfigSource.Own ? canal.MetaAppId ?? "" : "",
                    metaAppSecret = "",
                    hasPersistedMetaAppSecret = canal.MetaConfigSource == MetaConfigSource.Own && !string.IsNullOrWhiteSpace(canal.MetaAppSecret),
                    phoneNumberId = canal.PhoneNumberId ?? "",
                    businessAccountId = canal.BusinessAccountId ?? "",
                    apiUrl = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? "" : canal.ApiUrl ?? "",
                    apiKey = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? "" : canal.ApiKey ?? "",
                    instanceName = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? GetStoredInstanceName(canal, farmacia.Integracoes) : "",
                    instagramBusinessId = canal.InstagramAccountId ?? "",
                    instagramAccountId = canal.InstagramAccountId ?? "",
                    accessToken = canal.AccessToken ?? "",
                    metaAppMode = canal.MetaAppMode,
                    hasInstagramPassword = !string.IsNullOrWhiteSpace(canal.ApiKey),
                    hasPersistedApiKey = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial &&
                        (!string.IsNullOrWhiteSpace(canal.ApiKey) || !string.IsNullOrWhiteSpace(farmacia.Integracoes?.WhatsAppApiKey))
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter canal");
            return StatusCode(500, new { success = false, error = "Erro ao obter canal" });
        }
    }

    [HttpGet("id/{id}")]
    public async Task<IActionResult> GetChannelById(int id)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia?.Integracoes == null)
                return NotFound(new { success = false, error = "Integrações não encontradas" });

            var integracoes = farmacia.Integracoes!;

            await _context.Entry(integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            var canal = integracoes.Canais?.FirstOrDefault(c => c.Id == id);
            if (canal == null)
                return NotFound(new { success = false, error = "Canal não encontrado" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = canal.Id,
                    name = canal.Nome,
                    type = canal.Tipo,
                    isOfficial = canal.IsOfficial,
                    metaConfigSource = canal.MetaConfigSource,
                    metaAppId = canal.MetaConfigSource == MetaConfigSource.Own ? canal.MetaAppId ?? "" : "",
                    metaAppSecret = "",
                    hasPersistedMetaAppSecret = canal.MetaConfigSource == MetaConfigSource.Own && !string.IsNullOrWhiteSpace(canal.MetaAppSecret),
                    phoneNumberId = canal.PhoneNumberId ?? "",
                    businessAccountId = canal.BusinessAccountId ?? "",
                    apiUrl = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? "" : canal.ApiUrl ?? "",
                    apiKey = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? "" : canal.ApiKey ?? "",
                    instanceName = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial ? GetStoredInstanceName(canal, farmacia.Integracoes) : "",
                    hasPersistedApiKey = canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial &&
                        (!string.IsNullOrWhiteSpace(canal.ApiKey) || !string.IsNullOrWhiteSpace(farmacia.Integracoes.WhatsAppApiKey)),
                    instagramBusinessId = canal.IsOfficial ? canal.InstagramAccountId ?? "" : "",
                    instagramUsername = canal.IsOfficial ? "" : canal.InstagramAccountId ?? "",
                    instagramPassword = "",
                    hasInstagramPassword = !string.IsNullOrWhiteSpace(canal.ApiKey),
                    metaAppMode = canal.MetaAppMode,
                    accessToken = canal.Tipo == 2
                        ? (canal.IsOfficial ? canal.AccessToken ?? "" : "")
                        : canal.AccessToken ?? ""
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter canal");
            return StatusCode(500, new { success = false, error = "Erro ao obter canal" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var name = request.Name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, error = "Nome do canal é obrigatório" });

            if (request.Type is not (1 or 2))
                return BadRequest(new { success = false, error = "Tipo de canal inválido" });

            // Validações específicas por tipo
            if (request.Type == 1) // WhatsApp
            {
                if (request.IsOfficial)
                {
                    if (!IsValidMetaConfigSource(request.MetaConfigSource))
                        return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
                    if (request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
                        return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
                    if (request.MetaConfigSource == MetaConfigSource.Own)
                    {
                        if (string.IsNullOrWhiteSpace(request.MetaAppId))
                            return BadRequest(new { success = false, error = "Meta App ID é obrigatório para usar suas próprias configurações." });
                        if (string.IsNullOrWhiteSpace(request.MetaAppSecret))
                            return BadRequest(new { success = false, error = "Meta App Secret é obrigatório para usar suas próprias configurações." });
                    }
                    if (string.IsNullOrWhiteSpace(request.PhoneNumberId))
                        return BadRequest(new { success = false, error = "Phone Number ID é obrigatório para WhatsApp Oficial" });
                    if (string.IsNullOrWhiteSpace(request.AccessToken))
                        return BadRequest(new { success = false, error = "Access Token é obrigatório para WhatsApp Oficial" });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(request.InstanceName))
                        return BadRequest(new { success = false, error = "Nome da instância é obrigatório para WhatsApp Não Oficial" });
                }
            }
            else if (request.Type == 2) // Instagram
            {
                if (request.IsOfficial)
                {
                    if (!IsValidMetaConfigSource(request.MetaConfigSource))
                        return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
                    if (request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
                        return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
                    if (request.MetaConfigSource == MetaConfigSource.Own)
                    {
                        if (string.IsNullOrWhiteSpace(request.MetaAppId))
                            return BadRequest(new { success = false, error = "Meta App ID é obrigatório para usar suas próprias configurações." });
                        if (string.IsNullOrWhiteSpace(request.MetaAppSecret))
                            return BadRequest(new { success = false, error = "Meta App Secret é obrigatório para usar suas próprias configurações." });
                    }
                    if (string.IsNullOrWhiteSpace(request.InstagramBusinessId))
                        return BadRequest(new { success = false, error = "Instagram Business ID é obrigatório para integração oficial da Meta" });
                    if (string.IsNullOrWhiteSpace(request.AccessToken))
                        return BadRequest(new { success = false, error = "Access Token é obrigatório para integração oficial da Meta" });
                }
                else
                {
                    return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });
                }
            }

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia == null)
                return NotFound(new { success = false, error = "Farmácia não encontrada" });

            // Inicializa integracoes se não existir
            if (farmacia.Integracoes == null)
            {
                farmacia.Integracoes = new FarmaciaIntegracoes
                {
                    FarmaciaId = farmacia.Id,
                    ChaveWhatsApp = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()
                };
                _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
                await _context.SaveChangesAsync();
            }

            // Carregar canais existentes
            await _context.Entry(farmacia.Integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            // Validar unicidade global dos canais WhatsApp pelo identificador próprio
            if (request.Type == 1 && request.IsOfficial && !string.IsNullOrWhiteSpace(request.PhoneNumberId))
            {
                var phoneNumberId = request.PhoneNumberId.Trim();
                var existingCanal = await FindWhatsAppOfficialChannelByPhoneNumberIdAsync(phoneNumberId);
                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O Phone Number ID '{phoneNumberId}' já está em uso por outro canal WhatsApp." });
                }
            }
            else if (request.Type == 1 && !request.IsOfficial && !string.IsNullOrWhiteSpace(request.InstanceName))
            {
                var instanceNameTrimmed = NormalizeInstanceName(request.InstanceName);
                var instanceNameDisplay = GetInstanceDisplayName(instanceNameTrimmed);
                var existingCanal = await FindWhatsAppNonOfficialChannelByInstanceNameAsync(instanceNameTrimmed);

                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O nome da instância '{instanceNameDisplay}' já está em uso por outro canal WhatsApp." });
                }
            }

            // Cria novo canal (permite múltiplos com mesma combinação Tipo/IsOfficial)
            var canal = new Canal
            {
                FarmaciaIntegracosId = farmacia.Integracoes.Id,
                Nome = name,
                Tipo = request.Type,
                IsOfficial = request.IsOfficial,
                MetaConfigSource = ResolveMetaConfigSource(request.MetaConfigSource),
                MetaAppMode = request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                    ? ResolveMetaAppMode(request.MetaAppMode)
                    : MetaAppMode.Production
            };

            if (request.Type == 1) // WhatsApp
            {
                if (request.IsOfficial)
                {
                    canal.PhoneNumberId = request.PhoneNumberId!.Trim();
                    canal.BusinessAccountId = request.BusinessAccountId?.Trim();
                    canal.AccessToken = request.AccessToken?.Trim() ?? "";
                    canal.MetaConfigSource = ResolveMetaConfigSource(request.MetaConfigSource);
                    canal.MetaAppMode = request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                        ? ResolveMetaAppMode(request.MetaAppMode)
                        : MetaAppMode.Production;
                    canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                    canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppSecret?.Trim() : null;
                }
                else
                {
                    var resolvedApiKey = ResolveEvolutionApiKey(request.ApiKey, farmacia.Integracoes?.WhatsAppApiKey);
                    if (string.IsNullOrWhiteSpace(resolvedApiKey))
                        return BadRequest(new { success = false, error = "GLOBAL_API_KEY não configurada para WhatsApp Não Oficial" });

                    var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, farmacia.Integracoes?.WhatsAppApiUrl);
                    var urlValidation = ValidateEvolutionApiUrl(resolvedApiUrl);
                    if (!urlValidation.IsValid)
                    {
                        _logger.LogWarning("URL Evolution API inválida: {ApiUrl}. Detalhes: {Reason}", resolvedApiUrl, urlValidation.ErrorMessage);
                        return BadRequest(new { success = false, error = urlValidation.ErrorMessage });
                    }

                    canal.ApiUrl = resolvedApiUrl;
                    canal.ApiKey = resolvedApiKey;
                    canal.AccessToken = request.AccessToken?.Trim();
                    if (!string.IsNullOrWhiteSpace(request.InstanceName))
                    {
                        canal.InstanceName = NormalizeInstanceName(request.InstanceName);
                        SyncIntegracoesCanalNaoOficial(farmacia.Integracoes, canal.InstanceName, resolvedApiUrl, resolvedApiKey);
                    }

                    canal.MetaConfigSource = MetaConfigSource.PharmazapCentral;
                    canal.MetaAppMode = MetaAppMode.Production;
                    canal.MetaAppId = null;
                    canal.MetaAppSecret = null;
                }
            }
            else if (request.Type == 2) // Instagram
            {
                if (!request.IsOfficial)
                    return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });

                if (string.IsNullOrWhiteSpace(request.InstagramBusinessId))
                    return BadRequest(new { success = false, error = "Instagram Business ID é obrigatório para integração oficial da Meta" });
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return BadRequest(new { success = false, error = "Access Token é obrigatório para integração oficial da Meta" });

                canal.InstagramAccountId = request.InstagramBusinessId.Trim();
                canal.AccessToken = request.AccessToken.Trim();
                canal.ApiKey = null;
                canal.MetaConfigSource = ResolveMetaConfigSource(request.MetaConfigSource);
                canal.MetaAppMode = request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                    ? ResolveMetaAppMode(request.MetaAppMode)
                    : MetaAppMode.Production;
                canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppSecret?.Trim() : null;
            }

            _context.Canais.Add(canal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Canal '{Nome}' criado: {Type} ({IsOfficial}) para farmácia {FarmaciaId}", 
                name, request.Type, request.IsOfficial ? "Oficial" : "Não oficial", farmaciaId);

            return Ok(new
            {
                success = true,
                data = new { id = canal.Id },
                message = "Canal criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar canal");
            return StatusCode(500, new { success = false, error = "Erro ao salvar canal" });
        }
    }

    [HttpPut("{type}/{isOfficial}")]
    public async Task<IActionResult> UpdateChannel(int type, bool isOfficial, [FromBody] CreateChannelRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, error = "Nome do canal é obrigatório" });

            if (type is not (1 or 2))
                return BadRequest(new { success = false, error = "Tipo de canal inválido" });

            // Validações específicas por tipo
            if (type == 1 && isOfficial)
            {
                if (!IsValidMetaConfigSource(request.MetaConfigSource))
                    return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
                if (request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
                    return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
                if (string.IsNullOrWhiteSpace(request.PhoneNumberId))
                    return BadRequest(new { success = false, error = "Phone Number ID é obrigatório para WhatsApp Oficial" });
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return BadRequest(new { success = false, error = "Access Token é obrigatório para WhatsApp Oficial" });
            }
            else if (type == 1 && !isOfficial)
            {
                // Validado após carregar o canal para permitir reaproveitar o identificador já persistido.
            }
            else if (type == 2 && !request.IsOfficial)
            {
                return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });
            }
            else if (type == 2 && !IsValidMetaConfigSource(request.MetaConfigSource))
            {
                return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
            }
            else if (type == 2 && request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
            {
                return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
            }

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia == null)
                return NotFound(new { success = false, error = "Farmácia não encontrada" });

            // Inicializa integracoes se não existir
            if (farmacia.Integracoes == null)
            {
                farmacia.Integracoes = new FarmaciaIntegracoes
                {
                    FarmaciaId = farmacia.Id,
                    ChaveWhatsApp = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()
                };
                _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
                await _context.SaveChangesAsync();
            }

            var integracoes = farmacia.Integracoes!;

            await _context.Entry(integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            // Procura canal existente para essa combinação de tipo/isOfficial
            var canal = integracoes.Canais?.FirstOrDefault(c => c.Tipo == type && c.IsOfficial == isOfficial);

            var effectiveInstanceName = request.InstanceName;
            if (type == Consts.CanalTipo.WhatsApp && !isOfficial && string.IsNullOrWhiteSpace(effectiveInstanceName))
                effectiveInstanceName = canal == null ? null : GetStoredInstanceName(canal, integracoes);

            // Validar unicidade global dos canais WhatsApp pelo identificador próprio
            if (type == Consts.CanalTipo.WhatsApp && isOfficial && !string.IsNullOrWhiteSpace(request.PhoneNumberId))
            {
                var phoneNumberId = request.PhoneNumberId.Trim();
                var existingCanal = await FindWhatsAppOfficialChannelByPhoneNumberIdAsync(
                    phoneNumberId,
                    canal?.Id);

                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O Phone Number ID '{phoneNumberId}' já está em uso por outro canal WhatsApp." });
                }
            }
            else if (type == Consts.CanalTipo.WhatsApp && !isOfficial && !string.IsNullOrWhiteSpace(effectiveInstanceName))
            {
                var instanceNameTrimmed = NormalizeInstanceName(effectiveInstanceName);
                var instanceNameDisplay = GetInstanceDisplayName(instanceNameTrimmed);
                var existingCanal = await FindWhatsAppNonOfficialChannelByInstanceNameAsync(
                    instanceNameTrimmed,
                    canal?.Id);

                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O nome da instância '{instanceNameDisplay}' já está em uso por outro canal WhatsApp." });
                }
            }

            if (((type == Consts.CanalTipo.WhatsApp && isOfficial) || type == Consts.CanalTipo.Instagram) &&
                request.MetaConfigSource == MetaConfigSource.Own)
            {
                if (string.IsNullOrWhiteSpace(request.MetaAppId))
                    return BadRequest(new { success = false, error = "Meta App ID é obrigatório para usar suas próprias configurações." });

                var existingMetaAppSecret = canal?.MetaAppSecret;
                if (string.IsNullOrWhiteSpace(request.MetaAppSecret) && string.IsNullOrWhiteSpace(existingMetaAppSecret))
                    return BadRequest(new { success = false, error = "Meta App Secret é obrigatório para usar suas próprias configurações." });
            }
            else if (type == Consts.CanalTipo.WhatsApp && !isOfficial && string.IsNullOrWhiteSpace(effectiveInstanceName))
            {
                return BadRequest(new { success = false, error = "Nome da instância é obrigatório para WhatsApp Não Oficial" });
            }

            if (canal == null)
            {
                // Se não encontrar, cria um novo canal
                canal = new Canal
                {
                    FarmaciaIntegracosId = integracoes.Id,
                    Nome = name,
                    Tipo = type,
                    IsOfficial = isOfficial,
                    MetaConfigSource = ResolveMetaConfigSource(request.MetaConfigSource),
                    MetaAppMode = request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                        ? ResolveMetaAppMode(request.MetaAppMode)
                        : MetaAppMode.Production
                };
                _context.Canais.Add(canal);
            }
            else
            {
                // Se encontrar, atualiza o nome
                canal.Nome = name;
            }

            var usesMetaOfficialConfig = (type == Consts.CanalTipo.WhatsApp && isOfficial) || type == Consts.CanalTipo.Instagram;
            canal.MetaConfigSource = usesMetaOfficialConfig
                ? ResolveMetaConfigSource(request.MetaConfigSource)
                : MetaConfigSource.PharmazapCentral;
            canal.MetaAppMode = usesMetaOfficialConfig && request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                ? ResolveMetaAppMode(request.MetaAppMode)
                : MetaAppMode.Production;

            // Atualiza os dados do canal conforme o tipo
            if (type == 1) // WhatsApp
            {
                if (isOfficial)
                {
                    canal.PhoneNumberId = request.PhoneNumberId!.Trim();
                    canal.BusinessAccountId = request.BusinessAccountId?.Trim();
                    canal.AccessToken = request.AccessToken?.Trim() ?? "";
                    canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                    canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own
                        ? ResolveMetaAppSecret(request.MetaAppSecret, canal.MetaAppSecret)
                        : null;
                }
                else
                {
                    var resolvedApiKey = ResolveEvolutionApiKey(request.ApiKey, canal?.ApiKey ?? integracoes.WhatsAppApiKey);
                    if (string.IsNullOrWhiteSpace(resolvedApiKey))
                        return BadRequest(new { success = false, error = "GLOBAL_API_KEY não configurada para WhatsApp Não Oficial" });

                    var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, canal?.ApiUrl ?? integracoes.WhatsAppApiUrl);
                    var urlValidation = ValidateEvolutionApiUrl(resolvedApiUrl);
                    if (!urlValidation.IsValid)
                    {
                        _logger.LogWarning("URL Evolution API inválida na edição: {ApiUrl}. Detalhes: {Reason}", resolvedApiUrl, urlValidation.ErrorMessage);
                        return BadRequest(new { success = false, error = urlValidation.ErrorMessage });
                    }

                    canal.ApiUrl = resolvedApiUrl;
                    canal.ApiKey = resolvedApiKey;
                    canal.AccessToken = request.AccessToken?.Trim();
                    if (!string.IsNullOrWhiteSpace(effectiveInstanceName))
                    {
                        canal.InstanceName = NormalizeInstanceName(effectiveInstanceName);
                        SyncIntegracoesCanalNaoOficial(integracoes, canal.InstanceName, resolvedApiUrl, resolvedApiKey);
                    }

                    canal.MetaAppId = null;
                    canal.MetaAppSecret = null;
                }
            }
            else if (type == 2) // Instagram
            {
                if (!request.IsOfficial)
                    return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });

                if (string.IsNullOrWhiteSpace(request.InstagramBusinessId))
                    return BadRequest(new { success = false, error = "Instagram Business ID é obrigatório para integração oficial da Meta" });
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return BadRequest(new { success = false, error = "Access Token é obrigatório para integração oficial da Meta" });

                canal.InstagramAccountId = request.InstagramBusinessId.Trim();
                canal.AccessToken = request.AccessToken.Trim();
                canal.ApiKey = null;
                canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own
                    ? ResolveMetaAppSecret(request.MetaAppSecret, canal.MetaAppSecret)
                    : null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Canal atualizado: {Type} ({IsOfficial}) para farmácia {FarmaciaId}", 
                type, isOfficial ? "Oficial" : "Não oficial", farmaciaId);

            return Ok(new
            {
                success = true,
                message = "Canal atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar canal");
            return StatusCode(500, new { success = false, error = "Erro ao atualizar canal" });
        }
    }

    [HttpPut("id/{id}")]
    public async Task<IActionResult> UpdateChannelById(int id, [FromBody] CreateChannelRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, error = "Nome do canal é obrigatório" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia?.Integracoes == null)
                return NotFound(new { success = false, error = "Integrações não encontradas" });

            var integracoes = farmacia.Integracoes!;

            await _context.Entry(integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            var canal = integracoes.Canais?.FirstOrDefault(c => c.Id == id);
            if (canal == null)
                return NotFound(new { success = false, error = "Canal não encontrado" });

            var effectiveInstanceName = string.IsNullOrWhiteSpace(request.InstanceName)
                ? GetStoredInstanceName(canal, integracoes)
                : request.InstanceName;

            // Validar unicidade global dos canais WhatsApp pelo identificador próprio
            if (canal.Tipo == Consts.CanalTipo.WhatsApp && canal.IsOfficial && request.IsOfficial && !string.IsNullOrWhiteSpace(request.PhoneNumberId))
            {
                var phoneNumberId = request.PhoneNumberId.Trim();
                var existingCanal = await FindWhatsAppOfficialChannelByPhoneNumberIdAsync(phoneNumberId, id);
                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O Phone Number ID '{phoneNumberId}' já está em uso por outro canal WhatsApp." });
                }
            }
            else if (canal.Tipo == Consts.CanalTipo.WhatsApp && !canal.IsOfficial && !string.IsNullOrWhiteSpace(effectiveInstanceName))
            {
                var instanceNameTrimmed = NormalizeInstanceName(effectiveInstanceName);
                var instanceNameDisplay = GetInstanceDisplayName(instanceNameTrimmed);
                var existingCanal = await FindWhatsAppNonOfficialChannelByInstanceNameAsync(instanceNameTrimmed, id);

                if (existingCanal != null)
                {
                    return BadRequest(new { success = false, error = $"O nome da instância '{instanceNameDisplay}' já está em uso por outro canal WhatsApp." });
                }
            }

            // Validações específicas
            if (canal.Tipo == 1 && request.IsOfficial)
            {
                if (!IsValidMetaConfigSource(request.MetaConfigSource))
                    return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
                if (request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
                    return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
                if (string.IsNullOrWhiteSpace(request.PhoneNumberId))
                    return BadRequest(new { success = false, error = "Phone Number ID é obrigatório" });
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return BadRequest(new { success = false, error = "Access Token é obrigatório" });
            }
            else if (canal.Tipo == 1 && !request.IsOfficial)
            {
                if (string.IsNullOrWhiteSpace(effectiveInstanceName))
                    return BadRequest(new { success = false, error = "Nome da instância é obrigatório para WhatsApp Não Oficial" });

                // Validar URL da Evolution API
                var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, canal.ApiUrl ?? integracoes.WhatsAppApiUrl);
                var urlValidation = ValidateEvolutionApiUrl(resolvedApiUrl);
                if (!urlValidation.IsValid)
                {
                    _logger.LogWarning("URL Evolution API inválida na edição: {ApiUrl}. Detalhes: {Reason}", resolvedApiUrl, urlValidation.ErrorMessage);
                    return BadRequest(new { success = false, error = urlValidation.ErrorMessage });
                }
            }
            else if (canal.Tipo == 2 && !request.IsOfficial)
            {
                return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });
            }
            else if (canal.Tipo == 2 && !IsValidMetaConfigSource(request.MetaConfigSource))
            {
                return BadRequest(new { success = false, error = "Origem da configuração Meta inválida para integração oficial." });
            }
            else if (canal.Tipo == 2 && request.MetaConfigSource == MetaConfigSource.PharmazapCentral && !IsValidMetaAppMode(request.MetaAppMode))
            {
                return BadRequest(new { success = false, error = "Ambiente Meta inválido para integração oficial." });
            }

            if (((canal.Tipo == Consts.CanalTipo.WhatsApp && request.IsOfficial) || canal.Tipo == Consts.CanalTipo.Instagram) &&
                request.MetaConfigSource == MetaConfigSource.Own)
            {
                if (string.IsNullOrWhiteSpace(request.MetaAppId))
                    return BadRequest(new { success = false, error = "Meta App ID é obrigatório para usar suas próprias configurações." });
                if (string.IsNullOrWhiteSpace(request.MetaAppSecret) && string.IsNullOrWhiteSpace(canal.MetaAppSecret))
                    return BadRequest(new { success = false, error = "Meta App Secret é obrigatório para usar suas próprias configurações." });
            }

            canal.Nome = name;
            canal.IsOfficial = request.IsOfficial;
            var usesMetaOfficialConfig = (canal.Tipo == Consts.CanalTipo.WhatsApp && request.IsOfficial) || canal.Tipo == Consts.CanalTipo.Instagram;
            canal.MetaConfigSource = usesMetaOfficialConfig
                ? ResolveMetaConfigSource(request.MetaConfigSource)
                : MetaConfigSource.PharmazapCentral;
            canal.MetaAppMode = usesMetaOfficialConfig && request.MetaConfigSource == MetaConfigSource.PharmazapCentral
                ? ResolveMetaAppMode(request.MetaAppMode)
                : MetaAppMode.Production;

            if (canal.Tipo == 1)
            {
                if (request.IsOfficial)
                {
                    canal.PhoneNumberId = request.PhoneNumberId!.Trim();
                    canal.BusinessAccountId = request.BusinessAccountId?.Trim();
                    canal.AccessToken = request.AccessToken?.Trim() ?? "";
                    canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                    canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own
                        ? ResolveMetaAppSecret(request.MetaAppSecret, canal.MetaAppSecret)
                        : null;
                }
                else
                {
                    var resolvedApiKey = ResolveEvolutionApiKey(request.ApiKey, canal?.ApiKey ?? integracoes.WhatsAppApiKey);
                    if (string.IsNullOrWhiteSpace(resolvedApiKey))
                        return BadRequest(new { success = false, error = "GLOBAL_API_KEY não configurada para WhatsApp Não Oficial" });

                    var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, canal?.ApiUrl ?? integracoes.WhatsAppApiUrl);
                    var urlValidation = ValidateEvolutionApiUrl(resolvedApiUrl);
                    if (!urlValidation.IsValid)
                        return BadRequest(new { success = false, error = urlValidation.ErrorMessage });

                    canal.ApiUrl = resolvedApiUrl;
                    canal.ApiKey = resolvedApiKey;
                    canal.AccessToken = request.AccessToken?.Trim();
                    if (!string.IsNullOrWhiteSpace(effectiveInstanceName))
                    {
                        canal.InstanceName = NormalizeInstanceName(effectiveInstanceName);
                        SyncIntegracoesCanalNaoOficial(integracoes, canal.InstanceName, resolvedApiUrl, resolvedApiKey);
                    }

                    canal.MetaAppId = null;
                    canal.MetaAppSecret = null;
                }
            }
            else if (canal.Tipo == 2)
            {
                if (!request.IsOfficial)
                    return BadRequest(new { success = false, error = "Instagram deve usar integração oficial da Meta" });

                if (string.IsNullOrWhiteSpace(request.InstagramBusinessId))
                    return BadRequest(new { success = false, error = "Instagram Business ID é obrigatório para integração oficial da Meta" });
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                    return BadRequest(new { success = false, error = "Access Token é obrigatório para integração oficial da Meta" });

                canal.InstagramAccountId = request.InstagramBusinessId.Trim();
                canal.AccessToken = request.AccessToken.Trim();
                canal.ApiKey = null;
                canal.MetaAppId = request.MetaConfigSource == MetaConfigSource.Own ? request.MetaAppId?.Trim() : null;
                canal.MetaAppSecret = request.MetaConfigSource == MetaConfigSource.Own
                    ? ResolveMetaAppSecret(request.MetaAppSecret, canal.MetaAppSecret)
                    : null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Canal {Id} atualizado para farmácia {FarmaciaId}", id, farmaciaId);

            return Ok(new
            {
                success = true,
                message = "Canal atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar canal");
            return StatusCode(500, new { success = false, error = "Erro ao atualizar canal" });
        }
    }

    /// <summary>
    /// Gera o QR Code para conectar o WhatsApp Não Oficial via Evolution Go.
    /// Proxeia a chamada para a Evolution Go API usando as credenciais informadas,
    /// evitando que o cliente acesse o endpoint da Evolution diretamente.
    /// </summary>
    [HttpPost("qrcode")]
    public async Task<IActionResult> GenerateQrCode([FromBody] QrCodeGenerateRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);
            if (farmacia == null)
                return NotFound(new { success = false, error = "Farmácia não encontrada" });

            if (string.IsNullOrWhiteSpace(request.InstanceName))
                return BadRequest(new { success = false, error = "Nome da instância é obrigatório" });
            var persistedApiUrl = farmacia.Integracoes?.WhatsAppApiUrl?.Trim();
            var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida. Informe a URL base da Evolution Go (ex.: http://host:8080)." });
            evoBase = evoBase.TrimEnd('/');
            _logger.LogInformation("Evolution Go QR Code usando base URL efetiva: {EvoBase}", evoBase);

            var instanceName = NormalizeInstanceName(request.InstanceName);
            var persistedApiKey = farmacia.Integracoes?.WhatsAppApiKey?.Trim();
            var apiKey = ResolveEvolutionApiKey(request.ApiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia" });

            await PersistNonOfficialApiSettingsIfMissingAsync(
                farmacia,
                instanceName,
                resolvedApiUrl,
                apiKey);

            var salvarLogCompleto = await GetSalvarLogCompletoAsync(farmaciaId);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // 1. Verificar se a instância já existe. Só criar se não existir, para evitar
            // que a chamada de criação interfira no estado de QR Code que a Evolution Go
            // já está gerando (em especial durante retries, pois criar uma instância existente
            // pode redefinir o estado da instância em algumas versões da Evolution Go).
            var resolvedInstance = await TryResolveEvolutionInstanceAsync(httpClient, evoBase, apiKey, instanceName);
            var resolvedApiKey = string.IsNullOrWhiteSpace(resolvedInstance?.Token) ? null : resolvedInstance.Token;
            if (resolvedInstance is null)
            {
                // Instância não encontrada — tenta criar e depois recarregar o token.
                var createPayloads = new List<string>
                {
                    BuildEvolutionCreatePayload(instanceName, GenerateUniqueEvolutionInstanceToken(), includeWebhook: false),
                    JsonSerializer.Serialize(new
                    {
                        instanceName,
                        name = instanceName,
                        integration = "WHATSAPP-BAILEYS"
                    })
                };

                HttpStatusCode? createFailureStatus = null;
                string? createFailureBody = null;
                var createSucceeded = false;

                var duplicateTokenRetried = false;

                for (var attemptIndex = 0; attemptIndex < createPayloads.Count; attemptIndex++)
                {
                    var createPayload = createPayloads[attemptIndex];
                    try
                    {
                        using var createReq = new HttpRequestMessage(HttpMethod.Post, $"{evoBase}/instance/create");
                        createReq.Headers.Add("apikey", apiKey);
                        createReq.Content = new StringContent(createPayload, Encoding.UTF8, "application/json");
                        using var createResp = await httpClient.SendAsync(createReq);
                        var createBody = await createResp.Content.ReadAsStringAsync();

                        _logger.LogInformation(
                            "Evolution Go create instance '{Instance}' tentativa {Attempt} → {StatusCode}",
                            instanceName,
                            attemptIndex + 1,
                            createResp.StatusCode);

                        if (salvarLogCompleto)
                        {
                            _logger.LogInformation(
                                "QrCode [logCompleto] create instance | instance={Instance} | attempt={Attempt} | requestPayload={Payload} | status={Status} | response={Response}",
                                instanceName,
                                attemptIndex + 1,
                                createPayload,
                                (int)createResp.StatusCode,
                                createBody);
                        }

                        if (createResp.IsSuccessStatusCode || IsEvolutionInstanceAlreadyExistsError(createBody))
                        {
                            createSucceeded = true;
                            break;
                        }

                        createFailureStatus = createResp.StatusCode;
                        createFailureBody = string.IsNullOrWhiteSpace(createBody) ? "(sem corpo)" : createBody;

                        if (!duplicateTokenRetried && IsEvolutionDuplicateTokenError(createBody))
                        {
                            duplicateTokenRetried = true;
                            createPayloads.Insert(
                                attemptIndex + 1,
                                BuildEvolutionCreatePayload(instanceName, GenerateUniqueEvolutionInstanceToken(), includeWebhook: false));
                            _logger.LogWarning(
                                "Evolution Go create instance '{Instance}' falhou por token duplicado; repetindo criação com novo token",
                                instanceName);
                            continue;
                        }

                        // Builds da Evolution Go podem rejeitar campos extras em /instance/create.
                        // Em 400 na tentativa completa, tenta payload mínimo documentado.
                        var shouldTryMinimalFallback = attemptIndex == 0 && createResp.StatusCode == HttpStatusCode.BadRequest;
                        if (!shouldTryMinimalFallback)
                            break;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Erro ao criar instância '{Instance}' na Evolution Go", instanceName);
                        return StatusCode(502, new
                        {
                            success = false,
                            error = $"Falha ao criar instância: erro em POST /instance/create: {GetEvolutionConnectionErrorMessage(ex)}"
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        return StatusCode(504, new
                        {
                            success = false,
                            error = "Falha ao criar instância: timeout em POST /instance/create"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro inesperado ao criar instância '{Instance}' na Evolution Go", instanceName);
                        return StatusCode(502, new
                        {
                            success = false,
                            error = $"Falha ao criar instância: erro em POST /instance/create: {ex.Message}"
                        });
                    }
                }

                if (!createSucceeded)
                {
                    var statusCode = createFailureStatus ?? HttpStatusCode.BadGateway;
                    return StatusCode((int)statusCode, new
                    {
                        success = false,
                        error = $"Falha ao criar instância: {(int)statusCode} em POST /instance/create: {createFailureBody ?? "(sem corpo)"}"
                    });
                }

                resolvedInstance = await TryResolveEvolutionInstanceAsync(httpClient, evoBase, apiKey, instanceName);
                resolvedApiKey = string.IsNullOrWhiteSpace(resolvedInstance?.Token) ? null : resolvedInstance.Token;
                if (resolvedInstance is null)
                {
                    return StatusCode(502, new
                    {
                        success = false,
                        error = $"Falha ao criar instância: instância '{instanceName}' não encontrada após POST /instance/create (GET /instance/fetchInstances?instanceName={Uri.EscapeDataString(instanceName)})."
                    });
                }
            }

            // Fallback: usa a chave mestre se não conseguiu resolver o token da instância.
            resolvedApiKey ??= apiKey;

            if (IsPlausibleQrCodeBase64(resolvedInstance?.QrCodeBase64))
            {
                var imageUrl = await PersistQrCodeImageAsync(
                    resolvedInstance!.Name,
                    resolvedInstance.QrCodeBase64);
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    _logger.LogWarning(
                        "Evolution Go QR code de fetchInstances para '{Instance}' não pôde ser persistido",
                        resolvedInstance.Name);
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        success = false,
                        error = "O QR Code foi recebido, mas não pôde ser salvo no servidor."
                    });
                }

                _logger.LogInformation(
                    "Evolution Go QR code '{Instance}' reaproveitado de fetchInstances e salvo em {ImageUrl} ({Bytes} bytes)",
                    resolvedInstance!.Name,
                    imageUrl,
                    resolvedInstance!.QrCodeBase64!.Length);
                return Ok(new { success = true, base64 = resolvedInstance.QrCodeBase64, imageUrl });
            }

            // 2. Obter o QR Code da instância.
            HttpResponseMessage? qrResp = null;
            try
            {
                var effectiveInstanceName = resolvedInstance?.Name ?? instanceName;
                var encodedInstanceName = Uri.EscapeDataString(effectiveInstanceName);
                var qrEndpoints = new[]
                {
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/{encodedInstanceName}/qrcode"),
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/qrcode/{encodedInstanceName}"),
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/{encodedInstanceName}/qr"),
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/connect/{encodedInstanceName}"),
                    // Evolution Go (versões recentes) expõe QR em /instance/qr
                    // e aceita instanceName na query quando a chave é global.
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/qr?instanceName={encodedInstanceName}"),
                    new EvolutionQrEndpointAttempt(HttpMethod.Get, $"{evoBase}/instance/qr")
                };
                var qrApiKeys = string.Equals(resolvedApiKey, apiKey, StringComparison.OrdinalIgnoreCase)
                    ? new[] { resolvedApiKey }
                    : new[] { resolvedApiKey, apiKey };

                HttpStatusCode? qrFailureStatus = null;
                string? qrFailureBody = null;
                string? qrFailureMethodAndPath = null;
                foreach (var endpoint in qrEndpoints)
                {
                    foreach (var candidateApiKey in qrApiKeys)
                    {
                        using var qrReq = new HttpRequestMessage(endpoint.Method, endpoint.Uri);
                        qrReq.Headers.Add("apikey", candidateApiKey);
                        var response = await httpClient.SendAsync(qrReq);
                        var methodAndPath = BuildMethodAndPath(endpoint.Method, qrReq.RequestUri);
                        var responseBody = await response.Content.ReadAsStringAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation(
                                "Evolution Go QR code tentativa {MethodAndPath} para '{Instance}' retornou {Status} | bytes={Bytes}",
                                methodAndPath,
                                effectiveInstanceName,
                                response.StatusCode,
                                responseBody.Length);
                            qrResp = response;
                            break;
                        }

                        if (IsEvolutionSessionAlreadyLoggedIn(responseBody))
                        {
                            _logger.LogInformation(
                                "Evolution Go reports instance '{Instance}' is already connected while requesting QR Code",
                                effectiveInstanceName);
                            return Ok(new
                            {
                                success = true,
                                connected = true,
                                message = "A instância já está conectada. Você pode salvar o canal."
                            });
                        }

                        qrFailureStatus = response.StatusCode;
                        qrFailureBody = responseBody;
                        qrFailureMethodAndPath = methodAndPath;
                        _logger.LogInformation(
                            "Evolution Go QR code tentativa {MethodAndPath} para '{Instance}' retornou {Status} | body={Body}",
                            methodAndPath,
                            effectiveInstanceName,
                            response.StatusCode,
                            TruncateForLog(qrFailureBody));
                        response.Dispose();
                    }

                    if (qrResp is not null)
                        break;
                }

                if (qrResp is null && qrFailureStatus == HttpStatusCode.Unauthorized)
                {
                    // Evolution Go requires POST /instance/connect before GET /instance/qr
                    // in recent versions. Older versions expose the QR directly, so only
                    // perform this recovery after every legacy QR route rejected the token.
                    foreach (var candidateApiKey in qrApiKeys)
                    {
                        using var connectRequest = new HttpRequestMessage(HttpMethod.Post, $"{evoBase}/instance/connect");
                        connectRequest.Headers.Add("apikey", candidateApiKey);
                        connectRequest.Content = new StringContent(BuildEvolutionConnectPayload(), Encoding.UTF8, "application/json");

                        using var connectResponse = await httpClient.SendAsync(connectRequest);
                        var connectBody = await connectResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation(
                            "Evolution Go QR recovery POST /instance/connect for '{Instance}' returned {Status} | body={Body}",
                            effectiveInstanceName,
                            (int)connectResponse.StatusCode,
                            TruncateForLog(connectBody));

                        if (!connectResponse.IsSuccessStatusCode)
                            continue;

                        using var retryRequest = new HttpRequestMessage(HttpMethod.Get, $"{evoBase}/instance/qr");
                        retryRequest.Headers.Add("apikey", candidateApiKey);
                        var retryResponse = await httpClient.SendAsync(retryRequest);
                        var retryBody = await retryResponse.Content.ReadAsStringAsync();
                        if (retryResponse.IsSuccessStatusCode)
                        {
                            qrResp = retryResponse;
                            break;
                        }

                        qrFailureStatus = retryResponse.StatusCode;
                        qrFailureBody = retryBody;
                        qrFailureMethodAndPath = BuildMethodAndPath(HttpMethod.Get, retryRequest.RequestUri);
                        retryResponse.Dispose();
                    }
                }

                if (qrResp is null || !qrResp.IsSuccessStatusCode)
                {
                    var status = qrFailureStatus ?? HttpStatusCode.BadGateway;
                    var errBody = qrFailureBody ?? "Nenhum endpoint de QR Code retornou sucesso.";
                    var endpointSummary = qrFailureMethodAndPath is null
                        ? $"HTTP {(int)status}"
                        : $"{(int)status} em {qrFailureMethodAndPath}";
                    _logger.LogWarning(
                        "Evolution Go QR code '{Instance}' falhou na última tentativa {MethodAndPath} → {Status}: {Body}",
                        effectiveInstanceName,
                        qrFailureMethodAndPath,
                        status,
                        errBody);
                    return StatusCode((int)status, new
                    {
                        success = false,
                        error = $"Erro ao obter QR Code: {endpointSummary}: {errBody}"
                    });
                }

                var qrBody = await qrResp.Content.ReadAsStringAsync();
                _logger.LogInformation("Evolution Go QR code '{Instance}' obtido com sucesso ({Bytes} bytes)", effectiveInstanceName, qrBody.Length);

                if (salvarLogCompleto)
                {
                    _logger.LogInformation(
                        "QrCode [logCompleto] qrcode response | instance={Instance} | status={Status} | bytes={Bytes}",
                        effectiveInstanceName, (int)qrResp.StatusCode, qrBody.Length);
                }

                var (bodyIsJson, base64) = TryExtractQrCodeBase64WithMeta(qrBody);

                // Se o corpo NÃO for JSON, pode ser um base64 cru retornado diretamente
                // pela Evolution. Caso contrário, JAMAIS devolver o corpo JSON como base64
                // (isso gera imagens quebradas no frontend porque "data:image/png;base64,{JSON}"
                // não é uma imagem válida).
                if (string.IsNullOrWhiteSpace(base64) && !bodyIsJson)
                    base64 = qrBody.Trim();

                if (!IsPlausibleQrCodeBase64(base64))
                {
                    _logger.LogWarning(
                        "Evolution Go '{Instance}' respondeu sem QR Code utilizável. body={Body}",
                        effectiveInstanceName,
                        TruncateForLog(qrBody));
                    _logger.LogInformation(
                        "Evolution Go '{Instance}' respondeu sem QR Code utilizável ({Bytes} bytes). Retornando 202 para permitir retry.",
                        effectiveInstanceName, qrBody.Length);
                    return StatusCode(StatusCodes.Status202Accepted, new
                    {
                        success = false,
                        error = "QR Code ainda não está disponível. Aguarde alguns segundos e tente novamente."
                    });
                }

                var imageUrl = await PersistQrCodeImageAsync(effectiveInstanceName, base64);
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    _logger.LogWarning(
                        "Evolution Go QR code '{Instance}' foi recebido, mas não pôde ser persistido",
                        effectiveInstanceName);
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        success = false,
                        error = "O QR Code foi recebido, mas não pôde ser salvo no servidor."
                    });
                }

                _logger.LogInformation(
                    "Evolution Go QR code '{Instance}' persistido em {ImageUrl} ({Bytes} bytes)",
                    effectiveInstanceName,
                    imageUrl,
                    base64?.Length ?? 0);

                return Ok(new { success = true, base64, imageUrl });
            }
            finally
            {
                qrResp?.Dispose();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao chamar Evolution Go para QR Code");
            return StatusCode(502, new { success = false, error = GetEvolutionConnectionErrorMessage(ex) });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "Timeout ao obter QR Code da Evolution Go API" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar QR Code");
            return StatusCode(500, new { success = false, error = "Erro interno ao gerar QR Code" });
        }
    }

    /// <summary>
    /// Gera o Pairing Code para conectar o WhatsApp Não Oficial via Evolution Go.
    /// O código de pareamento é inserido no WhatsApp: Configurações → Aparelhos conectados → Conectar com número.
    /// </summary>
    [HttpPost("pairingcode")]
    public async Task<IActionResult> GeneratePairingCode([FromBody] PairingCodeGenerateRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);
            if (farmacia == null)
                return NotFound(new { success = false, error = "Farmácia não encontrada" });

            if (string.IsNullOrWhiteSpace(request.InstanceName))
                return BadRequest(new { success = false, error = "Nome da instância é obrigatório" });
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { success = false, error = "Número de telefone é obrigatório" });

            var persistedApiUrl = farmacia.Integracoes?.WhatsAppApiUrl?.Trim();
            var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida." });
            evoBase = evoBase.TrimEnd('/');

            var instanceName = NormalizeInstanceName(request.InstanceName);
            var phoneDigits = new string(request.PhoneNumber.Where(char.IsDigit).ToArray());
            if (!phoneDigits.StartsWith("55", StringComparison.Ordinal) || phoneDigits.Length is < 12 or > 13)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Informe o telefone completo com DDI 55, DDD e número (ex.: 5562999880839)."
                });
            }

            _logger.LogInformation(
                "GeneratePairingCode: inicio | farmacia={FarmaciaId} | instance={Instance} | evoBase={EvoBase} | phoneLength={PhoneLength} | requestApiUrl={RequestApiUrl}",
                farmaciaId,
                instanceName,
                evoBase,
                phoneDigits.Length,
                TruncateForLog(request.ApiUrl));
            var persistedApiKey = farmacia.Integracoes?.WhatsAppApiKey?.Trim();
            var apiKey = ResolveEvolutionApiKey(request.ApiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia" });

            // Normalizar o número: somente dígitos, sem prefixos especiais
            if (phoneDigits.Length < 10)
                return BadRequest(new { success = false, error = "Número de telefone inválido. Use o formato DDI+DDD+número (ex.: 5511999999999)" });

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            var resolvedInstance = await TryResolveEvolutionInstanceAsync(httpClient, evoBase, apiKey, instanceName);
            var pairingApiKey = string.IsNullOrWhiteSpace(resolvedInstance?.Token)
                ? apiKey
                : resolvedInstance.Token!;

            // Evolution Go precisa iniciar o cliente antes de gerar QR ou código de
            // pareamento. A chamada é idempotente quando a instância já está ativa.
            using (var connectRequest = new HttpRequestMessage(HttpMethod.Post, $"{evoBase}/instance/connect"))
            {
                connectRequest.Headers.Add("apikey", pairingApiKey);
                connectRequest.Content = new StringContent(BuildEvolutionConnectPayload(), Encoding.UTF8, "application/json");
                using var connectResponse = await httpClient.SendAsync(connectRequest);
                var connectBody = await connectResponse.Content.ReadAsStringAsync();
                _logger.LogInformation(
                    "GeneratePairingCode: POST /instance/connect para {Instance} retornou {Status} | body={Body}",
                    instanceName,
                    (int)connectResponse.StatusCode,
                    TruncateForLog(connectBody));
            }

            // O endpoint oficial da Evolution Go é /instance/pair e recebe "phone".
            // Mantemos os formatos legados como fallback para versões anteriores.
            var pairingEndpoints = new (string Endpoint, string Payload)[]
            {
                ($"{evoBase}/instance/pair", JsonSerializer.Serialize(new { phone = phoneDigits })),
                ($"{evoBase}/instance/pairingCode/{Uri.EscapeDataString(instanceName)}", JsonSerializer.Serialize(new { number = phoneDigits })),
                ($"{evoBase}/instance/{Uri.EscapeDataString(instanceName)}/pairingCode", JsonSerializer.Serialize(new { number = phoneDigits }))
            };

            foreach (var attempt in pairingEndpoints)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, attempt.Endpoint);
                req.Headers.Add("apikey", pairingApiKey);
                req.Content = new StringContent(attempt.Payload, Encoding.UTF8, "application/json");
                HttpResponseMessage resp;
                try
                {
                    _logger.LogInformation(
                        "GeneratePairingCode: tentando endpoint {Endpoint} para instance={Instance}",
                        attempt.Endpoint,
                        instanceName);
                    resp = await httpClient.SendAsync(req);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "GeneratePairingCode: excecao ao chamar endpoint {Endpoint} para instance={Instance}",
                        attempt.Endpoint,
                        instanceName);
                    continue;
                }

                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "GeneratePairingCode: endpoint {Endpoint} para instance={Instance} retornou {Status} | body={Body}",
                        attempt.Endpoint,
                        instanceName,
                        resp.StatusCode,
                        TruncateForLog(body));
                    continue;
                }

                _logger.LogInformation("PairingCode response | instance={Instance} | status={Status} | body={Body}",
                    instanceName, resp.StatusCode, TruncateForLog(body));

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    // Tentar extrair o código de diferentes formatos de resposta
                    string? code = null;
                    if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                        code = codeEl.GetString();
                    else if (root.TryGetProperty("pairingCode", out var pairingCodeEl) && pairingCodeEl.ValueKind == JsonValueKind.String)
                        code = pairingCodeEl.GetString();
                    else if (root.TryGetProperty("pairing_code", out var pairingCodeSnakeEl) && pairingCodeSnakeEl.ValueKind == JsonValueKind.String)
                        code = pairingCodeSnakeEl.GetString();
                    else if (root.TryGetProperty("pairCode", out var pairCodeEl) && pairCodeEl.ValueKind == JsonValueKind.String)
                        code = pairCodeEl.GetString();
                    else if (root.TryGetProperty("PairingCode", out var pairingCodePascalEl) && pairingCodePascalEl.ValueKind == JsonValueKind.String)
                        code = pairingCodePascalEl.GetString();
                    else if (root.TryGetProperty("data", out var dataEl))
                    {
                        if (dataEl.TryGetProperty("code", out var dataCodeEl) && dataCodeEl.ValueKind == JsonValueKind.String)
                            code = dataCodeEl.GetString();
                        else if (dataEl.TryGetProperty("pairingCode", out var dataPairingEl) && dataPairingEl.ValueKind == JsonValueKind.String)
                            code = dataPairingEl.GetString();
                        else if (dataEl.TryGetProperty("pairing_code", out var dataPairingSnakeEl) && dataPairingSnakeEl.ValueKind == JsonValueKind.String)
                            code = dataPairingSnakeEl.GetString();
                        else if (dataEl.TryGetProperty("pairCode", out var dataPairCodeEl) && dataPairCodeEl.ValueKind == JsonValueKind.String)
                            code = dataPairCodeEl.GetString();
                        else if (dataEl.TryGetProperty("PairingCode", out var dataPairingPascalEl) && dataPairingPascalEl.ValueKind == JsonValueKind.String)
                            code = dataPairingPascalEl.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        _logger.LogInformation(
                            "GeneratePairingCode: codigo extraido com sucesso para instance={Instance} | code={Code}",
                            instanceName,
                            code);
                        return Ok(new { success = true, code });
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "GeneratePairingCode: body nao era JSON valido no endpoint {Endpoint} para instance={Instance} | body={Body}",
                        attempt.Endpoint,
                        instanceName,
                        TruncateForLog(body));
                }
            }

            _logger.LogWarning(
                "GeneratePairingCode: falha final para instance={Instance} | evoBase={EvoBase}",
                instanceName,
                evoBase);
            return StatusCode(502, new { success = false, error = "Não foi possível gerar o código de pareamento. Verifique se a instância está criada e aguardando conexão." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "Timeout ao obter código de pareamento da Evolution Go API" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar Pairing Code");
            return StatusCode(500, new { success = false, error = "Erro interno ao gerar código de pareamento" });
        }
    }

    /// <summary>
    /// Consulta o status da instância WhatsApp Não Oficial na Evolution Go.
    /// Retorna o status: OPEN (conectado), CLOSE (desconectado) ou QRCODE (aguardando scan).
    /// </summary>
    [HttpGet("whatsapp/status")]
    public async Task<IActionResult> GetWhatsAppInstanceStatus([FromQuery] string instanceName, [FromQuery] string? apiUrl, [FromQuery] string? apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                return BadRequest(new { success = false, error = "instanceName é obrigatório" });

            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var persistedApiUrl = await GetStoredNonOfficialWhatsAppApiUrlAsync(farmaciaId);
            var resolvedApiUrl = ResolveEvolutionApiUrl(apiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida." });

            var persistedApiKey = await GetStoredNonOfficialWhatsAppApiKeyAsync(farmaciaId);
            var resolvedApiKey = ResolveEvolutionApiKey(apiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(resolvedApiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia" });

            var salvarLogCompleto = await GetSalvarLogCompletoAsync(farmaciaId);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            var normalizedInstanceName = NormalizeInstanceName(instanceName);
            var resolvedInstance = await TryResolveEvolutionInstanceAsync(httpClient, evoBase, resolvedApiKey.Trim(), normalizedInstanceName);
            var effectiveInstanceName = resolvedInstance?.Name ?? normalizedInstanceName;
            var instanceApiKey = string.IsNullOrWhiteSpace(resolvedInstance?.Token)
                ? resolvedApiKey.Trim()
                : resolvedInstance.Token!;

            var (status, connectionStateBody) = await TryGetEvolutionConnectionStatusAsync(
                httpClient,
                evoBase,
                effectiveInstanceName,
                instanceApiKey);

            if (string.IsNullOrWhiteSpace(status) &&
                !string.Equals(instanceApiKey, resolvedApiKey.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var fallbackConnection = await TryGetEvolutionConnectionStatusAsync(
                    httpClient,
                    evoBase,
                    effectiveInstanceName,
                    resolvedApiKey.Trim());

                if (!string.IsNullOrWhiteSpace(fallbackConnection.Status))
                {
                    status = fallbackConnection.Status;
                    connectionStateBody = fallbackConnection.RawBody;
                }
            }

            if (salvarLogCompleto && !string.IsNullOrWhiteSpace(connectionStateBody))
            {
                _logger.LogInformation(
                    "WhatsAppStatus [logCompleto] | instance={Instance} | response={Response}",
                    instanceName, connectionStateBody);
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                var (instances, fetchBody) = await FetchEvolutionInstancesAsync(httpClient, evoBase, resolvedApiKey.Trim(), normalizedInstanceName);
                var instance = TryFindMatchingEvolutionInstance(instances, normalizedInstanceName);

                if (salvarLogCompleto)
                {
                    _logger.LogInformation(
                        "WhatsAppStatus [logCompleto] fallback fetchInstances | instance={Instance} | response={Response}",
                        instanceName, fetchBody);
                }

                if (instance is null)
                    return Ok(new { success = true, status = "NOT_FOUND" });

                status = instance.Status;
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                var statusEndpoint = await TryGetEvolutionStatusEndpointAsync(
                    httpClient,
                    evoBase,
                    instanceApiKey);
                if (!string.IsNullOrWhiteSpace(statusEndpoint.Status))
                {
                    status = statusEndpoint.Status;
                    connectionStateBody = statusEndpoint.RawBody;
                }
            }

            if (string.IsNullOrWhiteSpace(status))
                status = "UNKNOWN";

            return Ok(new { success = true, status });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { success = false, error = GetEvolutionConnectionErrorMessage(ex) });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "Timeout ao consultar status" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status da instância");
            return StatusCode(500, new { success = false, error = "Erro interno" });
        }
    }

    [HttpGet("whatsapp/instances")]
    public async Task<IActionResult> ListWhatsAppInstances([FromQuery] string? apiUrl, [FromQuery] string? apiKey)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada", instances = new List<object>() });

            var persistedApiUrl = await GetStoredNonOfficialWhatsAppApiUrlAsync(farmaciaId);
            var resolvedApiUrl = ResolveEvolutionApiUrl(apiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida", instances = new List<object>() });

            var persistedApiKey = await GetStoredNonOfficialWhatsAppApiKeyAsync(farmaciaId);
            var resolvedApiKey = ResolveEvolutionApiKey(apiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(resolvedApiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia", instances = new List<object>() });

            var salvarLogCompleto = await GetSalvarLogCompletoAsync(farmaciaId);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            var (instancesData, body) = await FetchEvolutionInstancesAsync(httpClient, evoBase, resolvedApiKey.Trim());

            if (salvarLogCompleto)
            {
                _logger.LogInformation(
                    "WhatsAppListInstances [logCompleto] | url={Url} | status={Status} | response={Response}",
                    evoBase, 200, body);
            }

            var instances = instancesData
                .Select(instance => new { name = instance.Name, status = instance.Status ?? "unknown" })
                .Cast<object>()
                .ToList();

            return Ok(new { success = true, instances });
        }
        catch (HttpRequestException ex)
        {
            return Ok(new { success = false, error = GetEvolutionConnectionErrorMessage(ex), instances = new List<object>() });
        }
        catch (TaskCanceledException)
        {
            return Ok(new { success = false, error = "Timeout ao listar instâncias", instances = new List<object>() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar instâncias da Evolution");
            return Ok(new { success = false, error = "Erro interno", instances = new List<object>() });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChannel(int id)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaId);

            if (farmacia?.Integracoes == null)
                return NotFound(new { success = false, error = "Integrações não encontradas" });

            await _context.Entry(farmacia.Integracoes)
                .Collection(i => i.Canais!)
                .LoadAsync();

            var canal = farmacia.Integracoes.Canais?.FirstOrDefault(c => c.Id == id);
            if (canal == null)
                return NotFound(new { success = false, error = "Canal não encontrado" });

            var isNonOfficialWhatsApp = canal.Tipo == 1 && !canal.IsOfficial;
            if (isNonOfficialWhatsApp)
            {
                var fallbackCanal = farmacia.Integracoes.Canais?
                    .FirstOrDefault(c => c.Id != id && c.Tipo == 1 && !c.IsOfficial);

                if (fallbackCanal != null &&
                    !string.IsNullOrWhiteSpace(fallbackCanal.InstanceName) &&
                    !string.IsNullOrWhiteSpace(fallbackCanal.ApiUrl) &&
                    !string.IsNullOrWhiteSpace(fallbackCanal.ApiKey))
                {
                    SyncIntegracoesCanalNaoOficial(
                        farmacia.Integracoes,
                        fallbackCanal.InstanceName.Trim(),
                        fallbackCanal.ApiUrl.Trim(),
                        fallbackCanal.ApiKey.Trim());
                }
                else
                {
                    ResetIntegracoesCanalNaoOficial(farmacia.Integracoes);
                }
            }

            _context.Canais.Remove(canal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Canal {Id} deletado para farmácia {FarmaciaId}", id, farmaciaId);

            return Ok(new
            {
                success = true,
                message = "Canal deletado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar canal");
            return StatusCode(500, new { success = false, error = "Erro ao deletar canal" });
        }
    }

    [HttpGet("whatsapp/instance-info")]
    public async Task<IActionResult> GetWhatsAppInstanceInfo([FromQuery] string instanceName, [FromQuery] string? apiUrl, [FromQuery] string? apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                return BadRequest(new { success = false, error = "instanceName é obrigatório" });

            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var persistedApiUrl = await GetStoredNonOfficialWhatsAppApiUrlAsync(farmaciaId);
            var resolvedApiUrl = ResolveEvolutionApiUrl(apiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida. Informe a URL base da Evolution Go." });

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var persistedApiKey = await GetStoredNonOfficialWhatsAppApiKeyAsync(farmaciaId);
            var resolvedApiKey = ResolveEvolutionApiKey(apiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(resolvedApiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia" });

            var normalizedInstanceName = NormalizeInstanceName(instanceName);
            var (instances, _) = await FetchEvolutionInstancesAsync(httpClient, evoBase, resolvedApiKey.Trim(), normalizedInstanceName);
            var instance = TryFindMatchingEvolutionInstance(instances, normalizedInstanceName);

            if (instance is null)
            {
                string? status = null;
                return Ok(new { success = true, exists = false, status });
            }

            return Ok(new { success = true, exists = true, status = instance.Status });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GetWhatsAppInstanceInfo: falha de rede para instância '{Instance}'", instanceName);
            return StatusCode(502, new { success = false, error = GetEvolutionConnectionErrorMessage(ex) });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "Timeout ao consultar instância na Evolution Go API" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar informações da instância");
            return StatusCode(500, new { success = false, error = "Erro interno" });
        }
    }

    /// <summary>
    /// Garante que a instância Evolution Go existe, criando-a com todas as permissões
    /// de mensagens e mídia se ainda não existir.
    /// </summary>
    [HttpPost("whatsapp/ensure-instance")]
    public async Task<IActionResult> EnsureWhatsAppInstance([FromBody] QrCodeGenerateRequest request)
    {
        try
        {
            var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            if (string.IsNullOrWhiteSpace(request.InstanceName))
                return BadRequest(new { success = false, error = "Nome da instância é obrigatório" });
            var persistedApiUrl = await GetStoredNonOfficialWhatsAppApiUrlAsync(farmaciaId);
            var resolvedApiUrl = ResolveEvolutionApiUrl(request.ApiUrl, persistedApiUrl);
            var evoBase = DeriveEvolutionGoBaseUrl(resolvedApiUrl);
            if (evoBase == null)
                return BadRequest(new { success = false, error = "URL da API inválida. Informe a URL base da Evolution Go." });

            var instanceName = NormalizeInstanceName(request.InstanceName);
            _logger.LogInformation(
                "GenerateQrCode: inicio | farmacia={FarmaciaId} | instance={Instance} | evoBase={EvoBase} | requestApiUrl={RequestApiUrl} | persistedApiUrl={PersistedApiUrl}",
                farmaciaId,
                instanceName,
                evoBase,
                TruncateForLog(request.ApiUrl),
                TruncateForLog(persistedApiUrl));
            var persistedApiKey = await GetStoredNonOfficialWhatsAppApiKeyAsync(farmaciaId);
            var apiKey = ResolveEvolutionApiKey(request.ApiKey, persistedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new { success = false, error = "API Key não encontrada para esta farmácia" });
            var salvarLogCompleto = await GetSalvarLogCompletoAsync(farmaciaId);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // 1. Verificar se a instância já existe
            try
            {
                var instanceExists = await TryResolveEvolutionInstanceApiKeyAsync(httpClient, evoBase, apiKey, instanceName) is not null;
                if (instanceExists)
                {
                    if (request.ForceRecreate)
                    {
                        var deleteResult = await TryDeleteEvolutionInstanceAsync(httpClient, evoBase, instanceName, apiKey);
                        if (!deleteResult.Success)
                        {
                            var deleteError = string.IsNullOrWhiteSpace(deleteResult.ErrorBody)
                                ? "Não foi possível remover a instância existente antes da recriação."
                                : $"Não foi possível remover a instância existente: {deleteResult.ErrorBody}";
                            return StatusCode(502, new { success = false, error = deleteError });
                        }
                    }
                    else
                    {
                        _logger.LogInformation("EnsureWhatsAppInstance: instância '{Instance}' já existe", instanceName);
                        // Even if instance already exists, try to update webhook URL
                        await TryUpdateEvolutionWebhookAsync(httpClient, evoBase, apiKey, instanceName);
                        return Ok(new { success = true, created = false, alreadyExists = true, message = "Instância já existe" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EnsureWhatsAppInstance: erro ao verificar existência da instância '{Instance}'", instanceName);
            }

            // 2. Criar a instância com todas as permissões de mensagens e mídia
            var duplicateTokenRetried = false;
            while (true)
            {
                var createPayload = BuildEvolutionCreatePayload(instanceName, GenerateUniqueEvolutionInstanceToken(), includeWebhook: true);

                using var createReq = new HttpRequestMessage(HttpMethod.Post, $"{evoBase}/instance/create");
                createReq.Headers.Add("apikey", apiKey);
                createReq.Content = new StringContent(createPayload, Encoding.UTF8, "application/json");
                var createResp = await httpClient.SendAsync(createReq);

                var createBody = await createResp.Content.ReadAsStringAsync();

                if (salvarLogCompleto)
                {
                    _logger.LogInformation(
                        "EnsureInstance [logCompleto] | instance={Instance} | requestPayload={Payload} | status={Status} | response={Response}",
                        instanceName, createPayload, (int)createResp.StatusCode, createBody);
                }

                if (createResp.IsSuccessStatusCode)
                    break;

                if (IsEvolutionInstanceAlreadyExistsError(createBody))
                {
                    _logger.LogInformation(
                        "EnsureWhatsAppInstance: instância '{Instance}' já existia durante create (status {Status})",
                        instanceName,
                        createResp.StatusCode);
                    return Ok(new { success = true, created = false, alreadyExists = true, message = "Instância já existe" });
                }

                if (!duplicateTokenRetried && IsEvolutionDuplicateTokenError(createBody))
                {
                    duplicateTokenRetried = true;
                    _logger.LogWarning(
                        "EnsureWhatsAppInstance: token duplicado ao criar instância '{Instance}'; repetindo tentativa com novo token",
                        instanceName);
                    continue;
                }

                _logger.LogWarning("EnsureWhatsAppInstance: falha ao criar instância '{Instance}' → {Status}: {Body}",
                    instanceName, createResp.StatusCode, createBody);
                return StatusCode((int)createResp.StatusCode,
                    new { success = false, error = $"Falha ao criar instância na Evolution Go: {createBody}" });
            }

            _logger.LogInformation("EnsureWhatsAppInstance: instância '{Instance}' criada com sucesso", instanceName);

            // Configure webhook via connect endpoint (Evolution Go requires this for webhooks to work)
            await TryUpdateEvolutionWebhookAsync(httpClient, evoBase, apiKey, instanceName);

            return Ok(new { success = true, created = true, alreadyExists = false, message = "Instância criada com sucesso" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "EnsureWhatsAppInstance: falha de rede");
            return StatusCode(502, new { success = false, error = GetEvolutionConnectionErrorMessage(ex) });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, error = "Timeout ao criar instância na Evolution Go API" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnsureWhatsAppInstance: erro interno");
            return StatusCode(500, new { success = false, error = "Erro interno ao criar instância" });
        }
    }

    private static string GetDefaultChannelName(int type, bool isOfficial)
    {
       if (type == 2)
           return "Instagram";

       return isOfficial ? "WhatsApp Oficial" : "WhatsApp Não Oficial";
    }

    private async Task TryUpdateEvolutionWebhookAsync(HttpClient httpClient, string evoBase, string apiKey, string instanceName)
    {
        try
        {
            var instanceToken = await TryResolveEvolutionInstanceApiKeyAsync(httpClient, evoBase, apiKey, instanceName);
            if (string.IsNullOrWhiteSpace(instanceToken))
            {
                _logger.LogWarning("TryUpdateEvolutionWebhook: não foi possível resolver token da instância '{Instance}'", instanceName);
                return;
            }

            var connectPayload = BuildEvolutionConnectPayload();
            if (connectPayload == "{}")
            {
                _logger.LogWarning("TryUpdateEvolutionWebhook: Application:ApiUrl não configurado, webhook não atualizado");
                return;
            }

            using var connectReq = new HttpRequestMessage(HttpMethod.Post, $"{evoBase}/instance/connect");
            connectReq.Headers.Add("apikey", instanceToken);
            connectReq.Content = new StringContent(connectPayload, Encoding.UTF8, "application/json");
            using var connectResp = await httpClient.SendAsync(connectReq);
            var connectBody = await connectResp.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "TryUpdateEvolutionWebhook: POST /instance/connect para '{Instance}' retornou {Status}: {Body}",
                instanceName, (int)connectResp.StatusCode, connectBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryUpdateEvolutionWebhook: falha ao configurar webhook para '{Instance}'", instanceName);
        }
    }

    private string BuildEvolutionCreatePayload(string instanceName, string token, bool includeWebhook)
    {
        var payload = new Dictionary<string, object>
        {
            ["instanceName"] = instanceName,
            ["name"] = instanceName,
            ["token"] = token,
            ["integration"] = "WHATSAPP-BAILEYS"
        };

        if (includeWebhook)
        {
            var webhookUrl = (_configuration["Application:ApiUrl"]?.TrimEnd('/') ?? "http://localhost:5051") + "/api/whatsapp/webhook";
            payload["webhook"] = webhookUrl;
            payload["webhookByEvents"] = true;
            payload["events"] = new[]
            {
                "APPLICATION_STARTUP",
                "QRCODE_UPDATED",
                "MESSAGES_SET",
                "MESSAGES_UPSERT",
                "MESSAGES_UPDATE",
                "MESSAGES_DELETE",
                "SEND_MESSAGE",
                "CONTACTS_SET",
                "CONTACTS_UPSERT",
                "CONTACTS_UPDATE",
                "PRESENCE_UPDATE",
                "CHATS_SET",
                "CHATS_UPSERT",
                "CHATS_UPDATE",
                "CHATS_DELETE",
                "GROUPS_UPSERT",
                "GROUP_UPDATE",
                "GROUP_PARTICIPANTS_UPDATE",
                "CONNECTION_UPDATE",
                "CALL",
                "NEW_JWT_TOKEN"
            };
        }

        return JsonSerializer.Serialize(payload);
    }

    private string BuildEvolutionConnectPayload()
    {
        var apiUrl = _configuration["Application:ApiUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            return "{}";

        return JsonSerializer.Serialize(new
        {
            webhookUrl = $"{apiUrl}/api/whatsapp/webhook",
            subscribe = new[] { "ALL" }
        });
    }

    private static string GenerateUniqueEvolutionInstanceToken()
        => $"pharmazap-{Guid.NewGuid():N}";

    private static bool IsEvolutionDuplicateTokenError(string? body)
        => !string.IsNullOrWhiteSpace(body) &&
           (body.Contains("uni_instances_token", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("SQLSTATE 23505", StringComparison.OrdinalIgnoreCase));

    private string ResolveEvolutionApiKey(string? apiKey, string? persistedApiKey = null)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            return apiKey.Trim();

        if (!string.IsNullOrWhiteSpace(persistedApiKey))
            return persistedApiKey.Trim();

        return _configuration["GLOBAL_API_KEY"]?.Trim() ?? string.Empty;
    }

    private string ResolveEvolutionApiUrl(string? apiUrl, string? persistedApiUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(apiUrl))
            return apiUrl.Trim();

        if (!string.IsNullOrWhiteSpace(persistedApiUrl))
            return persistedApiUrl.Trim();

        return _configuration["WhatsApp:NonOfficialApiUrl"]?.Trim() ?? "http://localhost:8080";
    }

    private static string GetEvolutionConnectionErrorMessage(HttpRequestException ex)
    {
       var details = GetEvolutionConnectionErrorDetails(ex);
       return $"Não foi possível conectar à Evolution Go API: {details}";
    }

    private static string GetEvolutionConnectionErrorDetails(HttpRequestException ex)
    {
       if (ex.InnerException is AuthenticationException)
       {
           return "falha de SSL/TLS. Verifique se a URL usa o protocolo correto e se o certificado e confiavel.";
       }

       if (ex.InnerException is SocketException socketEx)
       {
           return socketEx.SocketErrorCode switch
           {
               SocketError.HostNotFound => "host nao encontrado. Verifique a URL informada.",
               SocketError.ConnectionRefused => "a conexao foi recusada. Verifique host, porta e se o servico esta em execucao.",
               SocketError.TimedOut => "tempo limite esgotado. Verifique a disponibilidade da Evolution Go API.",
               SocketError.NetworkUnreachable or SocketError.HostUnreachable => "a rede nao conseguiu alcancar o destino. Verifique a conectividade.",
               _ => socketEx.Message
           };
       }

       var message = ex.InnerException?.Message?.Trim();
       if (string.IsNullOrWhiteSpace(message))
           message = ex.Message.Trim();

       if (string.Equals(message, "An error occurred while sending the request.", StringComparison.OrdinalIgnoreCase))
       {
           return "a requisicao falhou ao sair do servidor. Verifique host, porta, HTTPS e certificado.";
       }

       return message!;
    }

    private static bool IsChannelConfigured(Canal canal)
    {
       if (canal.Tipo == 1 && canal.IsOfficial)
       {
           return !string.IsNullOrWhiteSpace(canal.PhoneNumberId) && 
                  !string.IsNullOrWhiteSpace(canal.AccessToken);
       }
        
       if (canal.Tipo == 1 && !canal.IsOfficial)
       {
           return !string.IsNullOrWhiteSpace(canal.ApiUrl) && 
                  !string.IsNullOrWhiteSpace(canal.ApiKey);
       }
        
        if (canal.Tipo == 2)
        {
            return canal.IsOfficial
                ? !string.IsNullOrWhiteSpace(canal.InstagramAccountId) &&
                  !string.IsNullOrWhiteSpace(canal.AccessToken)
                : !string.IsNullOrWhiteSpace(canal.InstagramAccountId) &&
                  !string.IsNullOrWhiteSpace(canal.ApiKey);
        }
        
       return false;
    }

    /// <summary>
    /// Valida a URL da Evolution API para WhatsApp Não Oficial.
    /// Aceita a raiz da Evolution Go (ex.: http://host:8080) e o formato legado com /api/whatsapp.
    /// </summary>
    private (bool IsValid, string ErrorMessage) ValidateEvolutionApiUrl(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return (false, "URL não pode estar vazia");

        var trimmedUrl = apiUrl.Trim().TrimEnd('/');

        // Verificar se contém '/webhook' (erro comum)
        if (trimmedUrl.Contains("/webhook", StringComparison.OrdinalIgnoreCase))
        {
            var correctedUrl = trimmedUrl.Replace("/webhook", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
            return (false, $"❌ URL contém '/webhook' inválido. Use apenas a URL base da Evolution Go, por exemplo: {correctedUrl}");
        }

        // Verificar se a URL é válida (contém protocolo e host)
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
            return (false, "❌ URL inválida. Formato esperado: http://host:8080 ou https://host:8080");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return (false, "❌ URL inválida. Use http:// ou https://");

        var normalizedPath = uri.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(normalizedPath) &&
            !normalizedPath.Equals("/", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.Equals("/api/whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            var suggestedBase = uri.GetLeftPart(UriPartial.Authority);
            return (false, $"❌ URL deve apontar para a raiz da Evolution Go (ex.: {suggestedBase}) ou usar o formato legado {suggestedBase}/api/whatsapp. Não use caminhos adicionais como '{normalizedPath}'.");
        }

        if (!uri.IsDefaultPort && uri.Port is not (6000 or 8080))
            _logger.LogWarning("⚠️ URL Evolution API usa porta incomum ({Port}). Verifique se está correto: {ApiUrl}", uri.Port, trimmedUrl);

        return (true, "");
    }

    /// <summary>
    /// Deriva a URL base da Evolution Go a partir da configuração armazenada.
    /// Aceita a raiz da Evolution Go e o formato legado "/api/whatsapp".
    /// </summary>
    private static string? DeriveEvolutionGoBaseUrl(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return null;

        var trimmed = apiUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return null;

        var normalizedPath = uri.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(normalizedPath) &&
            !normalizedPath.Equals("/", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.Equals("/api/whatsapp", StringComparison.OrdinalIgnoreCase))
            return null;

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string TruncateForLog(string? value, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }

    private async Task<string> ResolveEvolutionInstanceApiKeyAsync(HttpClient httpClient, string evoBase, string apiKey, string instanceName)
    {
        return await TryResolveEvolutionInstanceApiKeyAsync(httpClient, evoBase, apiKey, instanceName) ?? apiKey;
    }

    private async Task<string?> TryResolveEvolutionInstanceApiKeyAsync(HttpClient httpClient, string evoBase, string apiKey, string instanceName)
    {
        var instance = await TryResolveEvolutionInstanceAsync(httpClient, evoBase, apiKey, instanceName);
        return string.IsNullOrWhiteSpace(instance?.Token) ? null : instance.Token;
    }

    private async Task<EvolutionInstanceSummary?> TryResolveEvolutionInstanceAsync(HttpClient httpClient, string evoBase, string apiKey, string instanceName)
    {
        try
        {
            var (instances, _) = await FetchEvolutionInstancesAsync(httpClient, evoBase, apiKey, instanceName);
            return TryFindMatchingEvolutionInstance(instances, instanceName);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Falha ao resolver instância '{InstanceName}' pela Evolution Go", instanceName);
        }
        catch (HttpRequestException)
        {
        }

        return null;
    }

    private async Task<(List<EvolutionInstanceSummary> Instances, string RawBody)> FetchEvolutionInstancesAsync(
        HttpClient httpClient,
        string evoBase,
        string apiKey,
        string? instanceName = null)
    {
        var normalizedInstanceName = instanceName?.Trim();
        var fetchInstancesBaseUrl = $"{evoBase}/instance/fetchInstances";
        var requestUris = new[]
        {
            string.IsNullOrWhiteSpace(normalizedInstanceName)
                ? fetchInstancesBaseUrl
                : $"{fetchInstancesBaseUrl}?instanceName={Uri.EscapeDataString(normalizedInstanceName)}",
            $"{evoBase}/instance/all"
        };

        string? lastBody = null;
        HttpStatusCode? lastStatusCode = null;

        foreach (var requestUri in requestUris.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("apikey", apiKey);

            using var response = await httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                lastBody = body;
                lastStatusCode = response.StatusCode;
                continue;
            }

            return (ParseEvolutionInstances(body), body);
        }

        var fallbackMessage = lastStatusCode.HasValue
            ? $"Erro ao listar instâncias na Evolution Go API ({(int)lastStatusCode.Value})"
            : "Nenhuma resposta recebida da Evolution Go API ao listar instâncias";
        throw new HttpRequestException(lastBody ?? fallbackMessage);
    }

    private static async Task<(string? Status, string RawBody)> TryGetEvolutionConnectionStatusAsync(
        HttpClient httpClient,
        string evoBase,
        string instanceName,
        string apiKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{evoBase}/instance/connectionState/{Uri.EscapeDataString(instanceName)}");
        request.Headers.Add("apikey", apiKey);

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (null, body);

        return (TryExtractEvolutionStatus(body), body);
    }

    private static async Task<(string? Status, string RawBody)> TryGetEvolutionStatusEndpointAsync(
        HttpClient httpClient,
        string evoBase,
        string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{evoBase}/instance/status");
        request.Headers.Add("apikey", apiKey);

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (null, body);

        return (TryExtractEvolutionStatus(body), body);
    }

    private static async Task<(bool Success, string? ErrorBody)> TryDeleteEvolutionInstanceAsync(
        HttpClient httpClient,
        string evoBase,
        string instanceName,
        string apiKey)
    {
        var escapedInstance = Uri.EscapeDataString(instanceName);
        var attempts = new (HttpMethod Method, string Uri)[]
        {
            (HttpMethod.Delete, $"{evoBase}/instance/delete/{escapedInstance}"),
            (HttpMethod.Delete, $"{evoBase}/instance/logout/{escapedInstance}"),
            (HttpMethod.Get, $"{evoBase}/instance/logout/{escapedInstance}")
        };

        string? lastBody = null;
        foreach (var attempt in attempts)
        {
            using var req = new HttpRequestMessage(attempt.Method, attempt.Uri);
            req.Headers.Add("apikey", apiKey);
            using var resp = await httpClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound)
                return (true, null);

            lastBody = body;
        }

        return (false, lastBody);
    }

    private static bool IsEvolutionInstanceAlreadyExistsError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        if (body.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var errorEl) &&
                errorEl.ValueKind == JsonValueKind.String)
            {
                var errorText = errorEl.GetString();
                return !string.IsNullOrWhiteSpace(errorText) &&
                    errorText.Contains("already exists", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static List<EvolutionInstanceSummary> ParseEvolutionInstances(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var items = new List<JsonElement>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(root.EnumerateArray());
        }
        else if (root.TryGetProperty("data", out var dataEl))
        {
            if (dataEl.ValueKind == JsonValueKind.Array)
                items.AddRange(dataEl.EnumerateArray());
            else if (dataEl.ValueKind == JsonValueKind.Object)
                items.Add(dataEl);
        }
        else if (root.TryGetProperty("instances", out var instancesEl) && instancesEl.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(instancesEl.EnumerateArray());
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            items.Add(root);
        }

        return items
            .Select(item => TryBuildEvolutionInstanceSummary(item))
            .Where(item => item is not null)
            .Cast<EvolutionInstanceSummary>()
            .ToList();
    }

    private static EvolutionInstanceSummary? TryBuildEvolutionInstanceSummary(JsonElement item)
    {
        if (!TryGetInstanceName(item, out var name))
            return null;

        return new EvolutionInstanceSummary(
            name!,
            TryExtractEvolutionStatus(item),
            TryExtractEvolutionInstanceToken(item),
            TryExtractQrCodeBase64(item));
    }

    private static string? TryExtractQrCodeBase64(string body)
        => TryExtractQrCodeBase64WithMeta(body).Base64;

    private static string? TryExtractQrCodeBase64(JsonElement item)
    {
        if (item.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            return null;

        return TryExtractQrCodeBase64WithMeta(item).Base64;
    }

    /// <summary>
    /// Tenta extrair o base64 do QR Code do corpo da resposta da Evolution Go,
    /// devolvendo também se o corpo pôde ser interpretado como JSON. Esse metadado
    /// é usado para diferenciar "resposta JSON sem QR pronto" (retriável) de
    /// "resposta em texto puro contendo o base64 direto" (aceitável como fallback).
    /// </summary>
    private static (bool IsJson, string? Base64) TryExtractQrCodeBase64WithMeta(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (false, null);

        try
        {
            using var doc = JsonDocument.Parse(body);
            return TryExtractQrCodeBase64WithMeta(doc.RootElement);
        }
        catch (JsonException)
        {
        }

        return (false, null);
    }

    private static (bool IsJson, string? Base64) TryExtractQrCodeBase64WithMeta(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return (true, null);

        if (root.TryGetProperty("base64", out var base64El))
            return (true, NormalizeExtractedBase64(base64El));

        if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("base64", out var dataBase64El))
            return (true, NormalizeExtractedBase64(dataBase64El));

        if (root.TryGetProperty("data", out dataEl) && dataEl.TryGetProperty("qrcode", out var dataQrCodeEl))
        {
            if (dataQrCodeEl.ValueKind == JsonValueKind.String)
                return (true, NormalizeExtractedBase64(dataQrCodeEl));

            if (dataQrCodeEl.ValueKind == JsonValueKind.Object && dataQrCodeEl.TryGetProperty("base64", out var dataQrCodeBase64El))
                return (true, NormalizeExtractedBase64(dataQrCodeBase64El));
        }

        if (root.TryGetProperty("qrcode", out var qrCodeEl))
        {
            if (qrCodeEl.ValueKind == JsonValueKind.String)
                return (true, NormalizeExtractedBase64(qrCodeEl));

            if (qrCodeEl.ValueKind == JsonValueKind.Object && qrCodeEl.TryGetProperty("base64", out var qrCodeBase64El))
                return (true, NormalizeExtractedBase64(qrCodeBase64El));
        }

        if (root.TryGetProperty("instance", out var instanceEl) &&
            instanceEl.TryGetProperty("qrcode", out var instanceQrCodeEl))
        {
            if (instanceQrCodeEl.ValueKind == JsonValueKind.String)
                return (true, NormalizeExtractedBase64(instanceQrCodeEl));

            if (instanceQrCodeEl.ValueKind == JsonValueKind.Object && instanceQrCodeEl.TryGetProperty("base64", out var instanceQrCodeBase64El))
                return (true, NormalizeExtractedBase64(instanceQrCodeBase64El));
        }

        return (true, null);
    }

    private static string? NormalizeExtractedBase64(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            return null;

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Valida se o valor extraído se parece com um QR Code válido (data URL de imagem
    /// ou base64 razoavelmente longo). Evita que respostas em branco, JSON ou strings
    /// curtas sejam devolvidas ao frontend como imagem, causando ícone de imagem quebrada.
    /// </summary>
    private static bool IsPlausibleQrCodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();

        // JSON acidental (bug histórico: fallback devolvia o body inteiro como base64)
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return false;

        if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return trimmed.Length > "data:image/png;base64,".Length + 32;

        // Um PNG mínimo em base64 tem centenas de bytes; QR Code Evolution geralmente
        // passa de 1 KB. Usamos um piso conservador para descartar respostas espúrias.
        return trimmed.Length >= 100;
    }

    private static bool IsEvolutionSessionAlreadyLoggedIn(string? responseBody)
        => !string.IsNullOrWhiteSpace(responseBody) &&
           responseBody.Contains("session already logged in", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> PersistQrCodeImageAsync(string instanceName, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var normalizedPayload = payload.Trim();
        if (normalizedPayload.StartsWith('"') && normalizedPayload.EndsWith('"'))
            normalizedPayload = normalizedPayload[1..^1].Trim();

        // Evolution Go pode devolver "imagem_base64|texto_do_qr". A imagem é a
        // primeira parte; o texto após "|" é um fallback para clientes que renderizam
        // o QR localmente e não deve ser incluído no base64.
        var separatorIndex = normalizedPayload.IndexOf('|');
        if (separatorIndex > 0)
            normalizedPayload = normalizedPayload[..separatorIndex];

        byte[]? bytes = null;
        var extension = ".png";
        var contentType = "image/png";

        if (normalizedPayload.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = normalizedPayload.IndexOf(',');
            if (commaIndex <= 0 || commaIndex >= normalizedPayload.Length - 1)
                return null;

            var prefix = normalizedPayload[..commaIndex];
            var body = normalizedPayload[(commaIndex + 1)..];
            contentType = prefix.Contains("image/svg+xml", StringComparison.OrdinalIgnoreCase)
                ? "image/svg+xml"
                : prefix.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase)
                    ? "image/jpeg"
                    : "image/png";
            extension = contentType switch
            {
                "image/svg+xml" => ".svg",
                "image/jpeg" => ".jpg",
                _ => ".png"
            };

            if (contentType == "image/svg+xml" && !prefix.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(body));
            }
            else
            {
                var decoded = NormalizeBase64Payload(body);
                if (!TryDecodeBase64Payload(decoded, out bytes) &&
                    !TryRenderEvolutionQrPayload(body, out bytes))
                    return null;
            }
        }
        else if (LooksLikeSvgMarkup(normalizedPayload))
        {
            bytes = Encoding.UTF8.GetBytes(normalizedPayload);
            contentType = "image/svg+xml";
            extension = ".svg";
        }
        else
        {
            var decoded = NormalizeBase64Payload(normalizedPayload);
            if (!TryDecodeBase64Payload(decoded, out bytes) &&
                !TryRenderEvolutionQrPayload(normalizedPayload, out bytes))
                return null;
        }

        if (bytes == null || bytes.Length == 0)
            return null;

        var safeInstanceName = Path.GetFileName(NormalizeInstanceName(instanceName));
        if (string.IsNullOrWhiteSpace(safeInstanceName) ||
            !safeInstanceName.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return null;
        }

        var uploadsDir = Path.Combine(_webHostEnvironment?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "qrcodes");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{safeInstanceName}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        _logger.LogInformation(
            "Evolution Go QR code salvo em disco como '{FileName}' para a instância '{Instance}' ({ContentType}, {Bytes} bytes)",
            fileName,
            safeInstanceName,
            contentType,
            bytes.Length);

        return $"/uploads/qrcodes/{fileName}";
    }

    private bool TryRenderEvolutionQrPayload(string payload, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var qrPayload = payload.Trim();
        if (qrPayload.Length < 10)
            return false;

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
            bytes = new PngByteQRCode(data).GetGraphic(12);
            _logger.LogInformation(
                "Evolution Go retornou payload textual de QR; imagem PNG foi gerada localmente ({Bytes} bytes)",
                bytes.Length);
            return bytes.Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Não foi possível converter o payload textual retornado pela Evolution Go em imagem de QR Code");
            return false;
        }
    }

    private static bool TryDecodeBase64Payload(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static string NormalizeBase64Payload(string value)
    {
        var withPlusRestored = value.Replace(' ', '+');
        var compact = new string(withPlusRestored.Where(ch => !char.IsWhiteSpace(ch)).ToArray())
            .Replace('-', '+')
            .Replace('_', '/');

        var modulo = compact.Length % 4;
        if (modulo != 0)
            compact = compact.PadRight(compact.Length + (4 - modulo), '=');

        return compact;
    }

    private static bool LooksLikeSvgMarkup(string value)
    {
        var trimmed = value.TrimStart('\uFEFF', '\u0000', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
                trimmed.Contains("<svg", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryExtractEvolutionStatus(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return TryExtractEvolutionStatus(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryExtractEvolutionStatus(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (item.TryGetProperty("connectionStatus", out var connectionStatusEl))
        {
            if (connectionStatusEl.ValueKind == JsonValueKind.String)
                return connectionStatusEl.GetString();

            if (connectionStatusEl.ValueKind == JsonValueKind.Object && connectionStatusEl.TryGetProperty("state", out var connectionStateEl))
                return connectionStateEl.GetString();
        }

        if (item.TryGetProperty("status", out var statusEl))
            return statusEl.GetString();

        if (item.TryGetProperty("state", out var stateEl))
            return stateEl.GetString();

        var hasRootConnected = TryGetBooleanProperty(item, "Connected", out var rootConnected);
        var hasRootLoggedIn = TryGetBooleanProperty(item, "LoggedIn", out var rootLoggedIn);
        if (hasRootConnected || hasRootLoggedIn)
        {
            return rootConnected || rootLoggedIn ? "OPEN" : "CLOSE";
        }

        if (item.TryGetProperty("instance", out var instanceEl))
        {
            var nestedStatus = TryExtractEvolutionStatus(instanceEl);
            if (!string.IsNullOrWhiteSpace(nestedStatus))
                return nestedStatus;
        }

        if (item.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
        {
            var nestedStatus = TryExtractEvolutionStatus(dataEl);
            if (!string.IsNullOrWhiteSpace(nestedStatus))
                return nestedStatus;

            var hasConnected = TryGetBooleanProperty(dataEl, "Connected", out var connected);
            var hasLoggedIn = TryGetBooleanProperty(dataEl, "LoggedIn", out var loggedIn);
            if (hasConnected || hasLoggedIn)
                return connected || loggedIn ? "OPEN" : "CLOSE";
        }

        return null;
    }

    private static bool TryGetBooleanProperty(JsonElement element, string propertyName, out bool value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.Value.GetBoolean();
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetInstanceName(JsonElement item, out string? name)
    {
        name = null;

        if (item.ValueKind != JsonValueKind.Object)
            return false;

        if (item.TryGetProperty("name", out var nameEl))
            name = nameEl.GetString();
        else if (item.TryGetProperty("instance_name", out var instNameEl))
            name = instNameEl.GetString();
        else if (item.TryGetProperty("instanceName", out var instNameEl2))
            name = instNameEl2.GetString();
        else if (item.TryGetProperty("instance", out var instanceEl) && TryGetInstanceName(instanceEl, out var nestedName))
            name = nestedName;

        return !string.IsNullOrWhiteSpace(name);
    }

    private static string? TryExtractEvolutionInstanceToken(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (item.TryGetProperty("token", out var tokenEl))
            return tokenEl.GetString();

        if (item.TryGetProperty("hash", out var hashEl))
            return hashEl.GetString();

        if (item.TryGetProperty("instance", out var instanceEl))
            return TryExtractEvolutionInstanceToken(instanceEl);

        return null;
    }

    private static string BuildMethodAndPath(HttpMethod method, Uri? requestUri)
        => requestUri is null
            ? method.Method
            : $"{method.Method} {requestUri.PathAndQuery}";

    private sealed record EvolutionQrEndpointAttempt(HttpMethod Method, string Uri);

    private sealed record EvolutionInstanceSummary(string Name, string? Status, string? Token, string? QrCodeBase64);

    private async Task<string?> GetStoredNonOfficialWhatsAppApiKeyAsync(int farmaciaId)
    {
        var integracoes = await _context.FarmaciaIntegracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FarmaciaId == farmaciaId);

        return integracoes?.WhatsAppApiKey?.Trim();
    }

    private async Task<string?> GetStoredNonOfficialWhatsAppApiUrlAsync(int farmaciaId)
    {
        var integracoes = await _context.FarmaciaIntegracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FarmaciaId == farmaciaId);

        return integracoes?.WhatsAppApiUrl?.Trim();
    }

    private async Task PersistNonOfficialApiSettingsIfMissingAsync(Farmacia farmacia, string instanceName, string apiUrl, string apiKey)
    {
        var hasChanges = false;
        if (farmacia.Integracoes == null)
        {
            farmacia.Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = farmacia.Id
            };
            _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
            hasChanges = true;
        }

        var integracoes = farmacia.Integracoes;
        if (string.IsNullOrWhiteSpace(integracoes.WhatsAppApiKey))
        {
            integracoes.WhatsAppApiKey = apiKey;
            hasChanges = true;
        }

        if (string.IsNullOrWhiteSpace(integracoes.WhatsAppApiUrl))
        {
            integracoes.WhatsAppApiUrl = apiUrl;
            hasChanges = true;
        }

        if (string.IsNullOrWhiteSpace(integracoes.ChaveWhatsApp))
        {
            integracoes.ChaveWhatsApp = instanceName;
            hasChanges = true;
        }

        if (hasChanges)
            await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Verifica se o log completo está habilitado para a farmácia (FarmaciaIntegracoes.SalvarLogCompletoMidiaApiGo).
    /// </summary>
    private async Task<bool> GetSalvarLogCompletoAsync(int farmaciaId)
    {
        try
        {
            var integracoes = await _context.FarmaciaIntegracoes
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.FarmaciaId == farmaciaId);
            return integracoes?.SalvarLogCompletoMidiaApiGo ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sincroniza os campos de integração Evolution na FarmaciaIntegracoes para que
    /// WhatsAppService (envio de mensagens) e webhook matching funcionem corretamente.
    /// </summary>
    private static void SyncIntegracoesCanalNaoOficial(FarmaciaIntegracoes integracoes, string instanceName, string apiUrl, string apiKey)
    {
        integracoes.ChaveWhatsApp = instanceName;
        integracoes.WhatsAppApiUrl = apiUrl;
        integracoes.WhatsAppApiKey = apiKey;
    }

    private static void ResetIntegracoesCanalNaoOficial(FarmaciaIntegracoes integracoes)
    {
        integracoes.ChaveWhatsApp = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
        integracoes.WhatsAppApiUrl = null;
        integracoes.WhatsAppApiKey = null;
    }

    private static string GetStoredInstanceName(Canal canal, FarmaciaIntegracoes? integracoes = null)
    {
        if (!string.IsNullOrWhiteSpace(canal.InstanceName))
            return canal.InstanceName.Trim();

        if (!string.IsNullOrWhiteSpace(integracoes?.ChaveWhatsApp))
            return integracoes.ChaveWhatsApp.Trim();

        return string.Empty;
    }

    private static string NormalizeInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return string.Empty;

        return instanceName.Trim();
    }

    private static string GetInstanceDisplayName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return string.Empty;

        return instanceName.Trim();
    }

    private async Task<Canal?> FindWhatsAppOfficialChannelByPhoneNumberIdAsync(
        string phoneNumberId,
        int? excludeCanalId = null)
    {
        var normalizedPhoneNumberId = phoneNumberId.Trim();

        return await _context.Canais
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Tipo == 1 &&
                (excludeCanalId == null || c.Id != excludeCanalId.Value) &&
                c.IsOfficial &&
                !string.IsNullOrWhiteSpace(c.PhoneNumberId) &&
                c.PhoneNumberId.Trim() == normalizedPhoneNumberId);
    }

    private async Task<Canal?> FindWhatsAppNonOfficialChannelByInstanceNameAsync(
        string instanceName,
        int? excludeCanalId = null)
    {
        var normalizedCandidates = GetComparableInstanceNames(instanceName)
            .Select(candidate => candidate.ToLowerInvariant())
            .ToList();

        return await _context.Canais
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Tipo == 1 &&
                !c.IsOfficial &&
                (excludeCanalId == null || c.Id != excludeCanalId.Value) &&
                normalizedCandidates.Contains((c.InstanceName ?? c.Nome).Trim().ToLower()));
    }

    private static EvolutionInstanceSummary? TryFindMatchingEvolutionInstance(
        IEnumerable<EvolutionInstanceSummary> instances,
        string instanceName)
    {
        var comparableNames = GetComparableInstanceNames(instanceName);
        return instances.FirstOrDefault(item =>
            comparableNames.Any(candidate =>
                string.Equals(candidate, item.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<string> GetComparableInstanceNames(string instanceName)
    {
        var normalized = NormalizeInstanceName(instanceName);
        if (string.IsNullOrWhiteSpace(normalized))
            return new List<string>();

        return new List<string> { normalized };
    }

    private static bool IsValidMetaAppMode(MetaAppMode metaAppMode)
        => metaAppMode is MetaAppMode.Test or MetaAppMode.Production;

    private static MetaAppMode ResolveMetaAppMode(MetaAppMode metaAppMode)
        => IsValidMetaAppMode(metaAppMode) ? metaAppMode : MetaAppMode.Production;

    private static bool IsValidMetaConfigSource(MetaConfigSource metaConfigSource)
        => metaConfigSource is MetaConfigSource.Own or MetaConfigSource.PharmazapCentral;

    private static MetaConfigSource ResolveMetaConfigSource(MetaConfigSource metaConfigSource)
        => IsValidMetaConfigSource(metaConfigSource) ? metaConfigSource : MetaConfigSource.PharmazapCentral;

    private static string? ResolveMetaAppSecret(string? requestedSecret, string? existingSecret)
        => !string.IsNullOrWhiteSpace(requestedSecret)
            ? requestedSecret.Trim()
            : string.IsNullOrWhiteSpace(existingSecret)
                ? null
                : existingSecret.Trim();

}

public record CreateChannelRequest(
    string Name,
    int Type,
    bool IsOfficial,
    string? AccessToken,
    string? PhoneNumberId,
    string? InstagramBusinessId,
    string? BusinessAccountId,
    string? ApiUrl,
    string? ApiKey,
    string? InstanceName,
    string? InstagramUsername,
    string? InstagramPassword,
    bool HasStoredInstagramPassword = false,
    bool HasPersistedApiKey = false,
    MetaAppMode MetaAppMode = MetaAppMode.Production,
    MetaConfigSource MetaConfigSource = MetaConfigSource.Own,
    string? MetaAppId = null,
    string? MetaAppSecret = null,
    bool HasPersistedMetaAppSecret = false);

public record QrCodeGenerateRequest(
    string InstanceName,
    string ApiUrl,
    string ApiKey,
    string? Name = null,
    bool ForceRecreate = false);

public record PairingCodeGenerateRequest(
    string InstanceName,
    string ApiUrl,
    string ApiKey,
    string PhoneNumber);
