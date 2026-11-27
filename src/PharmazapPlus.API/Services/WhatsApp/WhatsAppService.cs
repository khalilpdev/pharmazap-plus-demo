#nullable enable

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Services.WhatsApp;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly PharmazapDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly WhatsAppConfig _config;
    private readonly IMessageQueue _messageQueue;
    private readonly IHubContext<PharmaHub> _hubContext;

    public WhatsAppService(
        HttpClient httpClient,
        PharmazapDbContext dbContext,
        IDistributedCache cache,
        ILogger<WhatsAppService> logger,
        IOptions<WhatsAppConfig> config,
        IMessageQueue messageQueue,
        IHubContext<PharmaHub> hubContext)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _config = config.Value;
        _messageQueue = messageQueue;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Versioned base URL for WhatsApp Business API calls (e.g. https://graph.facebook.com/v18.0).
    /// Uses <see cref="WhatsAppConfig.ApiVersion"/> so the version can be updated via configuration.
    /// </summary>
    private string VersionedApiBaseUrl => $"{_config.ApiBaseUrl}/{_config.ApiVersion}";

    #region Métodos da Interface IWhatsAppService

    public async Task<bool> ValidateWebhook(string mode, string token, string challenge)
    {
        try
        {
            _logger.LogInformation("Validating webhook: Mode={Mode}", mode);

            var verifyToken = _config.WebhookVerifyToken;
            if (mode == "subscribe" && token == verifyToken)
            {
                _logger.LogInformation("Webhook validation successful");
                return true;
            }

            _logger.LogWarning("Webhook validation failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook");
            return false;
        }
    }

    public async Task<WhatsAppMessageResponse> SendTextMessage(string farmaciaId, string to, string text)
    {
        try
        {
            var farmacia = await GetFarmaciaWithWhatsAppConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou WhatsApp não configurado");

            var integracoes = farmacia.Integracoes!;
            var salvarLogCompleto = integracoes.SalvarLogCompletoMidiaApiGo;
            var apiType = GetApiType(integracoes);

            if (apiType == WhatsAppApiType.Evolution)
            {
                var instanceName = integracoes.ChaveWhatsApp;
                if (string.IsNullOrEmpty(instanceName))
                    instanceName = "default";

                var result = await SendTextMessageEvolution(
                    integracoes.WhatsAppApiUrl!,
                    integracoes.WhatsAppApiKey ?? "",
                    instanceName,
                    to,
                    text,
                    farmaciaId,
                    salvarLogCompleto);

                if (result.Success)
                {
                    try
                    {
                        await NotifyNewMessage(result.MessageId, farmaciaId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SignalR notification failed for message {MessageId} (non-critical)", result.MessageId);
                    }
                }

                return result;
            }
            else
            {
                var toForApi = FormatPhoneNumberForSending(to);
                var toNormalizedForDb = to.NormalizeWhatsAppIdentifier();

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toForApi,
                    type = "text",
                    text = new { body = text }
                };

                var response = await SendWhatsAppRequest(
                    integracoes.WhatsAppPhoneNumberId,
                    integracoes.WhatsAppAccessToken,
                    "messages",
                    payload,
                    salvarLogCompleto);

                var result = await ProcessSendResponse(response, farmaciaId, toNormalizedForDb, text, salvarLogCompleto);

                if (result.Success)
                {
                    try
                    {
                        await NotifyNewMessage(result.MessageId, farmaciaId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SignalR notification failed for message {MessageId} (non-critical)", result.MessageId);
                    }
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text message to {To}", to);
            return CreateErrorResponse($"Erro ao enviar mensagem: {ex.Message}");
        }
    }

    public async Task<WhatsAppMessageResponse> SendMediaMessage(string farmaciaId, string to, string mediaUrl, string mediaType, string? caption = null)
    {
        try
        {
            var farmacia = await GetFarmaciaWithWhatsAppConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou WhatsApp não configurado");

            var integracoes = farmacia.Integracoes!;
            var salvarLogCompleto = integracoes.SalvarLogCompletoMidiaApiGo;
            var apiType = GetApiType(integracoes);

            if (apiType == WhatsAppApiType.Evolution)
            {
                var instanceName = integracoes.ChaveWhatsApp;
                if (string.IsNullOrEmpty(instanceName))
                    instanceName = "default";

                var result = await SendMediaMessageEvolution(
                    integracoes.WhatsAppApiUrl!,
                    integracoes.WhatsAppApiKey ?? "",
                    instanceName,
                    to,
                    mediaUrl,
                    mediaType,
                    caption,
                    farmaciaId,
                    salvarLogCompleto);

                return result;
            }
            else
            {
                var toForApi = FormatPhoneNumberForSending(to);
                var toNormalizedForDb = to.NormalizeWhatsAppIdentifier();
                var messageType = GetMediaMessageType(mediaType);

                object mediaObject;
                if (messageType == "image" || messageType == "video" || messageType == "document")
                {
                    mediaObject = new
                    {
                        link = mediaUrl,
                        caption = caption
                    };
                }
                else if (messageType == "audio")
                {
                    mediaObject = new
                    {
                        link = mediaUrl
                    };
                }
                else
                {
                    return CreateErrorResponse($"Tipo de mídia não suportado: {mediaType}");
                }

                var payloadDictionary = new Dictionary<string, object>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = toForApi,
                    ["type"] = messageType,
                    [messageType] = mediaObject
                };

                var json = JsonSerializer.Serialize(payloadDictionary);

                var url = $"{VersionedApiBaseUrl}/{integracoes.WhatsAppPhoneNumberId}/messages";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", integracoes.WhatsAppAccessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                var result = await ProcessSendResponse(response, farmaciaId, toNormalizedForDb,
                    $"Mídia: {mediaType} - {caption}", salvarLogCompleto);

                if (result.Success)
                {
                    var message = await _dbContext.Mensagens
                        .FirstOrDefaultAsync(m => m.MessageId == result.MessageId);

                    if (message != null)
                    {
                        message.MediaUrl = mediaUrl;
                        message.MediaType = mediaType;
                        message.MediaCaption = caption;
                        await _dbContext.SaveChangesAsync();
                    }
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending media message to {To}", to);
            return CreateErrorResponse($"Erro ao enviar mídia: {ex.Message}");
        }
    }

    public async Task<WhatsAppMessageResponse> SendTemplateMessage(string farmaciaId, string to, string templateName, string languageCode, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Meta WhatsApp API não configurada");

            var salvarLogCompleto = farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false;
            var cleanNumber = CleanPhoneNumber(to);

            var components = new List<object>();
            if (parameters != null && parameters.Any())
            {
                components.Add(new
                {
                    type = "body",
                    parameters = parameters.Select(p => new
                    {
                        type = "text",
                        text = p.Value?.ToString()
                    }).ToArray()
                });
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = cleanNumber,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = languageCode },
                    components = components
                }
            };

            var response = await SendWhatsAppRequest(
                farmacia.Integracoes!.WhatsAppPhoneNumberId,
                farmacia.Integracoes!.WhatsAppAccessToken,
                "messages",
                payload,
                salvarLogCompleto);

            var result = await ProcessSendResponse(response, farmaciaId, cleanNumber,
                $"Template: {templateName}", salvarLogCompleto);

            if (result.Success)
            {
                var message = await _dbContext.Mensagens
                    .FirstOrDefaultAsync(m => m.MessageId == result.MessageId);

                if (message != null)
                {
                    message.TemplateName = templateName;
                    message.TemplateLanguage = languageCode;
                    message.TemplateVariables = parameters != null ?
                        JsonSerializer.Serialize(parameters) : null;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending template message to {To}", to);
            return CreateErrorResponse($"Erro ao enviar template: {ex.Message}");
        }
    }

    public async Task<WebhookProcessingResult> ProcessWebhookPayload(Dictionary<string, object> payload)
    {
        try
        {
            var objectType = payload.TryGetValue("object", out var payloadObjectType)
                ? payloadObjectType?.ToString()
                : "unknown";
            _logger.LogInformation("Processing webhook payload (object={Object}, keys={KeysCount})",
                objectType,
                payload.Count);

            if (payload.ContainsKey("object") && payload["object"]?.ToString() == "whatsapp_business_account")
            {
                var entries = payload["entry"] as JsonElement?;
                if (entries?.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entries.Value.EnumerateArray())
                    {
                        var changes = entry.GetProperty("changes");
                        foreach (var change in changes.EnumerateArray())
                        {
                            var value = change.GetProperty("value");
                            var field = change.GetProperty("field").GetString();

                            if (field == "messages")
                            {
                                return await ProcessIncomingMessage(value);
                            }
                            else if (field == "message_template_status_update")
                            {
                                return await ProcessTemplateStatusUpdate(value);
                            }
                        }
                    }
                }
            }

            return new WebhookProcessingResult
            {
                Success = false,
                Message = "Payload não reconhecido",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook payload");
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Erro ao processar: {ex.Message}",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
    }

    public async Task<string> SetupWebhook(string farmaciaId, string webhookUrl)
    {
        try
        {
            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                throw new ArgumentException("Farmácia não encontrada ou Meta WhatsApp API não configurada");

            var appId = farmacia.Integracoes!.WhatsAppBusinessAccountId;
            var accessToken = farmacia.Integracoes!.WhatsAppAccessToken;

            var payload = new
            {
                url = webhookUrl,
                fields = new[] { "messages", "message_template_status_update" },
                verify_token = _config.WebhookVerifyToken
            };

            var response = await SendMetaApiRequest(
                $"{_config.ApiVersion}/{appId}/subscriptions",
                accessToken,
                payload,
                HttpMethod.Post);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook configurado para farmácia {FarmaciaId}: {WebhookUrl}",
                    farmaciaId, webhookUrl);
                return webhookUrl;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro ao configurar webhook: {Error}", error);
                throw new Exception($"Erro ao configurar webhook: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao configurar webhook para farmácia {FarmaciaId}", farmaciaId);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetBusinessProfile(string farmaciaId)
    {
        try
        {
            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                throw new ArgumentException("Farmácia não encontrada ou Meta WhatsApp API não configurada");

            var phoneNumberId = farmacia.Integracoes!.WhatsAppPhoneNumberId;
            var accessToken = farmacia.Integracoes!.WhatsAppAccessToken;

            var response = await SendMetaApiRequest(
                $"{_config.ApiVersion}/{phoneNumberId}?fields=about,address,description,email,profile_picture_url,websites,vertical",
                accessToken,
                null,
                HttpMethod.Get);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new Dictionary<string, object>();
            }
            else
            {
                _logger.LogWarning("Não foi possível obter perfil de negócios para farmácia {FarmaciaId}", farmaciaId);
                return new Dictionary<string, object>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter perfil de negócios para farmácia {FarmaciaId}", farmaciaId);
            return new Dictionary<string, object>();
        }
    }

    public async Task<WhatsAppMessageResponse> SendInteractiveMessage(string farmaciaId, string to, string headerText, string bodyText, List<WhatsAppButton> buttons)
    {
        try
        {
            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Meta WhatsApp API não configurada");

            var salvarLogCompleto = farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false;
            var cleanNumber = CleanPhoneNumber(to);

            var buttonObjects = buttons.Select(b => new
            {
                type = b.Type,
                reply = b.Type == "reply" ? new { id = b.Payload, title = b.Title } : null,
                url = b.Type == "url" ? b.Url : null,
                phone_number = b.Type == "phone_number" ? b.PhoneNumber : null
            }).ToArray();

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = cleanNumber,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    header = new
                    {
                        type = "text",
                        text = headerText
                    },
                    body = new
                    {
                        text = bodyText
                    },
                    action = new
                    {
                        buttons = buttonObjects
                    }
                }
            };

            var response = await SendWhatsAppRequest(
                farmacia.Integracoes!.WhatsAppPhoneNumberId,
                farmacia.Integracoes!.WhatsAppAccessToken,
                "messages",
                payload,
                salvarLogCompleto);

            var result = await ProcessSendResponse(response, farmaciaId, cleanNumber,
                $"Interativo: {headerText} - {bodyText}", salvarLogCompleto);

            if (result.Success)
            {
                var mensagem = await _dbContext.Mensagens
                    .FirstOrDefaultAsync(m => m.MessageId == result.MessageId);

                if (mensagem != null)
                {
                    mensagem.Type = MessageType.Interactive;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem interativa para {To}", to);
            return CreateErrorResponse($"Erro ao enviar mensagem interativa: {ex.Message}");
        }
    }

    public async Task<MessageStatus> GetMessageStatus(string farmaciaId, string messageId)
    {
        try
        {
            if (_config.DevelopmentMode)
            {
                await Task.Delay(100);
                return MessageStatus.Entregue;
            }

            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                return MessageStatus.Falha;

            var accessToken = farmacia.Integracoes!.WhatsAppAccessToken;

            var response = await SendMetaApiRequest(
                $"{_config.ApiVersion}/{messageId}",
                accessToken,
                null,
                HttpMethod.Get);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

                var status = result?["status"]?.ToString() ?? "unknown";

                return status.ToLower() switch
                {
                    "sent" => MessageStatus.Enviada,
                    "delivered" => MessageStatus.Entregue,
                    "read" => MessageStatus.Lida,
                    "failed" => MessageStatus.Falha,
                    _ => MessageStatus.Processando
                };
            }

            return MessageStatus.Falha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status da mensagem {MessageId}", messageId);
            return MessageStatus.Falha;
        }
    }

    public async Task<Dictionary<string, object>> GetBusinessMetrics(string farmaciaId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var farmacia = await GetFarmaciaWithMetaConfig(farmaciaId);
            if (farmacia == null)
                throw new ArgumentException("Farmácia não encontrada ou Meta WhatsApp API não configurada");

            var metrics = new Dictionary<string, object>
            {
                { "period_start", startDate },
                { "period_end", endDate }
            };

            if (_config.DevelopmentMode)
            {
                var random = new Random();
                metrics["total_messages_sent"] = random.Next(100, 1000);
                metrics["total_messages_received"] = random.Next(80, 900);
                metrics["delivery_rate"] = random.Next(85, 99);
                metrics["read_rate"] = random.Next(70, 95);
                metrics["cost_estimate"] = random.Next(50, 500);
                metrics["active_conversations"] = random.Next(10, 100);
                return metrics;
            }

            var appId = farmacia.Integracoes!.WhatsAppBusinessAccountId;
            var accessToken = farmacia.Integracoes!.WhatsAppAccessToken;
            var startUnix = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
            var endUnix = ((DateTimeOffset)endDate).ToUnixTimeSeconds();

            var response = await SendMetaApiRequest(
                $"{_config.ApiVersion}/{appId}/insights?metric=TOTAL_MESSAGES_SENT,TOTAL_MESSAGES_RECEIVED&since={startUnix}&until={endUnix}",
                accessToken,
                null,
                HttpMethod.Get);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

                if (apiResult != null && apiResult.ContainsKey("data"))
                {
                    var dataArray = apiResult["data"] as JsonElement?;
                    if (dataArray?.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataArray.Value.EnumerateArray())
                        {
                            var metric = item.GetProperty("name").GetString();
                            var values = item.GetProperty("values");

                            if (!string.IsNullOrEmpty(metric) && values.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var value in values.EnumerateArray())
                                {
                                    var val = value.GetProperty("value").GetString();
                                    metrics[metric] = val ?? string.Empty;
                                }
                            }
                        }
                    }
                }
            }

            var dbMetrics = await GetDatabaseMetrics(farmaciaId, startDate, endDate);
            foreach (var kvp in dbMetrics)
            {
                metrics[kvp.Key] = kvp.Value;
            }

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas de negócios para farmácia {FarmaciaId}", farmaciaId);
            return new Dictionary<string, object>
            {
                { "error", ex.Message },
                { "period_start", startDate },
                { "period_end", endDate }
            };
        }
    }

    /// <summary>
    /// Método de teste para verificar a formatação de números de telefone
    /// </summary>
    public string TestPhoneFormatting(string phoneNumber)
    {
        var formatted = FormatPhoneNumberForEvolution(phoneNumber);
        LogPhoneNumberFormatting(phoneNumber, formatted, "TestPhoneFormatting");
        return formatted;
    }

    #endregion

    #region Métodos Privados de Suporte

    private async Task<Farmacia?> GetFarmaciaWithWhatsAppConfig(string farmaciaId)
    {
        if (int.TryParse(farmaciaId, out int id))
        {
            var farmacia = await _dbContext.Farmacias
                .Include(f => f.Integracoes)
                .ThenInclude(i => i!.Canais)
                .Where(f => f.Id == id && f.Integracoes != null)
                .FirstOrDefaultAsync();

            if (farmacia?.Integracoes == null) return null;

            var temMetaApi = !string.IsNullOrEmpty(farmacia.Integracoes.WhatsAppPhoneNumberId) &&
                            !string.IsNullOrEmpty(farmacia.Integracoes.WhatsAppAccessToken);

            var temEvolutionApi = !string.IsNullOrEmpty(farmacia.Integracoes.WhatsAppApiUrl);

            if (!temMetaApi && !temEvolutionApi) return null;

            return farmacia;
        }
        return null;
    }

    private async Task<Farmacia?> GetFarmaciaWithMetaConfig(string farmaciaId)
    {
        if (int.TryParse(farmaciaId, out int id))
        {
            var farmacia = await _dbContext.Farmacias
                .Include(f => f.Integracoes)
                .Where(f => f.Id == id && f.Integracoes != null)
                .FirstOrDefaultAsync();

            if (farmacia?.Integracoes == null) return null;

            var temMetaApi = !string.IsNullOrEmpty(farmacia.Integracoes.WhatsAppPhoneNumberId) &&
                            !string.IsNullOrEmpty(farmacia.Integracoes.WhatsAppAccessToken);

            if (!temMetaApi) return null;

            return farmacia;
        }
        return null;
    }

    private enum WhatsAppApiType
    {
        Meta,
        Evolution
    }

    private WhatsAppApiType GetApiType(FarmaciaIntegracoes integracoes)
    {
        var hasMetaApi = !string.IsNullOrWhiteSpace(integracoes.WhatsAppPhoneNumberId) &&
                         !string.IsNullOrWhiteSpace(integracoes.WhatsAppAccessToken);
        var hasEvolutionApi = !string.IsNullOrWhiteSpace(integracoes.WhatsAppApiUrl);
        var hasNonOfficialWhatsAppChannel = integracoes.Canais?.Any(c =>
            c.Tipo == Consts.CanalTipo.WhatsApp &&
            !c.IsOfficial &&
            !string.IsNullOrWhiteSpace(c.InstanceName)) == true;

        if (hasEvolutionApi && hasNonOfficialWhatsAppChannel)
        {
            _logger.LogWarning(
                "Canal WhatsApp não oficial ativo encontrado; usando Evolution em vez de credenciais Meta residuais. PhoneNumberId={PhoneNumberId}, ApiUrl={ApiUrl}",
                integracoes.WhatsAppPhoneNumberId,
                integracoes.WhatsAppApiUrl);
            return WhatsAppApiType.Evolution;
        }

        if (hasMetaApi && hasEvolutionApi)
        {
            _logger.LogWarning(
                "Farmácia com configuração mista de WhatsApp sem canal não oficial ativo; usando Meta API. PhoneNumberId={PhoneNumberId}, ApiUrl={ApiUrl}",
                integracoes.WhatsAppPhoneNumberId,
                integracoes.WhatsAppApiUrl);
            return WhatsAppApiType.Meta;
        }

        if (hasEvolutionApi)
            return WhatsAppApiType.Evolution;

        return WhatsAppApiType.Meta;
    }

    private string FormatPhoneNumberForSending(string? phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber)) return string.Empty;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.Length == 0) return string.Empty;

        if (digits.StartsWith("55"))
        {
            if (digits.Length == 12)
            {
                // Se digits[4] == '9', o 9º dígito móvel já está presente; retorna como está.
                // Caso contrário, insere o 9 após o DDD.
                if (digits[4] == '9')
                    return digits;
                return "55" + digits[2..4] + "9" + digits[4..];
            }
            if (digits.Length == 13)
                return digits;
            return digits;
        }
        else
        {
            if (digits.Length == 10)
            {
                // Se digits[2] == '9', o prefixo móvel já está presente; apenas prefixa 55.
                // Caso contrário, insere o 9 após o DDD.
                if (digits[2] == '9')
                    return "55" + digits;
                return "55" + digits[..2] + "9" + digits[2..];
            }
            if (digits.Length == 11)
                return "55" + digits;
            if (digits.Length >= 8 && digits.Length < 10)
                return "55" + digits.PadLeft(10, '0')[..2] + "9" + digits.PadLeft(10, '0')[2..];
            return "55" + digits;
        }
    }

    private string FormatPhoneNumberForEvolution(string? phoneNumber)
    {
        var formatted = FormatPhoneNumberForSending(phoneNumber);
        if (string.IsNullOrEmpty(formatted)) return string.Empty;
        return formatted;
    }

    private async Task<WhatsAppMessageResponse> SendTextMessageEvolution(
        string apiUrl,
        string apiKey,
        string instanceName,
        string to,
        string text,
        string farmaciaId,
        bool salvarLogCompleto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Evolution API key não configurada para farmácia {FarmaciaId}", farmaciaId);
                return CreateErrorResponse("Evolution API key não configurada");
            }

            var formattedTo = FormatPhoneNumberForEvolution(to);
            LogPhoneNumberFormatting(to, formattedTo, "SendTextMessageEvolution");

            if (string.IsNullOrEmpty(formattedTo) || formattedTo.Length < 12)
            {
                _logger.LogError("Número formatado inválido: '{FormattedTo}' (original: '{To}')", formattedTo, to);
                return CreateErrorResponse($"Número de telefone inválido após formatação: {to}");
            }

            var baseUrl = DeriveEvolutionGoBaseUrl(apiUrl);
            var resolvedApiKey = await ResolveEvolutionInstanceApiKeyAsync(baseUrl, apiKey, instanceName);

            var payload = new
            {
                number = formattedTo,
                text,
                delay = 1200,
                formatJid = false
            };

            var json = JsonSerializer.Serialize(payload);

            var (response, responseContent, requestUrl) = await SendEvolutionRequestWithFallbackAsync(
                $"{baseUrl}/send/text",
                $"{baseUrl}/message/sendText/{instanceName}",
                resolvedApiKey,
                apiKey,
                json,
                salvarLogCompleto);

            if (salvarLogCompleto)
            {
                _logger.LogInformation("📥 Evolution API text response → URL: {Url} | Status: {StatusCode} | Content: {Content}",
                    requestUrl, response.StatusCode, responseContent);
            }

            if (response.IsSuccessStatusCode)
            {
                var messageId = ExtractEvolutionMessageId(responseContent) ?? $"evolution-{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "✅ Evolution message sent - MessageId: {MessageId}, To: {To} (formatted: {Formatted})",
                    messageId, to, formattedTo);

                return new WhatsAppMessageResponse
                {
                    Success = true,
                    MessageId = messageId,
                    Timestamp = DateTime.UtcNow
                };
            }
            else
            {
                _logger.LogError("Error sending Evolution API message. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new WhatsAppMessageResponse
                {
                    Success = false,
                    ErrorCode = ((int)response.StatusCode).ToString(),
                    ErrorMessage = responseContent
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text message via Evolution API to {To} (farmacia: {FarmaciaId})", to, farmaciaId);
            return CreateErrorResponse($"Erro ao enviar mensagem via Evolution: {ex.Message}");
        }
    }

    private async Task<WhatsAppMessageResponse> SendMediaMessageEvolution(
        string apiUrl,
        string apiKey,
        string instanceName,
        string to,
        string mediaUrl,
        string mediaType,
        string? caption,
        string farmaciaId,
        bool salvarLogCompleto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Evolution API key não configurada para farmácia {FarmaciaId}", farmaciaId);
                return CreateErrorResponse("Evolution API key não configurada");
            }

            var formattedTo = FormatPhoneNumberForEvolution(to);
            LogPhoneNumberFormatting(to, formattedTo, "SendMediaMessageEvolution");

            var baseUrl = DeriveEvolutionGoBaseUrl(apiUrl);
            var resolvedApiKey = await ResolveEvolutionInstanceApiKeyAsync(baseUrl, apiKey, instanceName);

            var mimeType = mediaType.ToLower() switch
            {
                "jpg" or "jpeg" => "image",
                "png" => "image",
                "gif" => "video",
                "mp4" or "mov" or "avi" => "video",
                "mp3" or "ogg" or "m4a" or "wav" => "audio",
                "pdf" or "doc" or "docx" or "xls" or "xlsx" => "document",
                _ => "image"
            };

            var payload = new
            {
                number = formattedTo,
                type = mimeType,
                url = mediaUrl,
                filename = GetEvolutionFileName(mediaUrl, mimeType),
                caption = caption ?? "",
                delay = 1200,
                formatJid = false
            };

            var json = JsonSerializer.Serialize(payload);

            var (response, responseContent, requestUrl) = await SendEvolutionRequestWithFallbackAsync(
                $"{baseUrl}/send/media",
                $"{baseUrl}/message/sendMedia/{instanceName}",
                resolvedApiKey,
                apiKey,
                json,
                salvarLogCompleto);

            if (salvarLogCompleto)
            {
                _logger.LogInformation("📥 Evolution API media response → URL: {Url} | Status: {StatusCode} | Content: {Content}",
                    requestUrl, response.StatusCode, responseContent);
            }

            if (response.IsSuccessStatusCode)
            {
                var messageId = ExtractEvolutionMessageId(responseContent) ?? $"evolution-{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "✅ Evolution media message sent - MessageId: {MessageId}, To: {To} (formatted: {Formatted})",
                    messageId, to, formattedTo);

                return new WhatsAppMessageResponse
                {
                    Success = true,
                    MessageId = messageId,
                    Timestamp = DateTime.UtcNow
                };
            }
            else
            {
                _logger.LogError("Error sending Evolution API media message. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new WhatsAppMessageResponse
                {
                    Success = false,
                    ErrorCode = ((int)response.StatusCode).ToString(),
                    ErrorMessage = responseContent
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending media message via Evolution API to {To}", to);
            return CreateErrorResponse($"Erro ao enviar mídia via Evolution: {ex.Message}");
        }
    }

    private string CleanPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber)) return string.Empty;
        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }

    private static string DeriveEvolutionGoBaseUrl(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return string.Empty;

        if (!Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out var uri))
            return apiUrl.Trim().TrimEnd('/');

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private async Task<string> ResolveEvolutionInstanceApiKeyAsync(string baseUrl, string apiKey, string instanceName)
    {
        var resolved = await TryResolveEvolutionInstanceApiKeyAsync(baseUrl, apiKey, instanceName);
        return resolved ?? apiKey;
    }

    private async Task<string?> TryResolveEvolutionInstanceApiKeyAsync(string baseUrl, string apiKey, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(instanceName))
            return null;

        var instanceEndpoints = new[]
        {
            $"{baseUrl}/instance/fetchInstances?instanceName={Uri.EscapeDataString(instanceName)}",
            $"{baseUrl}/instance/all"
        };

        foreach (var endpoint in instanceEndpoints)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("apikey", apiKey);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                continue;

            var content = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                JsonElement instancesEl;

                if (root.ValueKind == JsonValueKind.Array)
                    instancesEl = root;
                else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                    instancesEl = dataEl;
                else if (root.TryGetProperty("instances", out var instancesProp) && instancesProp.ValueKind == JsonValueKind.Array)
                    instancesEl = instancesProp;
                else
                    continue;

                foreach (var item in instancesEl.EnumerateArray())
                {
                    if (!TryGetEvolutionInstanceName(item, out var listedName) ||
                        !string.Equals(listedName, instanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (item.TryGetProperty("token", out var tokenEl))
                        return tokenEl.GetString();

                    return apiKey;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Falha ao ler instâncias Evolution em {Endpoint} para '{InstanceName}'", endpoint, instanceName);
            }
        }

        return null;
    }

    private async Task<(HttpResponseMessage Response, string Content, string RequestUrl)> SendEvolutionRequestWithFallbackAsync(
        string preferredUrl,
        string legacyUrl,
        string preferredApiKey,
        string legacyApiKey,
        string json,
        bool salvarLogCompleto)
    {
        var firstAttempt = await SendEvolutionRequestAsync(preferredUrl, preferredApiKey, json, salvarLogCompleto);
        var firstContent = await firstAttempt.Content.ReadAsStringAsync();

        if ((int)firstAttempt.StatusCode != 404)
            return (firstAttempt, firstContent, preferredUrl);

        firstAttempt.Dispose();

        var secondAttempt = await SendEvolutionRequestAsync(legacyUrl, legacyApiKey, json, salvarLogCompleto);
        var secondContent = await secondAttempt.Content.ReadAsStringAsync();
        return (secondAttempt, secondContent, legacyUrl);
    }

    private Task<HttpResponseMessage> SendEvolutionRequestAsync(string url, string apiKey, string json, bool salvarLogCompleto)
    {
        if (salvarLogCompleto)
        {
            _logger.LogInformation("📤 Evolution API request → URL: {Url} | Payload: {Payload}", url, json);
        }
        else
        {
            _logger.LogInformation("📤 Evolution API request → URL: {Url}", url);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("apikey", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _httpClient.SendAsync(request);
    }

    private static bool TryGetEvolutionInstanceName(JsonElement item, out string? name)
    {
        name = null;

        if (item.TryGetProperty("name", out var nameEl))
            name = nameEl.GetString();
        else if (item.TryGetProperty("instance_name", out var instanceNameEl))
            name = instanceNameEl.GetString();
        else if (item.TryGetProperty("instanceName", out var instanceNameEl2))
            name = instanceNameEl2.GetString();

        return !string.IsNullOrWhiteSpace(name);
    }

    private static string? ExtractEvolutionMessageId(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("key", out var keyEl) && keyEl.TryGetProperty("id", out var idEl))
                return idEl.GetString();

            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.TryGetProperty("key", out var dataKeyEl) && dataKeyEl.TryGetProperty("id", out var dataIdEl))
                    return dataIdEl.GetString();

                if (dataEl.TryGetProperty("id", out var directIdEl))
                    return directIdEl.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string GetEvolutionFileName(string mediaUrl, string mediaType)
    {
        if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
                return fileName;
        }

        return mimeTypeToFileName(mediaType);

        static string mimeTypeToFileName(string mediaType) => mediaType.ToLower() switch
        {
            "jpg" or "jpeg" => "image.jpg",
            "png" => "image.png",
            "gif" => "image.gif",
            "mp4" => "video.mp4",
            "mov" => "video.mov",
            "avi" => "video.avi",
            "mp3" => "audio.mp3",
            "ogg" => "audio.ogg",
            "m4a" => "audio.m4a",
            "wav" => "audio.wav",
            "pdf" => "document.pdf",
            "doc" => "document.doc",
            "docx" => "document.docx",
            "xls" => "document.xls",
            "xlsx" => "document.xlsx",
            _ => "attachment"
        };
    }

    private string GetLastEightDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length <= 8) return digits;
        return digits.Substring(digits.Length - 8);
    }

    private string GetMediaMessageType(string? mediaType)
    {
        if (string.IsNullOrEmpty(mediaType)) return "document";
        return mediaType.ToLower() switch
        {
            "jpg" or "jpeg" or "png" or "gif" => "image",
            "pdf" or "doc" or "docx" or "xls" or "xlsx" => "document",
            "mp3" or "ogg" or "m4a" => "audio",
            "mp4" or "mov" or "avi" => "video",
            _ => "document"
        };
    }

    private MessageType GetMessageTypeFromString(string? type)
    {
        if (string.IsNullOrEmpty(type)) return MessageType.Text;
        return type.ToLower() switch
        {
            "text" => MessageType.Text,
            "image" => MessageType.Image,
            "audio" => MessageType.Audio,
            "video" => MessageType.Video,
            "document" => MessageType.Document,
            "sticker" => MessageType.Sticker,
            "location" => MessageType.Location,
            "contacts" => MessageType.Contact,
            "interactive" => MessageType.Interactive,
            _ => MessageType.Text
        };
    }

    private async Task<HttpResponseMessage> SendWhatsAppRequest(string phoneNumberId, string accessToken, string endpoint, object payload, bool salvarLogCompleto)
    {
        var url = $"{VersionedApiBaseUrl}/{phoneNumberId}/{endpoint}";
        var json = JsonSerializer.Serialize(payload);

        if (salvarLogCompleto)
        {
            _logger.LogInformation("📤 WhatsApp request → URL: {Url} | PhoneNumberId: {PhoneId} | Payload: {Payload}",
                url, phoneNumberId, json);
        }
        else
        {
            _logger.LogInformation("📤 WhatsApp request → URL: {Url} | PhoneNumberId: {PhoneId}",
                url, phoneNumberId);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return await _httpClient.SendAsync(request);
    }

    private async Task<WhatsAppMessageResponse> ProcessSendResponse(HttpResponseMessage response, string farmaciaId, string to, string messageText, bool salvarLogCompleto)
    {
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (salvarLogCompleto)
            {
                _logger.LogInformation("📥 WhatsApp response → FarmaciaId: {FarmaciaId} | To: {To} | Status: {StatusCode} | Content: {Content}",
                    farmaciaId, to, response.StatusCode, responseContent);
            }

            var doc = JsonDocument.Parse(responseContent);
            string? messageId = null;
            if (doc.RootElement.TryGetProperty("messages", out var messagesElement) &&
                messagesElement.ValueKind == JsonValueKind.Array &&
                messagesElement.GetArrayLength() > 0)
            {
                var firstMessage = messagesElement[0];
                if (firstMessage.TryGetProperty("id", out var idElement))
                {
                    messageId = idElement.GetString();
                }
            }

            _logger.LogInformation(
                "✅ WhatsApp message sent via Meta API - MessageId: {MessageId}, To: {To}",
                messageId, to);

            return new WhatsAppMessageResponse
            {
                Success = true,
                MessageId = messageId,
                Timestamp = DateTime.UtcNow
            };
        }
        else
        {
            if (salvarLogCompleto)
            {
                _logger.LogError("Error sending WhatsApp message. FarmaciaId: {FarmaciaId}, To: {To}, Status: {StatusCode}, Response: {Response}",
                    farmaciaId, to, response.StatusCode, responseContent);
            }
            else
            {
                _logger.LogError("Error sending WhatsApp message. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
            }

            return new WhatsAppMessageResponse
            {
                Success = false,
                ErrorCode = ((int)response.StatusCode).ToString(),
                ErrorMessage = responseContent
            };
        }
    }

    private WhatsAppMessageResponse CreateErrorResponse(string message)
    {
        return new WhatsAppMessageResponse
        {
            Success = false,
            ErrorMessage = message,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task NotifyNewMessage(string? messageId, string farmaciaId)
    {
        await _hubContext.Clients.Group($"farmacia-{farmaciaId}")
            .SendAsync("NewMessageNotification", new
            {
                MessageId = messageId,
                FarmaciaId = farmaciaId,
                Timestamp = DateTime.UtcNow
            });
    }

    private async Task<(Cliente cliente, bool isNew, bool isInactive, bool isBlocked)> GetOrCreateCliente(int farmaciaId, string? whatsappId, string? contactName = null)
    {
        var normalizedWhatsAppId = NormalizeWhatsAppId(whatsappId);
        if (string.IsNullOrWhiteSpace(normalizedWhatsAppId))
            throw new ArgumentException("Identificador WhatsApp inválido.", nameof(whatsappId));

        var whatsappComCodigoPais = $"55{normalizedWhatsAppId}";
        var clientesBaseQuery = _dbContext.Clientes.Where(c => c.FarmaciaId == farmaciaId);
        var clientes = await clientesBaseQuery
            .Where(c =>
                c.WhatsAppId == normalizedWhatsAppId ||
                c.WhatsAppId == whatsappComCodigoPais ||
                c.WhatsAppId.EndsWith(normalizedWhatsAppId) ||
                c.WhatsAppId.EndsWith(whatsappComCodigoPais))
            .ToListAsync();
        if (clientes.Count == 0)
            clientes = await clientesBaseQuery.ToListAsync();

        var cliente = clientes.FirstOrDefault(c =>
            string.Equals(NormalizeWhatsAppId(c.WhatsAppId), normalizedWhatsAppId, StringComparison.Ordinal) ||
            NormalizeWhatsAppId(c.WhatsAppId).IsEquivalentWhatsAppIdentifier(normalizedWhatsAppId));

        var trimmedContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();

        if (cliente == null)
        {
            cliente = new Cliente
            {
                FarmaciaId = farmaciaId,
                WhatsAppId = normalizedWhatsAppId,
                Nome = trimmedContactName ?? $"Cliente WhatsApp {normalizedWhatsAppId}",
                DataCadastro = DateTime.UtcNow,
                Ativo = true,
                Status = ClienteStatus.Ativo,
                OrigemCadastro = Consts.ClienteDefaults.OrigemCadastro,
                UltimoContato = DateTime.UtcNow,
                CriadoPor = Consts.ClienteDefaults.CriadoPor,
                PerfilCompra = ClientePerfilCompraEnum.Prospecto,
                Farmacia = null
            };
            _dbContext.Clientes.Add(cliente);
            await _dbContext.SaveChangesAsync();
            return (cliente, true, false, false);
        }

        var normalizedIdUpdated = false;
        var normalizedCurrentId = NormalizeWhatsAppId(cliente.WhatsAppId);
        var preferredWhatsAppId = normalizedCurrentId.SelectPreferredWhatsAppIdentifier(whatsappId);
        if (!string.Equals(cliente.WhatsAppId, preferredWhatsAppId, StringComparison.Ordinal))
        {
            cliente.WhatsAppId = preferredWhatsAppId;
            normalizedIdUpdated = true;
        }

        var nomeAtualEhPlaceholder = string.IsNullOrWhiteSpace(cliente.Nome) ||
            cliente.Nome.StartsWith("Cliente WhatsApp ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cliente.Nome, normalizedCurrentId, StringComparison.OrdinalIgnoreCase);
        var deveAtualizarNomeComContato = trimmedContactName != null &&
            nomeAtualEhPlaceholder &&
            !string.Equals(cliente.Nome, trimmedContactName, StringComparison.Ordinal);
        if (deveAtualizarNomeComContato)
        {
            cliente.Nome = trimmedContactName;
            normalizedIdUpdated = true;
        }

        if (cliente.Status == ClienteStatus.Inativo)
        {
            if (normalizedIdUpdated)
                await _dbContext.SaveChangesAsync();
            return (cliente, false, true, false);
        }

        if (cliente.Status == ClienteStatus.Bloqueado)
        {
            if (normalizedIdUpdated)
                await _dbContext.SaveChangesAsync();
            return (cliente, false, false, true);
        }

        if (normalizedIdUpdated)
            await _dbContext.SaveChangesAsync();

        return (cliente, false, false, false);
    }

    /// <summary>
    /// Extrai o nome de contato (perfil do WhatsApp/Meta) do payload oficial da Cloud API,
    /// buscando o objeto correspondente em <c>value.contacts[]</c> pelo <c>wa_id</c> do remetente.
    /// A Meta expõe esse nome em <c>contacts[].profile.name</c>, incluindo o nome de usuário (username)
    /// configurado pelo contato no WhatsApp.
    /// </summary>
    private static string? ExtractMetaContactName(JsonElement value, string? fromRaw)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("contacts", out var contacts) ||
            contacts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var normalizedFrom = fromRaw.NormalizeWhatsAppIdentifier();

        string? fallbackName = null;
        foreach (var contact in contacts.EnumerateArray())
        {
            if (contact.ValueKind != JsonValueKind.Object)
                continue;

            if (!contact.TryGetProperty("profile", out var profile) ||
                profile.ValueKind != JsonValueKind.Object ||
                !profile.TryGetProperty("name", out var nameEl) ||
                nameEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            fallbackName ??= name;

            var waId = contact.TryGetProperty("wa_id", out var waIdEl) ? waIdEl.GetString() : null;
            var normalizedWaId = waId.NormalizeWhatsAppIdentifier();
            if (!string.IsNullOrWhiteSpace(normalizedWaId) &&
                (string.Equals(normalizedWaId, normalizedFrom, StringComparison.Ordinal) ||
                 normalizedWaId.IsEquivalentWhatsAppIdentifier(normalizedFrom)))
            {
                return name;
            }
        }

        return fallbackName;
    }

    private async Task<Atendimento> GetOrCreateAtendimento(int farmaciaId, int clienteId, string whatsappId)
    {
        var statusPendente = AtendimentoStatusEnum.Pendente.ToString();
        var statusEmAndamento = AtendimentoStatusEnum.EmAndamento.ToString();
        var atendimento = await _dbContext.Atendimentos
            .Where(a => a.FarmaciaId == farmaciaId &&
                       a.ClienteId == clienteId &&
                       (a.StatusEnum == AtendimentoStatusEnum.Pendente ||
                        a.StatusEnum == AtendimentoStatusEnum.EmAndamento))
            .OrderByDescending(a => a.DataInicio)
            .FirstOrDefaultAsync();

        if (atendimento == null)
        {
            atendimento = new Atendimento
            {
                FarmaciaId = farmaciaId,
                ClienteId = clienteId,
                WhatsAppId = whatsappId,
                CanalEnum = CanalEnum.WhatsApp,
                StatusEnum = AtendimentoStatusEnum.Pendente,
                DataInicio = DateTime.UtcNow,
                Farmacia = null!,
                Cliente = null!,
                Atendente = null!
            };
        }

        return atendimento;
    }

    private async Task ProcessMessageContent(JsonElement message, Mensagem mensagem, string? type)
    {
        switch ((type ?? "").ToLower())
        {
            case "text":
                mensagem.Text = message.GetProperty("text").GetProperty("body").GetString();
                break;

            case "image":
            case "audio":
            case "video":
            case "document":
            case "sticker":
                var media = message.GetProperty(type!);
                mensagem.MediaUrl = await DownloadAndStoreMedia(media);
                mensagem.MediaType = type;
                mensagem.MediaCaption = media.TryGetProperty("caption", out var caption) ?
                    caption.GetString() : null;
                break;

            case "location":
                var location = message.GetProperty("location");
                mensagem.Text = $"📍 Localização: {location.GetProperty("latitude").GetDouble()}, " +
                               $"{location.GetProperty("longitude").GetDouble()}";
                break;

            case "contacts":
                var contacts = message.GetProperty("contacts");
                mensagem.Text = "📇 Contato recebido";
                break;

            case "interactive":
                var interactive = message.GetProperty("interactive");
                var interactiveType = interactive.GetProperty("type").GetString();
                mensagem.Text = $"🔄 Interativo: {interactiveType}";
                break;
        }
    }

    private async Task<string?> DownloadAndStoreMedia(JsonElement mediaElement)
    {
        try
        {
            var mediaId = mediaElement.GetProperty("id").GetString();
            var mediaUrl = $"{_config.ApiBaseUrl}/{mediaId}";

            // Em produção, implementar download e upload para Azure Blob Storage
            return $"https://pharmazapplus.blob.core.windows.net/media/{mediaId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading media");
            return null;
        }
    }

    private async Task<WebhookProcessingResult> ProcessTemplateStatusUpdate(JsonElement value)
    {
        try
        {
            var messageId = value.GetProperty("message_id").GetString();
            var eventType = value.GetProperty("event").GetString();
            var timestamp = value.GetProperty("timestamp").GetInt64();

            var mensagem = await _dbContext.Mensagens
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (mensagem != null)
            {
                var status = (eventType ?? "").ToLower() switch
                {
                    "sent" => MessageStatus.Enviada,
                    "delivered" => MessageStatus.Entregue,
                    "read" => MessageStatus.Lida,
                    "failed" => MessageStatus.Falha,
                    _ => MessageStatus.Processando
                };

                mensagem.Status = status;

                switch (status)
                {
                    case MessageStatus.Entregue:
                        mensagem.DeliveredAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                        break;
                    case MessageStatus.Lida:
                        mensagem.ReadAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                        break;
                }

                await _dbContext.SaveChangesAsync();
                await NotifyMessageStatusUpdate(mensagem.AtendimentoId, messageId, status);
            }

            return new WebhookProcessingResult
            {
                Success = true,
                Message = $"Status do template atualizado: {eventType}",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar atualização de status de template");
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Erro ao processar status: {ex.Message}",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
    }

    private async Task<WebhookProcessingResult> ProcessIncomingMessage(JsonElement value)
    {
        try
        {
            var phoneNumberId = value.GetProperty("metadata").GetProperty("phone_number_id").GetString();
            var farmacia = await _dbContext.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Integracoes != null && f.Integracoes.WhatsAppPhoneNumberId == phoneNumberId);

            if (farmacia == null || farmacia.Integracoes == null)
            {
                _logger.LogWarning("Farmácia não encontrada para phone_number_id: {PhoneNumberId}", phoneNumberId);
                return new WebhookProcessingResult
                {
                    Success = false,
                    Message = "Farmácia não encontrada",
                    ProcessedMessage = null,
                    Atendimento = null
                };
            }

            var messages = value.GetProperty("messages");
            foreach (var message in messages.EnumerateArray())
            {
                var fromRaw = message.GetProperty("from").GetString();
                if (string.IsNullOrWhiteSpace(fromRaw))
                {
                    _logger.LogWarning("Mensagem recebida com remetente inválido. FarmaciaId: {FarmaciaId}", farmacia.Id);
                    continue;
                }
                var from = fromRaw.NormalizeWhatsAppIdentifier();
                var messageId = message.GetProperty("id").GetString();
                var timestamp = message.GetProperty("timestamp").GetInt64();
                var type = message.GetProperty("type").GetString();

                var existingMessage = await _dbContext.Mensagens
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);

                if (existingMessage != null)
                {
                    _logger.LogInformation("Mensagem já processada: {MessageId}", messageId);
                    continue;
                }

                var contactName = ExtractMetaContactName(value, fromRaw);
                var (cliente, newClientCreated, isInactive, isBlocked) = await GetOrCreateCliente(farmacia.Id, fromRaw, contactName);

                if (isBlocked)
                {
                    _logger.LogWarning("Mensagem de cliente bloqueado ignorada: {WhatsAppId}, FarmaciaId: {FarmaciaId}", from, farmacia.Id);
                    continue;
                }

                if (isInactive)
                {
                    _logger.LogInformation("Mensagem de cliente inativo recibida: {WhatsAppId}, FarmaciaId: {FarmaciaId}", from, farmacia.Id);

                    await NotifyAtendenteNovoAtendimentoInativo(farmacia.Id, cliente.Id, messageId);

                    continue;
                }

                var atendimento = await GetOrCreateAtendimento(farmacia.Id, cliente.Id, fromRaw);
                var newAtendimentoCreated = atendimento.Id == 0;

                if (newAtendimentoCreated)
                {
                    await _dbContext.Atendimentos.AddAsync(atendimento);
                    await _dbContext.SaveChangesAsync();
                }

                var mensagem = new Mensagem
                {
                    MessageId = messageId,
                    AtendimentoId = atendimento.Id,
                    Direction = MessageDirection.Inbound,
                    Type = GetMessageTypeFromString(type),
                    Status = MessageStatus.Recebida,
                    ReceivedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime
                };

                await ProcessMessageContent(message, mensagem, type);

                await _dbContext.Mensagens.AddAsync(mensagem);
                await _dbContext.SaveChangesAsync();

                cliente.AtualizarUltimoContato();
                _dbContext.Clientes.Update(cliente);
                await _dbContext.SaveChangesAsync();

                await _messageQueue.EnqueueAsync("new-messages", new
                {
                    FarmaciaId = farmacia.Id,
                    AtendimentoId = atendimento.Id,
                    MensagemId = mensagem.Id,
                    MessageType = type,
                    IsNewAtendimento = newAtendimentoCreated
                });

                return new WebhookProcessingResult
                {
                    Success = true,
                    Message = "Mensagem processada com sucesso",
                    ProcessedMessage = mensagem,
                    Atendimento = atendimento,
                    NewAtendimentoCreated = newAtendimentoCreated,
                    NewClientCreated = newClientCreated
                };
            }

            return new WebhookProcessingResult
            {
                Success = true,
                Message = "Nenhuma mensagem nova para processar",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing incoming message");
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Erro ao processar mensagem: {ex.Message}",
                ProcessedMessage = null,
                Atendimento = null
            };
        }
    }

    private static string NormalizeWhatsAppId(string? input)
    {
        return input.NormalizeWhatsAppIdentifier();
    }

    private async Task NotifyMessageStatusUpdate(int atendimentoId, string? messageId, MessageStatus status)
    {
        try
        {
            var atendimento = await _dbContext.Atendimentos
                .FirstOrDefaultAsync(a => a.Id == atendimentoId);

            if (atendimento != null)
            {
                await _hubContext.Clients.Group($"atendimento-{atendimentoId}")
                    .SendAsync("MessageStatusUpdated", new
                    {
                        MessageId = messageId,
                        Status = status.ToString(),
                        Timestamp = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar atualização de status da mensagem");
        }
    }

    private async Task<HttpResponseMessage> SendMetaApiRequest(string endpoint, string accessToken, object? payload, HttpMethod method)
    {
        var url = $"{_config.ApiBaseUrl}/{endpoint}";

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task<Dictionary<string, object>> GetDatabaseMetrics(string farmaciaId, DateTime startDate, DateTime endDate)
    {
        if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
            return new Dictionary<string, object>();

        var metrics = new Dictionary<string, object>();

        var mensagensEnviadas = await _dbContext.Mensagens
            .Where(m => m.Atendimento!.FarmaciaId == farmaciaIdInt &&
                       m.Direction == MessageDirection.Outbound &&
                       m.SentAt >= startDate && m.SentAt <= endDate)
            .CountAsync();

        var mensagensRecebidas = await _dbContext.Mensagens
            .Where(m => m.Atendimento!.FarmaciaId == farmaciaIdInt &&
                       m.Direction == MessageDirection.Inbound &&
                       m.ReceivedAt >= startDate && m.ReceivedAt <= endDate)
            .CountAsync();

        var mensagensEntregues = await _dbContext.Mensagens
            .Where(m => m.Atendimento!.FarmaciaId == farmaciaIdInt &&
                       m.Direction == MessageDirection.Outbound &&
                       m.Status == MessageStatus.Entregue &&
                       m.DeliveredAt >= startDate && m.DeliveredAt <= endDate)
            .CountAsync();

        var mensagensLidas = await _dbContext.Mensagens
            .Where(m => m.Atendimento!.FarmaciaId == farmaciaIdInt &&
                       m.Direction == MessageDirection.Outbound &&
                       m.Status == MessageStatus.Lida &&
                       m.ReadAt >= startDate && m.ReadAt <= endDate)
            .CountAsync();

        metrics["database_messages_sent"] = mensagensEnviadas;
        metrics["database_messages_received"] = mensagensRecebidas;
        metrics["database_delivery_rate"] = mensagensEnviadas > 0 ?
            (decimal)mensagensEntregues / mensagensEnviadas * 100 : 0;
        metrics["database_read_rate"] = mensagensEnviadas > 0 ?
            (decimal)mensagensLidas / mensagensEnviadas * 100 : 0;

        var atendimentos = await _dbContext.Atendimentos
            .Where(a => a.FarmaciaId == farmaciaIdInt &&
                       a.DataInicio >= startDate && a.DataInicio <= endDate)
            .ToListAsync();

        metrics["database_total_atendimentos"] = atendimentos.Count;
        metrics["database_atendimentos_finalizados"] = atendimentos.Count(a => a.Status == "Finalizado");
        metrics["database_tempo_medio_atendimento"] = atendimentos
            .Where(a => a.DuracaoSegundos.HasValue)
            .Average(a => a.DuracaoSegundos) ?? 0;

        var novosClientes = await _dbContext.Clientes
            .Where(c => c.FarmaciaId == farmaciaIdInt &&
                       c.DataCadastro >= startDate && c.DataCadastro <= endDate)
            .CountAsync();

        metrics["database_novos_clientes"] = novosClientes;

        return metrics;
    }

    private async Task NotifyAtendenteNovoAtendimentoInativo(int farmaciaId, int clienteId, string? messageId)
    {
        var cliente = await _dbContext.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId);

        var notification = new
        {
            Tipo = "cliente_inativo",
            ClienteId = clienteId,
            ClienteNome = cliente?.Nome ?? "Desconhecido",
            WhatsAppId = cliente?.WhatsAppId,
            MessageId = messageId,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId.ToString()))
            .SendAsync(Consts.SignalR.Metodos.NovaMensagemRecebida, notification);

        _logger.LogInformation("Notificação de cliente inativo enviada: ClienteId {ClienteId}, FarmaciaId {FarmaciaId}", clienteId, farmaciaId);
    }

    /// <summary>
    /// Registra em log o processo de formatação de números de telefone para diagnóstico
    /// </summary>
    private void LogPhoneNumberFormatting(string? original, string? formatted, string context)
    {
        _logger.LogInformation(
            "📱 Phone formatting [{Context}] - Original: '{Original}' -> Formatted: '{Formatted}' | " +
            "Digits Original: {OriginalDigits} | Digits Formatted: {FormattedDigits}",
            context,
            original,
            formatted,
            string.IsNullOrEmpty(original) ? "" : new string(original.Where(char.IsDigit).ToArray()),
            string.IsNullOrEmpty(formatted) ? "" : new string(formatted.Where(char.IsDigit).ToArray()));
    }

    #endregion
}
