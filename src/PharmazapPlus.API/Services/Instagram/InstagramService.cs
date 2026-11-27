using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Services.Instagram;

public class InstagramService : IInstagramService
{
    private readonly HttpClient _httpClient;
    private readonly PharmazapDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InstagramService> _logger;
    private readonly InstagramConfig _config;
    private readonly IMessageQueue _messageQueue;
    private readonly IHubContext<PharmaHub> _hubContext;
    private readonly ICrmService _crmService;
    private readonly ICryptoService _cryptoService;

    public InstagramService(
        HttpClient httpClient,
        PharmazapDbContext dbContext,
        IDistributedCache cache,
        ILogger<InstagramService> logger,
        IOptions<InstagramConfig> config,
        IMessageQueue messageQueue,
        IHubContext<PharmaHub> hubContext,
        ICrmService crmService,
        ICryptoService cryptoService)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _config = config.Value;
        _messageQueue = messageQueue;
        _hubContext = hubContext;
        _crmService = crmService;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Versioned base URL for Instagram Graph API calls (e.g. https://graph.instagram.com/v21.0).
    /// </summary>
    private string VersionedApiBaseUrl => $"{_config.ApiBaseUrl}/{_config.ApiVersion}";

    #region Webhook

    public Task<bool> ValidateWebhook(string mode, string token, string challenge)
    {
        try
        {
            _logger.LogInformation("Instagram webhook verification: Mode={Mode}", mode);

            var verifyToken = _config.WebhookVerifyToken;
            if (mode == "subscribe" && token == verifyToken)
            {
                _logger.LogInformation("Instagram webhook verification successful");
                return Task.FromResult(true);
            }

            _logger.LogWarning("Instagram webhook verification failed");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Instagram webhook");
            return Task.FromResult(false);
        }
    }

    public async Task<WebhookProcessingResult> ProcessWebhookPayload(Dictionary<string, object> payload)
    {
        try
        {
            _logger.LogInformation("Processing Instagram webhook payload");

            // Instagram webhooks come under "instagram" object field
            // Structure: { object: "instagram", entry: [ { id, time, messaging: [ ... ] } ] }
            if (!payload.TryGetValue("object", out var objectField) || objectField?.ToString() != "instagram")
            {
                return new WebhookProcessingResult
                {
                    Success = false,
                    Message = "Not an Instagram webhook payload"
                };
            }

            if (!payload.TryGetValue("entry", out var entryObj))
            {
                return new WebhookProcessingResult
                {
                    Success = false,
                    Message = "No entry found in payload"
                };
            }

            var entries = JsonSerializer.Deserialize<List<JsonElement>>(entryObj.ToString()!);
            if (entries == null || entries.Count == 0)
            {
                return new WebhookProcessingResult
                {
                    Success = false,
                    Message = "Empty entries in payload"
                };
            }

            WebhookProcessingResult? lastResult = null;

            foreach (var entry in entries)
            {
                if (!entry.TryGetProperty("messaging", out var messagingArray))
                    continue;

                foreach (var messagingEvent in messagingArray.EnumerateArray())
                {
                    lastResult = await ProcessMessagingEvent(messagingEvent);
                }
            }

            return lastResult ?? new WebhookProcessingResult
            {
                Success = true,
                Message = "Webhook processed (no messaging events)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Instagram webhook payload");
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<WebhookProcessingResult> ProcessMessagingEvent(JsonElement messagingEvent)
    {
        var senderId = messagingEvent.GetProperty("sender").GetProperty("id").GetString()!;
        var recipientId = messagingEvent.GetProperty("recipient").GetProperty("id").GetString()!;

        string? messageText = null;
        string? messageType = "text";

        if (messagingEvent.TryGetProperty("message", out var message))
        {
            if (message.TryGetProperty("text", out var text))
                messageText = text.GetString();

            if (message.TryGetProperty("attachments", out var attachments))
            {
                var firstAttachment = attachments.EnumerateArray().FirstOrDefault();
                if (firstAttachment.ValueKind != JsonValueKind.Undefined)
                {
                    messageType = firstAttachment.GetProperty("type").GetString() ?? "unknown";
                    if (firstAttachment.TryGetProperty("payload", out var attachPayload) &&
                        attachPayload.TryGetProperty("url", out var url))
                    {
                        messageText = url.GetString();
                    }
                }
            }
        }

        // Find or create Atendimento
        var farmacia = await ResolveFarmaciaForInstagramRecipientAsync(recipientId);

        if (farmacia == null)
        {
            _logger.LogWarning("No farmacia found for Instagram recipient {RecipientId}", recipientId);
            return new WebhookProcessingResult { Success = false, Message = "Farmacia not found" };
        }

        var cliente = await _crmService.GetOrCreateClienteByCanalContato(
            farmacia.Id.ToString(),
            senderId,
            CanalEnum.Instagram);

        var atendimento = await _dbContext.Atendimentos
            .FirstOrDefaultAsync(a =>
                a.FarmaciaId == farmacia.Id &&
                a.CanalEnum == CanalEnum.Instagram &&
                (a.ClienteId == cliente.Id || a.WhatsAppId == senderId) &&
                a.StatusEnum != AtendimentoStatusEnum.Finalizado);

        var isNew = false;
        if (atendimento == null)
        {
            atendimento = new Atendimento
            {
                FarmaciaId = farmacia.Id,
                Farmacia = null!,
                ClienteId = cliente.Id,
                Cliente = null!,
                Atendente = null!,
                WhatsAppId = senderId, // Instagram Scoped User ID (IGSID)
                CanalEnum = CanalEnum.Instagram,
                StatusEnum = AtendimentoStatusEnum.Pendente,
                DataInicio = DateTime.UtcNow
            };
            _dbContext.Atendimentos.Add(atendimento);
            await _dbContext.SaveChangesAsync();
            isNew = true;
        }
        else if (atendimento.ClienteId == null)
        {
            atendimento.ClienteId = cliente.Id;
            await _dbContext.SaveChangesAsync();
        }

        var msgType = messageType switch
        {
            "image" => MessageType.Image,
            "video" => MessageType.Video,
            "audio" => MessageType.Audio,
            "file" => MessageType.Document,
            _ => MessageType.Text
        };

        var mensagem = new Mensagem
        {
            AtendimentoId = atendimento.Id,
            MessageId = $"ig-{Guid.NewGuid()}",
            Direction = MessageDirection.Inbound,
            Type = msgType,
            Status = MessageStatus.Recebida,
            Text = messageText ?? "",
            ReceivedAt = DateTime.UtcNow
        };
        _dbContext.Mensagens.Add(mensagem);
        await _dbContext.SaveChangesAsync();

        // Notify via SignalR
        try
        {
            var grupo = Consts.SignalR.GrupoFarmacia(farmacia.Id.ToString());
            await _hubContext.Clients.Group(grupo)
                .SendAsync(Consts.SignalR.Metodos.NovaMensagemRecebida, new
                {
                    AtendimentoId = atendimento.Id,
                    Canal = "Instagram",
                    MensagemId = mensagem.Id,
                    Texto = messageText
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify via SignalR for Instagram message");
        }

        // Enqueue for processing
        try
        {
            await _messageQueue.EnqueueAsync(Consts.Filas.NovasMensagens, new
            {
                FarmaciaId = farmacia.Id.ToString(),
                AtendimentoId = atendimento.Id,
                IsNewAtendimento = isNew,
                Canal = "Instagram"
            });
        }
        catch (Exception queueEx)
        {
            _logger.LogWarning(queueEx, "Could not enqueue Instagram message notification");
        }

        return new WebhookProcessingResult
        {
            Success = true,
            Message = $"Instagram message processed from {senderId}",
            ProcessedMessage = mensagem,
            Atendimento = atendimento,
            NewAtendimentoCreated = isNew
        };
    }

    private async Task<Farmacia?> ResolveFarmaciaForInstagramRecipientAsync(string recipientId)
    {
        var normalizedRecipientId = recipientId.Trim();

        var farmaciaIdFromChannel = await _dbContext.Canais
            .AsNoTracking()
            .Where(c =>
                c.Tipo == (int)CanalEnum.Instagram &&
                c.InstagramAccountId != null &&
                c.InstagramAccountId.Trim() == normalizedRecipientId &&
                c.FarmaciaIntegracoes != null)
            .Select(c => (int?)c.FarmaciaIntegracoes!.FarmaciaId)
            .FirstOrDefaultAsync();

        if (farmaciaIdFromChannel.HasValue)
        {
            var farmaciaFromChannel = await _dbContext.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id == farmaciaIdFromChannel.Value);

            if (farmaciaFromChannel != null)
            {
                _logger.LogInformation(
                    "Instagram recipient {RecipientId} resolved via channel table to farmacia {FarmaciaId}",
                    normalizedRecipientId,
                    farmaciaFromChannel.Id);
                return farmaciaFromChannel;
            }
        }

        var farmaciaLegacy = await _dbContext.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Integracoes != null &&
                (f.Integracoes.InstagramAccountId == normalizedRecipientId || f.Integracoes.PageId == normalizedRecipientId));

        if (farmaciaLegacy != null)
        {
            _logger.LogInformation(
                "Instagram recipient {RecipientId} resolved via legacy integration fields to farmacia {FarmaciaId}",
                normalizedRecipientId,
                farmaciaLegacy.Id);
        }

        return farmaciaLegacy;
    }

    #endregion

    #region Send Messages

    public async Task<InstagramMessageResponse> SendTextMessage(string farmaciaId, string recipientId, string text)
    {
        try
        {
            var farmacia = await GetFarmaciaWithInstagramConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Instagram não configurado");

            // Instagram Messaging API: POST /me/messages
            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text }
            };

            return await SendInstagramRequest(farmacia.PageId, farmacia.InstagramAccessToken, payload, farmacia.SalvarLogCompletoMidiaApiGo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Instagram text message to {Recipient}", recipientId);
            return CreateErrorResponse(ex.Message);
        }
    }

    public async Task<InstagramMessageResponse> SendMediaMessage(string farmaciaId, string recipientId, string mediaUrl, string mediaType, string? caption = null)
    {
        try
        {
            var farmacia = await GetFarmaciaWithInstagramConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Instagram não configurado");

            // Instagram supports image, video, audio, file attachments
            var attachment = new Dictionary<string, object>
            {
                ["type"] = mediaType,
                ["payload"] = new { url = mediaUrl, is_reusable = true }
            };

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { attachment }
            };

            var result = await SendInstagramRequest(farmacia.PageId, farmacia.InstagramAccessToken, payload, farmacia.SalvarLogCompletoMidiaApiGo);

            // If there's a caption, send it as a follow-up text
            if (result.Success && !string.IsNullOrEmpty(caption))
            {
                await SendTextMessage(farmaciaId, recipientId, caption!);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Instagram media message to {Recipient}", recipientId);
            return CreateErrorResponse(ex.Message);
        }
    }

    private static readonly HashSet<string> ValidButtonTypes = new(StringComparer.OrdinalIgnoreCase) { "web_url", "postback" };

    public async Task<InstagramMessageResponse> SendTemplateMessage(string farmaciaId, string recipientId, string templateTitle, string templateSubtitle, List<InstagramButton>? buttons = null, string? imageUrl = null, string? defaultActionUrl = null)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(recipientId))
                return CreateErrorResponse("Destinatário (recipientId) é obrigatório");

            if (string.IsNullOrWhiteSpace(templateTitle))
                return CreateErrorResponse("Título do template é obrigatório");

            // Validate button types before sending to Meta
            if (buttons != null)
            {
                if (buttons.Count > 3)
                    return CreateErrorResponse("Generic Templates do Instagram suportam no máximo 3 botões");

                foreach (var b in buttons)
                {
                    if (!ValidButtonTypes.Contains(b.Type))
                        return CreateErrorResponse($"Tipo de botão inválido: '{b.Type}'. Tipos válidos: 'web_url', 'postback'");

                    if (string.IsNullOrWhiteSpace(b.Title))
                        return CreateErrorResponse("Todos os botões devem ter um título (title)");

                    if (b.Type.Equals("web_url", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(b.Url) && string.IsNullOrWhiteSpace(b.Payload))
                        return CreateErrorResponse("Botões do tipo 'web_url' devem ter uma URL");

                    if (b.Type.Equals("postback", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(b.Payload))
                        return CreateErrorResponse("Botões do tipo 'postback' devem ter um payload");
                }
            }

            var farmacia = await GetFarmaciaWithInstagramConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Instagram não configurado");

            // Instagram uses Generic Template via Send API
            // Ref: https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/messaging-api
            var element = new Dictionary<string, object>
            {
                ["title"] = templateTitle,
                ["subtitle"] = templateSubtitle ?? ""
            };

            // image_url is optional per Meta docs
            if (!string.IsNullOrEmpty(imageUrl))
                element["image_url"] = imageUrl;

            // default_action is an optional web_url action triggered when the template is tapped
            if (!string.IsNullOrEmpty(defaultActionUrl))
            {
                element["default_action"] = new Dictionary<string, object>
                {
                    ["type"] = "web_url",
                    ["url"] = defaultActionUrl
                };
            }

            // Map buttons: web_url buttons need "url", postback buttons need "payload"
            if (buttons != null && buttons.Count > 0)
            {
                var mappedButtons = buttons.Select(b =>
                {
                    var btn = new Dictionary<string, object>
                    {
                        ["type"] = b.Type,
                        ["title"] = b.Title
                    };

                    if (b.Type.Equals("web_url", StringComparison.OrdinalIgnoreCase))
                        btn["url"] = b.Url ?? b.Payload;
                    else // postback
                        btn["payload"] = b.Payload;

                    return btn;
                }).ToList();

                element["buttons"] = mappedButtons;
            }

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new
                {
                    attachment = new
                    {
                        type = "template",
                        payload = new
                        {
                            template_type = "generic",
                            elements = new[] { element }
                        }
                    }
                }
            };

            return await SendInstagramRequest(farmacia.PageId, farmacia.InstagramAccessToken, payload, farmacia.SalvarLogCompletoMidiaApiGo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Instagram template message to {Recipient}", recipientId);
            return CreateErrorResponse(ex.Message);
        }
    }

    public async Task<InstagramMessageResponse> SendInteractiveMessage(string farmaciaId, string recipientId, string headerText, string bodyText, List<InstagramButton> buttons)
    {
        try
        {
            var farmacia = await GetFarmaciaWithInstagramConfig(farmaciaId);
            if (farmacia == null)
                return CreateErrorResponse("Farmácia não encontrada ou Instagram não configurado");

            // Instagram interactive: Quick Replies
            var quickReplies = buttons.Select(b => new Dictionary<string, object>
            {
                ["content_type"] = "text",
                ["title"] = b.Title,
                ["payload"] = b.Payload
            }).ToList();

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new
                {
                    text = $"{headerText}\n{bodyText}",
                    quick_replies = quickReplies
                }
            };

            return await SendInstagramRequest(farmacia.PageId, farmacia.InstagramAccessToken, payload, farmacia.SalvarLogCompletoMidiaApiGo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Instagram interactive message to {Recipient}", recipientId);
            return CreateErrorResponse(ex.Message);
        }
    }

    #endregion

    #region Business Profile & Metrics

    public async Task<string> SetupWebhook(string farmaciaId, string webhookUrl)
    {
        _logger.LogInformation("Setting up Instagram webhook for farmacia {FarmaciaId}: {WebhookUrl}", farmaciaId, webhookUrl);

        // Instagram webhooks are configured at the Meta App level via App Dashboard
        // or programmatically via the App Subscriptions API
        // POST /{app-id}/subscriptions
        // fields: messages, messaging_postbacks, messaging_optins, messaging_referrals
        return webhookUrl;
    }

    public async Task<Dictionary<string, object>> GetBusinessProfile(string farmaciaId)
    {
        try
        {
            var farmacia = await GetFarmaciaWithInstagramConfig(farmaciaId);
            if (farmacia == null)
                return new Dictionary<string, object> { ["error"] = "Farmácia não encontrada" };

            // GET /{ig-user-id}?fields=id,name,biography,profile_picture_url,followers_count,media_count
            var url = $"{VersionedApiBaseUrl}/{farmacia.InstagramAccountId}" +
                      "?fields=id,name,biography,profile_picture_url,followers_count,media_count" +
                      $"&access_token={farmacia.InstagramAccessToken}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(content)
                    ?? new Dictionary<string, object>();
            }

            _logger.LogWarning("Failed to get Instagram business profile: {Content}", content);
            return new Dictionary<string, object> { ["error"] = content };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Instagram business profile");
            return new Dictionary<string, object> { ["error"] = ex.Message };
        }
    }

    public async Task<MessageStatus> GetMessageStatus(string farmaciaId, string messageId)
    {
        // Instagram doesn't expose per-message delivery status like WhatsApp
        // We track it internally based on webhook delivery/read receipts
        var mensagem = await _dbContext.Mensagens
            .FirstOrDefaultAsync(m => m.MessageId == messageId);

        return mensagem?.Status ?? MessageStatus.Enviada;
    }

    #endregion

    #region Private Helpers

    private async Task<InstagramMessageResponse> SendInstagramRequest(string? pageId, string? accessToken, object payload, bool salvarLogCompleto)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return CreateErrorResponse("Instagram AccessToken not configured");
        }

        // Instagram Messaging API: POST /me/messages with Bearer token
        // Ref: https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/messaging-api
        var url = $"{VersionedApiBaseUrl}/me/messages";

        var serializedPayload = JsonSerializer.Serialize(payload);
        if (salvarLogCompleto)
        {
            _logger.LogInformation("📤 Instagram API request → URL: {Url} | PageId: {PageId} | Payload: {Payload}",
                url, pageId, serializedPayload);
        }
        else
        {
            _logger.LogDebug("Instagram API Request payload: {Payload}", serializedPayload);
        }

        var jsonContent = new StringContent(
            serializedPayload,
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = jsonContent;
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (salvarLogCompleto)
        {
            _logger.LogInformation("📥 Instagram API response → URL: {Url} | Status: {StatusCode} | Content: {Content}",
                url, response.StatusCode, responseContent);
        }
        else
        {
            _logger.LogInformation("Instagram API Response: {StatusCode} - {Content}",
                response.StatusCode, responseContent);
        }

        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var messageId = result.TryGetProperty("message_id", out var mid) ? mid.GetString() : null;

            return new InstagramMessageResponse
            {
                Success = true,
                MessageId = messageId,
                Timestamp = DateTime.UtcNow
            };
        }

        // Parse Meta error response
        string? errorCode = null;
        string? errorMessage = responseContent;

        try
        {
            var errorJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
            if (errorJson.TryGetProperty("error", out var error))
            {
                errorCode = error.TryGetProperty("code", out var code) ? code.GetInt32().ToString() : null;
                errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : responseContent;
            }
        }
        catch { /* Use raw response as error */ }

        return new InstagramMessageResponse
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<InstagramFarmaciaConfig?> GetFarmaciaWithInstagramConfig(string farmaciaId)
    {
        if (!int.TryParse(farmaciaId, out var id))
            return null;

        var farmacia = await _dbContext.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (farmacia == null) return null;

        return new InstagramFarmaciaConfig
        {
            Id = farmacia.Id,
            InstagramAccountId = farmacia.Integracoes?.InstagramAccountId ?? _config.InstagramAccountId,
            PageId = farmacia.Integracoes?.PageId ?? _config.PageId,
            InstagramAccessToken = farmacia.Integracoes?.InstagramAccessToken ?? _config.AccessToken,
            SalvarLogCompletoMidiaApiGo = farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false
        };
    }

    private static string NormalizeInstagramUsername(string username)
        => username.Trim().TrimStart('@');

    private record InstagramFarmaciaConfig
    {
        public int Id { get; init; }
        public string InstagramAccountId { get; init; } = "";
        public string PageId { get; init; } = "";
        public string InstagramAccessToken { get; init; } = "";
        public bool SalvarLogCompletoMidiaApiGo { get; init; }
    }

    private static InstagramMessageResponse CreateErrorResponse(string message, string? code = null)
    {
        return new InstagramMessageResponse
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
            Timestamp = DateTime.UtcNow
        };
    }

    #endregion
}
