using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<WhatsAppController> _logger;
    private readonly PharmazapDbContext _context;
    private readonly IMessageQueue _messageQueue;
    private readonly IConfiguration _configuration;
    private readonly ICrmService _crmService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEvolutionGoCryptoService _cryptoService;
    private readonly IRemetenteBloqueadoService _remetenteBloqueadoService;
    private readonly string _mediaUploadsDirectory;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _webhookLocks = new();

    public WhatsAppController(
        IWhatsAppService whatsAppService,
        ILogger<WhatsAppController> logger,
        PharmazapDbContext context,
        IMessageQueue messageQueue,
        IConfiguration configuration,
        ICrmService crmService,
        IHttpClientFactory httpClientFactory,
        IEvolutionGoCryptoService cryptoService,
        IRemetenteBloqueadoService remetenteBloqueadoService,
        IWebHostEnvironment? webHostEnvironment = null)
    {
        _whatsAppService = whatsAppService;
        _logger = logger;
        _context = context;
        _messageQueue = messageQueue;
        _configuration = configuration;
        _crmService = crmService;
        _httpClientFactory = httpClientFactory;
        _cryptoService = cryptoService;
        _remetenteBloqueadoService = remetenteBloqueadoService;
        var webRootPath = webHostEnvironment?.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        _mediaUploadsDirectory = Path.Combine(webRootPath, "uploads", "media");
    }

    // Webhook verification (GET)
    [HttpGet("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyWebhook(
        [FromQuery] string hub_mode,
        [FromQuery] string hub_challenge,
        [FromQuery] string hub_verify_token)
    {
        _logger.LogInformation("Webhook verification request: Mode={Mode}",
            hub_mode);

        var isValid = await _whatsAppService.ValidateWebhook(hub_mode, hub_verify_token, hub_challenge);

        if (isValid)
        {
            _logger.LogInformation("Webhook verification successful");
            return Ok(hub_challenge);
        }

        _logger.LogWarning("Webhook verification failed");
        return Unauthorized();
    }

    // Webhook message processing (POST)
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessWebhook([FromBody] Dictionary<string, object> payload)
    {
        try
        {
            _logger.LogInformation("Received webhook payload");

            if (IsEvolutionGoWebhook(payload))
            {
                var evolutionResult = await ProcessEvolutionGoWebhook(payload);
                if (evolutionResult.Success)
                    return Ok(new { success = true, message = evolutionResult.Message });

                return BadRequest(new { success = false, error = evolutionResult.Message });
            }

            var result = await _whatsAppService.ProcessWebhookPayload(payload);

            if (result.Success)
            {
                _logger.LogInformation("Webhook processed successfully: {Message}", result.Message);
                return Ok(new { success = true, message = result.Message });
            }
            else
            {
                _logger.LogWarning("Webhook processing failed: {Message}", result.Message);
                return BadRequest(new { success = false, error = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    private static bool IsEvolutionGoWebhook(Dictionary<string, object> payload)
    {
        return payload.ContainsKey("event") && payload.ContainsKey("instanceName");
    }

    private async Task<(bool Success, string Message)> ProcessEvolutionGoWebhook(Dictionary<string, object> payload)
    {
        var eventName = payload.TryGetValue("event", out var eventValue)
            ? eventValue?.ToString()
            : null;
        if (!string.Equals(eventName, "MESSAGES_UPSERT", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(eventName, "Message", StringComparison.OrdinalIgnoreCase))
            return (true, $"Evento Evolution Go ignorado: {eventName}");

        var instanceName = payload.TryGetValue("instanceName", out var instanceValue)
            ? instanceValue?.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(instanceName))
            return (false, "Payload Evolution Go sem instanceName");

        if (!payload.TryGetValue("data", out var dataValue))
            return (false, "Payload Evolution Go sem data");

        var data = ToJsonElement(dataValue);

        string? remoteJid;
        string? messageId;
        bool fromMe;

        if (TryGetPropertyIgnoreCase(data, "Info", out var info))
        {
            fromMe = TryGetPropertyIgnoreCase(info, "IsFromMe", out var fromMeProp) &&
                     fromMeProp.ValueKind == JsonValueKind.True;
            if (fromMe)
                return (true, "Mensagem outbound ignorada");

            var isGroupInfo = TryGetPropertyIgnoreCase(info, "IsGroup", out var isGroupProp) &&
                              isGroupProp.ValueKind == JsonValueKind.True;
            if (isGroupInfo)
            {
                remoteJid = TryGetPropertyIgnoreCase(info, "Chat", out var chat) ? chat.GetString() : null;
            }
            else
            {
                var sender = TryGetPropertyIgnoreCase(info, "Sender", out var senderProp) ? senderProp.GetString() : null;
                var chatFallback = TryGetPropertyIgnoreCase(info, "Chat", out var chatProp) ? chatProp.GetString() : null;
                remoteJid = string.IsNullOrWhiteSpace(sender) ? chatFallback : sender;
            }

            messageId = TryGetPropertyIgnoreCase(info, "ID", out var idProp)
                ? idProp.GetString()
                : null;
        }
        else if (TryGetPropertyIgnoreCase(data, "key", out var key))
        {
            fromMe = TryGetPropertyIgnoreCase(key, "fromMe", out var fromMeProp) &&
                     fromMeProp.ValueKind == JsonValueKind.True;
            if (fromMe)
                return (true, "Mensagem outbound ignorada");

            remoteJid = TryGetPropertyIgnoreCase(key, "remoteJid", out var remoteJidProp)
                ? remoteJidProp.GetString()
                : null;

            messageId = TryGetPropertyIgnoreCase(key, "id", out var idProp)
                ? idProp.GetString()
                : null;
        }
        else
        {
            return (false, "Payload Evolution Go sem Info ou key");
        }

        if (IsWhatsAppNewsletterJid(remoteJid))
        {
            _logger.LogInformation("Mensagem de canal/newsletter ignorada: {RemoteJid}", remoteJid);
            return (true, "Mensagem de canal/newsletter ignorada");
        }

        if (IsWhatsAppBroadcastJid(remoteJid))
        {
            _logger.LogInformation("Mensagem de broadcast ignorada: {RemoteJid}", remoteJid);
            return (true, "Mensagem de broadcast ignorada");
        }

        var isGroup = IsWhatsAppGroupJid(remoteJid);
        var whatsappId = isGroup ? SanitizeGroupJid(remoteJid) : SanitizePhone(remoteJid);
        if (string.IsNullOrWhiteSpace(whatsappId))
            return (false, "Número do remetente inválido");
        var contactName = isGroup ? null : ExtractEvolutionGoContactName(data);

        var comparableInstanceNames = GetComparableInstanceNames(instanceName);
        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f =>
                f.Integracoes != null &&
                comparableInstanceNames.Contains(f.Integracoes.ChaveWhatsApp));

        if (farmacia == null)
            return (false, $"Instância não vinculada a farmácia: {instanceName}");

        // Verificar se o remetente está bloqueado (lista negra)
        if (!isGroup)
        {
            var estaBloqueado = await _remetenteBloqueadoService.IsRemetentoBloqueadoAsync(whatsappId, farmacia.Id);
            if (estaBloqueado)
            {
                _logger.LogInformation("Mensagem de remetente bloqueado ignorada: {numero} (Farmácia: {farmaciaId})",
                    whatsappId, farmacia.Id);
                return (true, "Remetente bloqueado - mensagem ignorada");
            }
        }

        if (string.IsNullOrWhiteSpace(messageId))
            _logger.LogWarning("EvolutionGo webhook sem messageId para remoteJid={RemoteJid} farmacia={FarmaciaId} — deduplicação desabilitada para esta mensagem", whatsappId, farmacia.Id);

        SemaphoreSlim? semaphore = null;
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            semaphore = _webhookLocks.GetOrAdd(messageId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                var alreadyProcessed = await _context.Mensagens.AnyAsync(m => m.MessageId == messageId);
                if (alreadyProcessed)
                    return (true, "Mensagem já processada");
            }

            var cliente = isGroup
                ? null
                : await _crmService.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), whatsappId, contactName);
            if (!isGroup && cliente == null)
                return (false, "Não foi possível identificar o cliente da mensagem");

            var atendimentosPendentesQuery = _context.Atendimentos
                .Where(a =>
                    a.FarmaciaId == farmacia.Id &&
                    a.StatusEnum != AtendimentoStatusEnum.Finalizado);

            atendimentosPendentesQuery = isGroup
                ? atendimentosPendentesQuery.Where(a => a.WhatsAppId == whatsappId)
                : atendimentosPendentesQuery.Where(a => a.ClienteId == cliente!.Id || a.WhatsAppId == whatsappId);

            var atendimento = await atendimentosPendentesQuery
                .OrderByDescending(a => a.DataInicio)
                .FirstOrDefaultAsync();

            var novoAtendimento = false;
            if (atendimento == null)
            {
                atendimento = new Atendimento
                {
                    FarmaciaId = farmacia.Id,
                    Farmacia = null!,
                    ClienteId = cliente?.Id,
                    Cliente = null!,
                    Atendente = null!,
                    WhatsAppId = whatsappId,
                    CanalEnum = CanalEnum.WhatsApp,
                    StatusEnum = AtendimentoStatusEnum.Pendente,
                    DataInicio = DateTime.UtcNow
                };
                _context.Atendimentos.Add(atendimento);
                await _context.SaveChangesAsync();
                novoAtendimento = true;
            }
            else if (!isGroup && atendimento.ClienteId == null)
            {
                atendimento.ClienteId = cliente!.Id;
                await _context.SaveChangesAsync();
            }

            var messagePayload = TryGetPropertyIgnoreCase(data, "Message", out var msgProp)
                ? msgProp
                : default;
            var groupName = isGroup ? ExtractEvolutionGoGroupName(data) : null;
            var groupSenderName = isGroup ? ExtractEvolutionGoGroupSenderName(data) : null;
            // Save full data JSON (with key, message, messageType) for /message/downloadmedia fallback
            var rawMessageJson = data.ValueKind == JsonValueKind.Object
                ? data.GetRawText()
                : null;

            if (rawMessageJson != null)
            {
                _logger.LogDebug("EvolutionGo raw data payload for message {MessageId}: {Payload}",
                    messageId, rawMessageJson.Length > 500 ? rawMessageJson[..500] + "..." : rawMessageJson);
            }
            if (isGroup && farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo == true)
            {
                _logger.LogInformation(
                    "EvolutionGo group metadata extracted | messageId={MessageId} | groupJid={GroupJid} | groupName={GroupName} | senderName={SenderName}",
                    messageId,
                    whatsappId,
                    groupName ?? "(none)",
                    groupSenderName ?? "(none)");
            }
            var text = ExtractEvolutionGoMessageText(messagePayload);
            var messageType = ExtractEvolutionGoMessageType(messagePayload);
            if (TryGetPropertyIgnoreCase(data, "messageType", out var messageTypeProp) &&
                messageTypeProp.ValueKind == JsonValueKind.String)
            {
                var mappedMessageType = MapEvolutionGoMessageType(messageTypeProp.GetString());
                if (mappedMessageType != MessageType.Text || messageType == MessageType.Text)
                    messageType = mappedMessageType;
            }
            var (mediaType, mediaUrl, mediaCaption, mediaSize, mediaKey, encType, fileSha256, fileEncSha256, directPath) = ExtractEvolutionGoMedia(messagePayload, messageType);
            // Evolution Go media payloads can omit a separate text body; use the caption for chat display/search.
            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(mediaCaption))
                text = mediaCaption;

            // Skip saving messages that have no displayable content (e.g. reactions, polls,
            // status updates or other unhandled message types). Persisting them would create
            // empty bubbles in the chat and trigger spurious "Nova mensagem recebida" notifications.
            var messageTypeIsMedia = messageType is MessageType.Image or MessageType.Audio
                or MessageType.Video or MessageType.Document or MessageType.Sticker;
            if (string.IsNullOrWhiteSpace(text) &&
                string.IsNullOrWhiteSpace(mediaType) &&
                string.IsNullOrWhiteSpace(mediaUrl) &&
                !messageTypeIsMedia)
            {
                _logger.LogDebug(
                    "Skipping blank message {MessageId} (messageType={MessageType}) — no text and no media",
                    messageId, messageType);
                return (true, "Mensagem sem conteúdo ignorada");
            }

            string? localUrl = null;
            var inlineBase64Media = ExtractInlineBase64Media(messagePayload);
            if (!string.IsNullOrWhiteSpace(inlineBase64Media))
            {
                localUrl = await SaveBase64Media(inlineBase64Media, mediaType);
                if (!string.IsNullOrWhiteSpace(localUrl))
                {
                    _logger.LogInformation("Media stored locally from inline base64 payload: {LocalUrl}", localUrl);
                    mediaUrl = localUrl;
                }
            }

            // Try CDN download if URL is available
            var cdnUrl = mediaUrl;

            // If no direct CDN URL but directPath exists, construct the full URL
            if (string.IsNullOrWhiteSpace(localUrl) &&
                string.IsNullOrWhiteSpace(cdnUrl) &&
                !string.IsNullOrWhiteSpace(directPath))
            {
                var path = directPath.StartsWith("/") ? directPath : $"/{directPath}";
                cdnUrl = $"{Consts.WhatsAppCdn.MmgPrefix}{path}";
                _logger.LogDebug("Constructed CDN URL from directPath: {CdnUrl}", cdnUrl);

                if (IsAllowedMediaUrl(cdnUrl))
                {
                    try
                    {
                        localUrl = await DownloadAndStoreMediaLocally(cdnUrl, mediaType, farmacia.Integracoes?.WhatsAppApiKey, mediaKey);
                        if (!string.IsNullOrWhiteSpace(localUrl))
                        {
                            _logger.LogInformation("Media stored locally from directPath CDN URL: {LocalUrl}", localUrl);
                            mediaUrl = localUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download media from directPath CDN URL: {CdnUrl}", cdnUrl);
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(localUrl) && !string.IsNullOrWhiteSpace(cdnUrl))
            {
                try
                {
                    localUrl = await DownloadAndStoreMediaLocally(cdnUrl, mediaType, farmacia.Integracoes?.WhatsAppApiKey, mediaKey);
                    if (!string.IsNullOrWhiteSpace(localUrl))
                    {
                        _logger.LogInformation("Media stored locally: {LocalUrl} (original: {OriginalUrl})", localUrl, cdnUrl);
                        mediaUrl = localUrl;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download and store media locally from CDN: {CdnUrl}", cdnUrl);
                }
            }

            // Fallback via Evolution API (if local download failed or wasn't attempted)
            var hasMediaFields = !string.IsNullOrWhiteSpace(mediaKey) || !string.IsNullOrWhiteSpace(directPath);
            var isMediaMessage = messageType is MessageType.Image or MessageType.Audio or MessageType.Video or MessageType.Document or MessageType.Sticker;
            var hasEvolutionFallbackData = isMediaMessage &&
                                           (!string.IsNullOrWhiteSpace(mediaUrl) ||
                                            !string.IsNullOrWhiteSpace(rawMessageJson) ||
                                            hasMediaFields);
            if (string.IsNullOrWhiteSpace(localUrl) && hasEvolutionFallbackData &&
                !string.IsNullOrWhiteSpace(farmacia.Integracoes?.WhatsAppApiUrl))
            {
                _logger.LogInformation(
                    "Local media unavailable for message {MessageId}; trying Evolution API fallback (mediaType={MediaType}, hasMediaKey={HasMediaKey}, hasDirectPath={HasDirectPath})",
                    messageId, mediaType, !string.IsNullOrWhiteSpace(mediaKey), !string.IsNullOrWhiteSpace(directPath));
                try
                {
                    localUrl = await DownloadMediaViaEvolutionApi(
                        farmacia.Integracoes.WhatsAppApiUrl,
                        farmacia.Integracoes.WhatsAppApiKey,
                        messageId, mediaType, mediaKey, rawMessageJson, encType, fileSha256, fileEncSha256, directPath,
                        farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false);
                    if (!string.IsNullOrWhiteSpace(localUrl))
                    {
                        _logger.LogInformation("Media downloaded via Evolution API: {LocalUrl}", localUrl);
                        mediaUrl = localUrl;
                    }
                    else
                    {
                        _logger.LogWarning("Evolution API fallback did not return media for message {MessageId}", messageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download media via Evolution API for message {MessageId}", messageId);
                }
            }

            var receivedAt = DateTime.UtcNow;
            if (data.TryGetProperty("messageTimestamp", out var timestampProp) &&
                timestampProp.ValueKind == JsonValueKind.Number &&
                timestampProp.TryGetInt64(out var timestamp))
            {
                receivedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }

            var mensagem = new Mensagem
            {
                AtendimentoId = atendimento.Id,
                MessageId = string.IsNullOrWhiteSpace(messageId) ? $"pharmazap-evolution-{Guid.NewGuid():N}" : messageId,
                Direction = MessageDirection.Inbound,
                Type = messageType,
                Status = MessageStatus.Recebida,
                Text = text,
                MediaType = mediaType,
                MediaUrl = mediaUrl,
                MediaCaption = mediaCaption,
                MediaSize = mediaSize,
                ReceivedAt = receivedAt
            };

            var meta = mensagem.Metadata;
            var hasMetadata = false;
            if (isGroup)
            {
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    meta[Consts.EvolutionGoWebhook.GroupNameMetadataKey] = groupName;
                    hasMetadata = true;
                }

                if (!string.IsNullOrWhiteSpace(groupSenderName))
                {
                    meta[Consts.EvolutionGoWebhook.SenderNameMetadataKey] = groupSenderName;
                    hasMetadata = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(mediaKey) || !string.IsNullOrWhiteSpace(encType) || !string.IsNullOrWhiteSpace(fileSha256) || !string.IsNullOrWhiteSpace(fileEncSha256) || !string.IsNullOrWhiteSpace(directPath) || rawMessageJson != null)
            {
                if (!string.IsNullOrWhiteSpace(mediaKey)) meta["mediaKey"] = mediaKey;
                if (!string.IsNullOrWhiteSpace(encType)) meta["encType"] = encType;
                if (!string.IsNullOrWhiteSpace(fileSha256)) meta["fileSha256"] = fileSha256;
                if (!string.IsNullOrWhiteSpace(fileEncSha256)) meta["fileEncSha256"] = fileEncSha256;
                if (!string.IsNullOrWhiteSpace(directPath)) meta["directPath"] = directPath;
                if (rawMessageJson != null) meta["rawDataPayload"] = rawMessageJson;
                hasMetadata = true;
            }

            if (hasMetadata)
                mensagem.Metadata = meta;

            _context.Mensagens.Add(mensagem);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
                when (dbEx.InnerException is PostgresException { SqlState: "23505" })
            {
                _logger.LogWarning("Mensagem Evolution Go duplicada ignorada (unique constraint): MessageId={MessageId}", mensagem.MessageId);
                return (true, "Mensagem já processada (unique constraint)");
            }

            try
            {
                await _messageQueue.EnqueueAsync(Consts.Filas.NovasMensagens, new
                {
                    FarmaciaId = farmacia.Id.ToString(),
                    AtendimentoId = atendimento.Id,
                    MensagemId = mensagem.Id,
                    MessageType = messageType.ToString(),
                    IsNewAtendimento = novoAtendimento
                });
            }
            catch (Exception queueEx)
            {
                _logger.LogWarning(queueEx, "Falha ao enfileirar notificação de mensagem Evolution Go");
            }

            return (true, "Mensagem Evolution Go processada com sucesso");
        }
        finally
        {
            if (semaphore != null)
            {
                semaphore.Release();
                _webhookLocks.TryRemove(messageId!, out _);
            }
        }
    }

    private static List<string> GetComparableInstanceNames(string? instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return new List<string>();

        var normalized = instanceName.Trim();
        return new List<string> { normalized };
    }

    private static JsonElement ToJsonElement(object? value)
    {
        if (value is JsonElement jsonElement)
            return jsonElement;

        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string SanitizePhone(string? remoteJid)
    {
        if (string.IsNullOrWhiteSpace(remoteJid))
            return string.Empty;

        return NormalizeWhatsAppId(remoteJid.Split('@')[0]);
    }

    private static bool IsWhatsAppGroupJid(string? remoteJid)
    {
        return !string.IsNullOrWhiteSpace(remoteJid) &&
               remoteJid.Contains("@g.us", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWhatsAppBroadcastJid(string? remoteJid)
    {
        return !string.IsNullOrWhiteSpace(remoteJid) &&
               (remoteJid.Equals("status@broadcast", StringComparison.OrdinalIgnoreCase) ||
                remoteJid.EndsWith("@broadcast", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWhatsAppNewsletterJid(string? remoteJid)
    {
        if (string.IsNullOrWhiteSpace(remoteJid))
            return false;

        if (remoteJid.EndsWith("@newsletter", StringComparison.OrdinalIgnoreCase))
            return true;

        if (remoteJid.Contains('@'))
            return false;

        var digits = new string(remoteJid.Where(char.IsDigit).ToArray());

        // Evolution logs can expose newsletter/channel IDs as raw 120363... values without the suffix.
        return digits.Length > 15 && digits.StartsWith("120363", StringComparison.Ordinal);
    }

    private static string SanitizeGroupJid(string? remoteJid)
    {
        if (string.IsNullOrWhiteSpace(remoteJid))
            return string.Empty;

        var atIndex = remoteJid.IndexOf('@');
        var endIndex = atIndex >= 0 ? atIndex : remoteJid.Length;
        var digits = new char[endIndex];
        var digitCount = 0;
        for (var i = 0; i < endIndex; i++)
        {
            if (char.IsDigit(remoteJid[i]))
                digits[digitCount++] = remoteJid[i];
        }

        var groupId = digitCount == 0 ? string.Empty : new string(digits, 0, digitCount);
        return string.IsNullOrWhiteSpace(groupId) ? string.Empty : $"{groupId}@g.us";
    }

    private static string NormalizeWhatsAppId(string? input)
    {
        return input.NormalizeWhatsAppIdentifier();
    }

    private static string? ExtractEvolutionGoContactName(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        var directName = ExtractFirstString(data, Consts.EvolutionGoWebhook.ContactNameDirectFields);
        if (!string.IsNullOrWhiteSpace(directName))
            return directName.Trim();

        foreach (var containerName in Consts.EvolutionGoWebhook.ContactNameContainerFields)
        {
            if (!TryGetPropertyIgnoreCase(data, containerName, out var container) ||
                container.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nestedName = ExtractFirstString(container, Consts.EvolutionGoWebhook.ContactNameNestedFields);
            if (!string.IsNullOrWhiteSpace(nestedName))
                return nestedName.Trim();
        }

        return null;
    }

    private static string? ExtractEvolutionGoGroupName(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        var directName = ExtractFirstString(data, "groupName", "groupSubject", "subject", "groupTitle");
        if (!string.IsNullOrWhiteSpace(directName))
            return directName.Trim();

        foreach (var containerName in new[] { "Info", "groupMetadata", "groupInfo" })
        {
            if (!TryGetPropertyIgnoreCase(data, containerName, out var container) ||
                container.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nestedName = ExtractFirstString(container, "groupName", "groupSubject", "subject", "name", "groupTitle");
            if (!string.IsNullOrWhiteSpace(nestedName))
                return nestedName.Trim();
        }

        return null;
    }

    private static string? ExtractEvolutionGoGroupSenderName(JsonElement data)
    {
        var senderName = ExtractEvolutionGoContactName(data);
        if (!string.IsNullOrWhiteSpace(senderName))
            return senderName.Trim();

        string? senderJid = null;
        if (TryGetPropertyIgnoreCase(data, "Info", out var info) &&
            info.ValueKind == JsonValueKind.Object)
        {
            senderJid = ExtractFirstString(info, "Sender", "sender", "participant");
        }

        if (string.IsNullOrWhiteSpace(senderJid) &&
            TryGetPropertyIgnoreCase(data, "key", out var key) &&
            key.ValueKind == JsonValueKind.Object)
        {
            senderJid = ExtractFirstString(key, "participant", "sender");
        }

        if (string.IsNullOrWhiteSpace(senderJid))
            return null;

        var normalizedSender = SanitizePhone(senderJid);
        if (!string.IsNullOrWhiteSpace(normalizedSender))
            return normalizedSender.FormatTelefone();

        var atIndex = senderJid.IndexOf('@');
        return atIndex > 0 ? senderJid[..atIndex] : senderJid;
    }

    private static MessageType ExtractEvolutionGoMessageType(JsonElement message)
    {
        message = UnwrapEvolutionGoMessage(message);

        if (message.ValueKind != JsonValueKind.Object)
            return MessageType.Text;

        if (TryGetPropertyIgnoreCase(message, "imageMessage", out _)) return MessageType.Image;
        if (TryGetPropertyIgnoreCase(message, "videoMessage", out _)) return MessageType.Video;
        if (TryGetPropertyIgnoreCase(message, "audioMessage", out _)) return MessageType.Audio;
        if (TryGetPropertyIgnoreCase(message, "documentMessage", out _)) return MessageType.Document;
        if (TryGetPropertyIgnoreCase(message, "stickerMessage", out _)) return MessageType.Sticker;
        return MessageType.Text;
    }

    private static MessageType MapEvolutionGoMessageType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return MessageType.Text;

        return messageType.Trim().ToLowerInvariant() switch
        {
            "imagemessage" or "image" => MessageType.Image,
            "videomessage" or "video" => MessageType.Video,
            "audiomessage" or "audio" => MessageType.Audio,
            "documentmessage" or "document" => MessageType.Document,
            "stickermessage" or "sticker" => MessageType.Sticker,
            _ => MessageType.Text
        };
    }

    private static string ExtractEvolutionGoMessageText(JsonElement message)
    {
        message = UnwrapEvolutionGoMessage(message);

        if (message.ValueKind != JsonValueKind.Object)
            return string.Empty;

        if (TryGetPropertyIgnoreCase(message, "conversation", out var conversation) &&
            conversation.ValueKind == JsonValueKind.String)
            return conversation.GetString() ?? string.Empty;

        if (TryGetPropertyIgnoreCase(message, "extendedTextMessage", out var extendedTextMessage) &&
            TryGetPropertyIgnoreCase(extendedTextMessage, "text", out var extendedText) &&
            extendedText.ValueKind == JsonValueKind.String)
            return extendedText.GetString() ?? string.Empty;

        if (TryGetPropertyIgnoreCase(message, "imageMessage", out var imageMessage))
            return ExtractCaptionOrDefault(imageMessage, "[Imagem]");

        if (TryGetPropertyIgnoreCase(message, "videoMessage", out var videoMessage))
            return ExtractCaptionOrDefault(videoMessage, "[Vídeo]");

        if (TryGetPropertyIgnoreCase(message, "audioMessage", out _))
            return "[Áudio]";

        if (TryGetPropertyIgnoreCase(message, "documentMessage", out var documentMessage))
        {
            var docCaption = ExtractCaptionOrDefault(documentMessage, string.Empty);
            if (!string.IsNullOrWhiteSpace(docCaption))
                return docCaption;

            if (TryGetPropertyIgnoreCase(documentMessage, "fileName", out var fileName) &&
                fileName.ValueKind == JsonValueKind.String)
                return fileName.GetString() ?? "[Documento]";

            return "[Documento]";
        }

        if (TryGetPropertyIgnoreCase(message, "stickerMessage", out _))
            return "[Sticker]";

        return string.Empty;
    }

    private (string? MediaType, string? MediaUrl, string? MediaCaption, long? MediaSize, string? MediaKey, string? EncType, string? FileSha256, string? FileEncSha256, string? DirectPath) ExtractEvolutionGoMedia(JsonElement message, MessageType messageType)
    {
        message = UnwrapEvolutionGoMessage(message);
        if (message.ValueKind != JsonValueKind.Object)
            return (null, null, null, null, null, null, null, null, null);

        JsonElement mediaPayload;
        string mediaType;

        switch (messageType)
        {
            case MessageType.Image:
                if (!TryGetMediaPayload(message, "imageMessage", out mediaPayload))
                    return (null, null, null, null, null, null, null, null, null);
                mediaType = "image";
                break;
            case MessageType.Audio:
                if (!TryGetMediaPayload(message, "audioMessage", out mediaPayload))
                    return (null, null, null, null, null, null, null, null, null);
                mediaType = "audio";
                break;
            case MessageType.Video:
                if (!TryGetMediaPayload(message, "videoMessage", out mediaPayload))
                    return (null, null, null, null, null, null, null, null, null);
                mediaType = "video";
                break;
            case MessageType.Document:
                if (!TryGetMediaPayload(message, "documentMessage", out mediaPayload))
                    return (null, null, null, null, null, null, null, null, null);
                mediaType = "document";
                break;
            case MessageType.Sticker:
                if (!TryGetMediaPayload(message, "stickerMessage", out mediaPayload))
                    return (null, null, null, null, null, null, null, null, null);
                mediaType = "sticker";
                break;
            default:
                return (null, null, null, null, null, null, null, null, null);
        }

        if (mediaPayload.ValueKind != JsonValueKind.Object)
            return (mediaType, null, null, null, null, null, null, null, null);

        var mediaUrl = ExtractFirstString(mediaPayload, "downloadUrl", "mediaUrl", "url");
        var directPath = ExtractFirstString(mediaPayload, "directPath");
        var mediaCaption = ExtractFirstString(mediaPayload, "caption", "fileName");
        var mediaMimeType = ExtractFirstString(mediaPayload, "mimetype", "contentType");
        var mediaSize = ExtractFirstLong(mediaPayload, "fileLength", "size");
        var mediaKey = ExtractFirstString(mediaPayload, "mediaKey", "MediaKey");
        var encType = ExtractFirstString(mediaPayload, "encType");
        var fileSha256 = ExtractFirstString(mediaPayload, "fileSha256", "file_sha256");
        var fileEncSha256 = ExtractFirstString(mediaPayload, "fileEncSha256", "file_enc_sha256", "fileEncSha256");

        _logger.LogDebug("EvolutionGo media payload for type={MediaType}: encType={EncType}, hasMediaKey={HasKey}, hasFileSha256={HasSha256}, hasFileEncSha256={HasEncSha256}, hasDownloadUrl={HasUrl}, hasDirectPath={HasDirectPath}",
            mediaType, encType ?? "(null)", mediaKey != null, fileSha256 != null, fileEncSha256 != null, mediaUrl != null, directPath != null);

        return (mediaMimeType ?? mediaType, mediaUrl, mediaCaption, mediaSize, mediaKey, encType, fileSha256, fileEncSha256, directPath);
    }

    private static string? ExtractInlineBase64Media(JsonElement message)
    {
        message = UnwrapEvolutionGoMessage(message);
        if (message.ValueKind != JsonValueKind.Object)
            return null;

        if (TryExtractBase64Value(message, out var base64Value))
            return base64Value;

        var wrappers = new[]
        {
            "imageMessage",
            "videoMessage",
            "audioMessage",
            "documentMessage",
            "stickerMessage"
        };

        foreach (var wrapper in wrappers)
        {
            if (TryGetPropertyIgnoreCase(message, wrapper, out var wrapperPayload) &&
                wrapperPayload.ValueKind == JsonValueKind.Object &&
                TryExtractBase64Value(wrapperPayload, out base64Value))
            {
                return base64Value;
            }
        }

        return null;
    }

    private static bool TryExtractBase64Value(JsonElement payload, out string? base64Value)
    {
        base64Value = null;
        if (payload.ValueKind != JsonValueKind.Object)
            return false;

        if (TryGetPropertyIgnoreCase(payload, "data", out var dataPayload) &&
            dataPayload.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(dataPayload, "base64", out var nestedBase64) &&
            nestedBase64.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(nestedBase64.GetString()))
        {
            base64Value = StripDataUrlPrefix(nestedBase64.GetString()!);
            return true;
        }

        foreach (var propertyName in new[] { "base64", "media", "image", "video", "audio", "document" })
        {
            if (TryGetPropertyIgnoreCase(payload, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                base64Value = StripDataUrlPrefix(property.GetString()!);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMediaPayload(JsonElement message, string wrapperName, out JsonElement mediaPayload)
    {
        if (TryGetPropertyIgnoreCase(message, wrapperName, out mediaPayload))
            return true;

        if (LooksLikeMediaPayload(message))
        {
            mediaPayload = message;
            return true;
        }

        mediaPayload = default;
        return false;
    }

    private static bool LooksLikeMediaPayload(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return false;

        // Some Evolution Go payloads put media fields directly in Message instead of imageMessage/videoMessage wrappers.
        return TryGetPropertyIgnoreCase(message, "url", out _) ||
               TryGetPropertyIgnoreCase(message, "downloadUrl", out _) ||
               TryGetPropertyIgnoreCase(message, "mediaUrl", out _) ||
               TryGetPropertyIgnoreCase(message, "directPath", out _) ||
               TryGetPropertyIgnoreCase(message, "base64", out _) ||
               TryGetPropertyIgnoreCase(message, "media", out _) ||
               TryGetPropertyIgnoreCase(message, "image", out _) ||
               TryGetPropertyIgnoreCase(message, "video", out _) ||
               TryGetPropertyIgnoreCase(message, "audio", out _) ||
               TryGetPropertyIgnoreCase(message, "document", out _) ||
               (TryGetPropertyIgnoreCase(message, "data", out var dataPayload) &&
                dataPayload.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(dataPayload, "base64", out _)) ||
               TryGetPropertyIgnoreCase(message, "caption", out _) ||
               TryGetPropertyIgnoreCase(message, "fileName", out _) ||
               TryGetPropertyIgnoreCase(message, "mimetype", out _) ||
               TryGetPropertyIgnoreCase(message, "fileLength", out _) ||
               TryGetPropertyIgnoreCase(message, "size", out _);
    }

    private static JsonElement UnwrapEvolutionGoMessage(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return message;

        if (ContainsEvolutionGoMessageContent(message))
            return message;

        if (TryGetPropertyIgnoreCase(message, "message", out var directMessage) &&
            directMessage.ValueKind == JsonValueKind.Object)
        {
            return UnwrapEvolutionGoMessage(directMessage);
        }

        var wrappers = new[]
        {
            "ephemeralMessage",
            "viewOnceMessage",
            "viewOnceMessageV2",
            "viewOnceMessageV2Extension",
            "editedMessage"
        };

        foreach (var wrapper in wrappers)
        {
            if (!TryGetPropertyIgnoreCase(message, wrapper, out var wrapperPayload) ||
                wrapperPayload.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var unwrapped = UnwrapEvolutionGoMessage(wrapperPayload);
            if (ContainsEvolutionGoMessageContent(unwrapped))
                return unwrapped;
        }

        return message;
    }

    private static bool ContainsEvolutionGoMessageContent(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return false;

        return TryGetPropertyIgnoreCase(message, "conversation", out _) ||
               TryGetPropertyIgnoreCase(message, "extendedTextMessage", out _) ||
               TryGetPropertyIgnoreCase(message, "imageMessage", out _) ||
               TryGetPropertyIgnoreCase(message, "videoMessage", out _) ||
               TryGetPropertyIgnoreCase(message, "audioMessage", out _) ||
               TryGetPropertyIgnoreCase(message, "documentMessage", out _) ||
               TryGetPropertyIgnoreCase(message, "stickerMessage", out _);
    }

    private static string? ExtractFirstString(JsonElement payload, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetPropertyIgnoreCase(payload, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static long? ExtractFirstLong(JsonElement payload, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(payload, propertyName, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numberValue))
                return numberValue;

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), out var stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static string ExtractCaptionOrDefault(JsonElement payload, string defaultText)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(payload, "caption", out var caption) &&
            caption.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(caption.GetString()))
        {
            return caption.GetString()!;
        }

        return defaultText;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out value))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    // Enviar mensagem de texto
    [HttpPost("send/text")]
    [Authorize(Roles = "Administrador,Atendente")]
    public async Task<IActionResult> SendTextMessage([FromBody] SendTextRequest request)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
            {
                return BadRequest(new { success = false, error = "Farmácia não identificada" });
            }

            var result = await _whatsAppService.SendTextMessage(farmaciaId, request.To, request.Text);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    messageId = result.MessageId,
                    timestamp = result.Timestamp
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage,
                    code = result.ErrorCode
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text message");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    // Enviar mensagem com mídia
    [HttpPost("send/media")]
    [Authorize(Roles = "Administrador,Atendente")]
    public async Task<IActionResult> SendMediaMessage([FromForm] SendMediaRequest request)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
            {
                return BadRequest(new { success = false, error = "Farmácia não identificada" });
            }

            // Upload do arquivo para blob storage
            var mediaUrl = await UploadMediaToStorage(request.MediaFile);

            var result = await _whatsAppService.SendMediaMessage(
                farmaciaId, request.To, mediaUrl, request.MediaType, request.Caption);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    messageId = result.MessageId,
                    mediaUrl = mediaUrl,
                    timestamp = result.Timestamp
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage,
                    code = result.ErrorCode
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending media message");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    // Enviar template
    [HttpPost("send/template")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SendTemplateMessage([FromBody] SendTemplateRequest request)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
            {
                return BadRequest(new { success = false, error = "Farmácia não identificada" });
            }

            var result = await _whatsAppService.SendTemplateMessage(
                farmaciaId, request.To, request.TemplateName, request.LanguageCode, request.Parameters);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    messageId = result.MessageId,
                    timestamp = result.Timestamp
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage,
                    code = result.ErrorCode
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending template message");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    // Configurar webhook para uma farmácia
    [HttpPost("setup-webhook/{farmaciaId}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SetupWebhook(int farmaciaId, [FromBody] SetupWebhookRequest request)
    {
        try
        {
            var result = await _whatsAppService.SetupWebhook(farmaciaId.ToString(), request.WebhookUrl);

            return Ok(new
            {
                success = true,
                webhookUrl = result,
                message = "Webhook configurado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up webhook for farmacia {FarmaciaId}", farmaciaId);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    private async Task<string> UploadMediaToStorage(IFormFile file)
    {
        // Implementar upload para Azure Blob Storage
        // Por enquanto, retornar URL mock
        return $"https://pharmazapplus.blob.core.windows.net/media/{Guid.NewGuid()}/{file.FileName}";
    }

    private const long MaxMediaSizeBytes = 25 * 1024 * 1024; // 25 MB

    /// <summary>
    /// Returns true only for HTTPS URLs on known WhatsApp CDN hosts,
    /// preventing SSRF attacks by blocking arbitrary or internal URLs.
    /// </summary>
    internal static bool IsAllowedMediaUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        var host = uri.Host.ToLowerInvariant();
        return host == Consts.WhatsAppCdn.WhatsAppNetDomain
            || host.EndsWith("." + Consts.WhatsAppCdn.WhatsAppNetDomain, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fallback: baixa mídia descriptografada via Evolution Go API.
    /// Tenta primeiro /message/downloadmedia (aceita o payload completo da mensagem),
    /// depois /message/downloadimage (com fields individuais) como fallback.
    /// </summary>
    private async Task<string?> DownloadMediaViaEvolutionApi(
        string apiUrl, string? apiKey, string? messageId, string? mediaType,
        string? mediaKey = null, string? rawPayloadJson = null,
        string? encType = null, string? fileSha256 = null, string? fileEncSha256 = null,
        string? directPath = null, bool salvarLogCompleto = false)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            _logger.LogDebug("Evolution API URL not available, skipping Evolution download");
            return null;
        }

        var baseUrl = apiUrl.TrimEnd('/');

        var downloadMessageJson = TryBuildDownloadMessageJson(rawPayloadJson, out var parsedMessageJson)
            ? parsedMessageJson
            : null;

        // 1. Try /message/downloadmedia with the message payload (simpler, less error-prone)
        if (!string.IsNullOrWhiteSpace(downloadMessageJson))
        {
            try
            {
                // Build JSON manually to avoid double-serializing the parsed message JSON.
                var downloadMediaJson = $"{{\"message\":{downloadMessageJson}}}";
                LogMediaApiGoRequestIfEnabled(salvarLogCompleto, "/message/downloadmedia", messageId, downloadMediaJson);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/message/downloadmedia");
                AddEvolutionApiKeyHeaderIfPresent(request, apiKey);
                request.Content = new StringContent(downloadMediaJson, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var response = await client.SendAsync(request);

                var responseBody = await response.Content.ReadAsStringAsync();
                LogMediaApiGoResponseIfEnabled(salvarLogCompleto, "/message/downloadmedia", messageId, response, responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var doc = JsonDocument.Parse(responseBody);
                    var base64Data = ExtractMediaFromResponse(doc, responseBody);
                    if (!string.IsNullOrWhiteSpace(base64Data))
                    {
                        return await SaveBase64Media(base64Data, mediaType);
                    }
                }
                else
                {
                    _logger.LogDebug("Evolution API /message/downloadmedia returned {Status} for message {MessageId}: {Body}",
                        response.StatusCode, messageId,
                        responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download media via /message/downloadmedia for message {MessageId}", messageId);
            }
        }

        // 2. Fallback: tenta /message/downloadimage (algumas versões da evolution-go suportam)
        try
        {
            var json = BuildDownloadImageRequestJson(
                downloadMessageJson, mediaType, mediaKey, encType, fileSha256, fileEncSha256, directPath);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            LogMediaApiGoRequestIfEnabled(salvarLogCompleto, "/message/downloadimage", messageId, json);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            foreach (var endpoint in GetDownloadImageEndpoints(baseUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                AddEvolutionApiKeyHeaderIfPresent(request, apiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                LogMediaApiGoResponseIfEnabled(salvarLogCompleto, endpoint.Replace(baseUrl, string.Empty), messageId, response, responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var doc = JsonDocument.Parse(responseBody);
                    var base64Data = ExtractMediaFromResponse(doc, responseBody);
                    if (!string.IsNullOrWhiteSpace(base64Data))
                    {
                        return await SaveBase64Media(base64Data, mediaType);
                    }
                }
                else
                {
                    _logger.LogDebug("Evolution API {Endpoint} returned {Status} for message {MessageId}: {Body}",
                        endpoint, response.StatusCode, messageId,
                        responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download media via Evolution API for message {MessageId}", messageId);
            return null;
        }
    }

    /// <summary>
    /// Extrai o payload base64 da resposta da Evolution API.
    /// A evolution-go v0.7+ retorna:
    /// { "message":"success", "data":{ "base64":"data:image/jpeg;base64,iVBOR...", "timestamp":"..." } }
    /// Também suporta formatos legados:
    /// { "media":"base64..." }, { "image":"base64..." }, { "base64":"base64..." }
    /// ou o body inteiro sendo uma string base64 pura.
    /// </summary>
    private static string? ExtractMediaFromResponse(JsonDocument doc, string responseBody)
    {
        // 1. Formato evolution-go v0.7+ (data.base64 = data URL)
        if (doc.RootElement.TryGetProperty("data", out var dataProp) &&
            dataProp.ValueKind == JsonValueKind.Object)
        {
            if (dataProp.TryGetProperty("base64", out var dataBase64) &&
                dataBase64.ValueKind == JsonValueKind.String)
            {
                var rawBase64 = dataBase64.GetString();
                if (!string.IsNullOrWhiteSpace(rawBase64))
                    return StripDataUrlPrefix(rawBase64);
            }
        }

        // 2. Campo "base64" na raiz
        if (doc.RootElement.TryGetProperty("base64", out var base64Prop) &&
            base64Prop.ValueKind == JsonValueKind.String)
        {
            var rawBase64 = base64Prop.GetString();
            if (!string.IsNullOrWhiteSpace(rawBase64))
                return StripDataUrlPrefix(rawBase64);
        }

        // 3. Campos legados: media, image, video, audio, document
        if (doc.RootElement.TryGetProperty("media", out var mediaProp) &&
            mediaProp.ValueKind == JsonValueKind.String)
            return mediaProp.GetString();

        if (doc.RootElement.TryGetProperty("image", out var imageProp) &&
            imageProp.ValueKind == JsonValueKind.String)
            return imageProp.GetString();

        if (doc.RootElement.TryGetProperty("video", out var videoProp) &&
            videoProp.ValueKind == JsonValueKind.String)
            return videoProp.GetString();

        if (doc.RootElement.TryGetProperty("audio", out var audioProp) &&
            audioProp.ValueKind == JsonValueKind.String)
            return audioProp.GetString();

        if (doc.RootElement.TryGetProperty("document", out var docProp) &&
            docProp.ValueKind == JsonValueKind.String)
            return docProp.GetString();

        // 4. Fallback: tenta interpretar o body inteiro como base64 puro
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            var trimmed = responseBody.Trim().Trim('"');
            if (IsLikelyBase64(trimmed))
                return StripDataUrlPrefix(trimmed);
        }

        return null;
    }

    /// <summary>
    /// Se o valor for uma data URL (data:mime;base64,...), extrai apenas a parte base64.
    /// </summary>
    private static string StripDataUrlPrefix(string value)
    {
        const string dataPrefix = "data:";
        if (value.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var base64Marker = ";base64,";
            var idx = value.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return value[(idx + base64Marker.Length)..];
        }

        return value;
    }

    /// <summary>
    /// Verifica superficialmente se a string parece ser base64 (apenas caracteres válidos).
    /// </summary>
    private static bool IsLikelyBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        // Se começa com data: é data URL — deixa passar (StripDataUrlPrefix resolve)
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return true;
        // Base64 válido tem comprimento múltiplo de 4 (ou com padding =)
        // e contém apenas [A-Za-z0-9+/=]
        if (value.Length % 4 != 0 && !value.EndsWith("="))
            return false;
        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
                return false;
        }
        return true;
    }

    private void LogMediaApiGoRequestIfEnabled(bool enabled, string endpoint, string? messageId, string? jsonPayload)
    {
        if (!enabled)
            return;

        _logger.LogInformation(
            "Media API GO request | endpoint={Endpoint} | messageId={MessageId} | json={JsonPayload}",
            endpoint,
            messageId,
            jsonPayload ?? string.Empty);
    }

    private void LogMediaApiGoResponseIfEnabled(
        bool enabled,
        string endpoint,
        string? messageId,
        HttpResponseMessage response,
        string? responseBody)
    {
        if (!enabled)
            return;

        _logger.LogInformation(
            "Media API GO response | endpoint={Endpoint} | messageId={MessageId} | status={StatusCode} | contentType={ContentType} | response={ResponseBody}",
            endpoint,
            messageId,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString() ?? "N/A",
            responseBody ?? string.Empty);
    }

    private static void AddEvolutionApiKeyHeaderIfPresent(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("apikey", apiKey);
    }

    internal static bool TryBuildDownloadMessageJson(string? rawPayloadJson, out string messageJson)
    {
        messageJson = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPayloadJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(rawPayloadJson);
            var root = doc.RootElement;
            if (TryGetPropertyIgnoreCase(root, "message", out var message) &&
                message.ValueKind == JsonValueKind.Object)
            {
                messageJson = message.GetRawText();
                return true;
            }

            if (ContainsEvolutionGoMessageContent(root) || LooksLikeMediaPayload(root))
            {
                messageJson = root.GetRawText();
                return true;
            }
        }
        catch
        {
            // Not parseable, leave fields as-is.
        }

        return false;
    }

    internal static string? BuildDownloadImageRequestJson(
        string? messageJson,
        string? mediaType,
        string? mediaKey,
        string? encType,
        string? fileSha256,
        string? fileEncSha256,
        string? directPath)
    {
        if (!string.IsNullOrWhiteSpace(messageJson))
            return $"{{\"message\":{messageJson}}}";

        var wrapperName = ResolveDownloadImageWrapperName(mediaType);
        if (wrapperName is null)
            return null;

        var mediaPayload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(directPath))
            mediaPayload["directPath"] = directPath;
        if (!string.IsNullOrWhiteSpace(mediaKey))
            mediaPayload["mediaKey"] = ConvertBase64ToIntArrayOrString(mediaKey);
        if (!string.IsNullOrWhiteSpace(fileSha256))
            mediaPayload["fileSHA256"] = ConvertBase64ToIntArrayOrString(fileSha256);
        if (!string.IsNullOrWhiteSpace(fileEncSha256))
            mediaPayload["fileEncSHA256"] = ConvertBase64ToIntArrayOrString(fileEncSha256);

        var mimetype = ResolveDownloadImageMimeType(mediaType, encType);
        if (!string.IsNullOrWhiteSpace(mimetype))
            mediaPayload["mimetype"] = mimetype;

        if (mediaPayload.Count == 0)
            return null;

        var payload = new Dictionary<string, object?>
        {
            ["message"] = new Dictionary<string, object?>
            {
                [wrapperName] = mediaPayload
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string? ResolveDownloadImageWrapperName(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return "imageMessage";

        var normalized = mediaType.Trim().ToLowerInvariant();
        return normalized switch
        {
            _ when normalized.Contains("audio") => "audioMessage",
            _ when normalized.Contains("video") => "videoMessage",
            _ when IsDocumentMediaType(normalized) => "documentMessage",
            _ when normalized.Contains("sticker") || normalized.Contains("webp") => "stickerMessage",
            _ when normalized.Contains("image") => "imageMessage",
            _ => "imageMessage"
        };
    }

    private static string? ResolveDownloadImageMimeType(string? mediaType, string? encType)
    {
        var normalizedMediaType = mediaType?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedMediaType) && normalizedMediaType.Contains('/'))
            return normalizedMediaType;

        var normalizedEncType = encType?.Trim();
        return !string.IsNullOrWhiteSpace(normalizedEncType) && normalizedEncType.Contains('/')
            ? normalizedEncType
            : null;
    }

    private static bool IsDocumentMediaType(string normalizedMediaType)
    {
        return normalizedMediaType.Contains("document") ||
               normalizedMediaType.Contains("pdf") ||
               normalizedMediaType.Contains("application/") ||
               normalizedMediaType.Contains("text/");
    }

    private static object ConvertBase64ToIntArrayOrString(string value)
    {
        try
        {
            return Array.ConvertAll(Convert.FromBase64String(value), b => (int)b);
        }
        catch
        {
            return value;
        }
    }

    private static IEnumerable<string> GetDownloadImageEndpoints(string baseUrl)
    {
        yield return $"{baseUrl}/message/downloadimage";
        yield return $"{baseUrl}/message-downloadimage";
    }

    private static void TryConvertToByteArray(Dictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is not string strValue)
            return;

        if (string.IsNullOrWhiteSpace(strValue))
            return;

        try
        {
            var bytes = Convert.FromBase64String(strValue);
            payload[key] = bytes.Select(b => (int)b).ToArray();
        }
        catch
        {
            // Not a valid base64 string, leave as-is
        }
    }

    /// <summary>
    /// Backward compat: extrai campos de mídia individuais de um rawMessagePayload antigo
    /// (formato: {"imageMessage": {"mediaKey": "...", "directPath": "...", ...}})
    /// quando os metadados individuais não foram salvos.
    /// </summary>
    private static void TryExtractMediaFieldsFromPayload(
        string rawPayload, ref string? mediaKey, ref string? directPath,
        ref string? fileSha256, ref string? fileEncSha256, ref string? encType)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            // Old format wraps media in imageMessage/videoMessage/audioMessage/documentMessage/stickerMessage
            var mediaWrappers = new[] { "imageMessage", "videoMessage", "audioMessage", "documentMessage", "stickerMessage" };
            foreach (var wrapper in mediaWrappers)
            {
                if (!root.TryGetProperty(wrapper, out var mediaPayload) ||
                    mediaPayload.ValueKind != JsonValueKind.Object)
                    continue;

                if (string.IsNullOrWhiteSpace(mediaKey))
                    mediaKey = ExtractFirstString(mediaPayload, "mediaKey", "MediaKey");
                if (string.IsNullOrWhiteSpace(directPath))
                    directPath = ExtractFirstString(mediaPayload, "directPath");
                if (string.IsNullOrWhiteSpace(fileSha256))
                    fileSha256 = ExtractFirstString(mediaPayload, "fileSha256", "file_sha256");
                if (string.IsNullOrWhiteSpace(fileEncSha256))
                    fileEncSha256 = ExtractFirstString(mediaPayload, "fileEncSha256", "file_enc_sha256", "fileEncSha256");
                if (string.IsNullOrWhiteSpace(encType))
                    encType = ExtractFirstString(mediaPayload, "encType");
                break;
            }
        }
        catch
        {
            // Not parseable, leave fields as-is
        }
    }

    private async Task<string?> SaveBase64Media(string base64Data, string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(base64Data))
            return null;

        try
        {
            // Remove prefixo data URL (data:mime;base64,...) se presente
            var cleanBase64 = StripDataUrlPrefix(base64Data);
            var mediaData = Convert.FromBase64String(cleanBase64);
            if (!IsMediaContentSane(mediaData, mediaType, null))
            {
                _logger.LogWarning("Ignoring base64 media with incompatible signature (mediaType={MediaType}, size={Size})",
                    mediaType, mediaData.Length);
                return null;
            }

            var extension = ResolveExtension(mediaType, null);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsDir = _mediaUploadsDirectory;
            Directory.CreateDirectory(uploadsDir);
            var filePath = Path.Combine(uploadsDir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, mediaData);
            _logger.LogInformation("Media saved: {FileName} (type={MediaType})", fileName, mediaType);
            return $"{Consts.Midia.UploadUrlPrefix}{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save base64 media data");
            return null;
        }
    }

    private async Task<string?> DownloadAndStoreMediaLocally(string mediaUrl, string? mediaType, string? evolutionApiKey = null, string? mediaKey = null, Guid? mensagemId = null)
    {
        if (!IsAllowedMediaUrl(mediaUrl))
        {
            _logger.LogDebug("Media URL not in allowlist, skipping download: {MediaUrl}", mediaUrl);
            return null;
        }

        // Tentar buscar mediaKey do MetadataJson do banco se não foi fornecida (ex: correção posterior)
        if (string.IsNullOrWhiteSpace(mediaKey) && mensagemId.HasValue)
        {
            var msg = await _context.Mensagens.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mensagemId.Value);
            if (msg?.MetadataJson != null)
            {
                try
                {
                    var meta = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(msg.MetadataJson);
                    if (meta != null && meta.TryGetValue("mediaKey", out var mk) && mk is string mkStr && !string.IsNullOrWhiteSpace(mkStr))
                    {
                        mediaKey = mkStr;
                        _logger.LogInformation("MediaKey retrieved from MetadataJson for mensagem {MensagemId}", mensagemId.Value);
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PharmazapPlus/1.0");
        client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrWhiteSpace(evolutionApiKey))
        {
            client.DefaultRequestHeaders.Remove("apikey");
            client.DefaultRequestHeaders.Add("apikey", evolutionApiKey);
        }

        using var response = await client.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxMediaSizeBytes)
        {
            _logger.LogWarning("Media file too large ({Size} bytes), skipping: {MediaUrl}", contentLength.Value, mediaUrl);
            return null;
        }

        var encryptedData = await response.Content.ReadAsByteArrayAsync();
        if (encryptedData.Length == 0)
        {
            _logger.LogWarning("Empty media content from {MediaUrl}", mediaUrl);
            return null;
        }

        var mediaData = encryptedData;

        if (!string.IsNullOrWhiteSpace(mediaKey))
        {
            var decrypted = _cryptoService.DecryptMedia(encryptedData, mediaKey, mediaType ?? "image");
            if (decrypted is not null)
            {
                mediaData = decrypted;
                _logger.LogInformation("Media decrypted successfully using mediaKey for {MediaUrl}", mediaUrl);
            }
            else
            {
                _logger.LogWarning("Media decryption failed for {MediaUrl}. " +
                    "mediaKey length={KeyLen}, mediaType={MediaType}, " +
                    "encrypted size={EncSize}, first bytes={FirstBytes}",
                    mediaUrl,
                    mediaKey?.Length,
                    mediaType,
                    encryptedData?.Length,
                    encryptedData?.Length > 4 ? Convert.ToHexString(encryptedData[..4]) : "N/A");
                _logger.LogInformation("Skipping local media persistence for {MediaUrl} to force Evolution API fallback after decryption failure", mediaUrl);
                return null;
            }
        }

        if (!IsMediaContentSane(mediaData, mediaType, response.Content.Headers.ContentType?.MediaType))
        {
            _logger.LogWarning(
                "Ignoring media with incompatible signature for {MediaUrl} (mediaType={MediaType}, responseType={ResponseType}, size={Size})",
                mediaUrl,
                mediaType,
                response.Content.Headers.ContentType?.MediaType,
                mediaData.Length);
            return null;
        }

        var extension = ResolveExtension(mediaType, response.Content.Headers.ContentType?.MediaType);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsDir = _mediaUploadsDirectory;
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        var resolvedPath = Path.GetFullPath(filePath);
        var resolvedDir = Path.GetFullPath(uploadsDir);
        if (!resolvedPath.StartsWith(resolvedDir + Path.DirectorySeparatorChar))
        {
            _logger.LogWarning("Path traversal attempt blocked: {FilePath}", filePath);
            return null;
        }

        await System.IO.File.WriteAllBytesAsync(filePath, mediaData);

        return $"{Consts.Midia.UploadUrlPrefix}{fileName}";
    }

    private static string ResolveExtension(string? mediaType, string? mimeFromResponse)
    {
        // Prefer the response Content-Type when it's a real MIME, but fall back to the
        // webhook-supplied mediaType when the CDN returns the generic
        // "application/octet-stream" (which would otherwise produce a useless ".bin" file).
        var responseMime = mimeFromResponse?.Trim().ToLowerInvariant();
        var webhookMime = mediaType?.Trim().ToLowerInvariant();
        var mime = !string.IsNullOrEmpty(responseMime) && responseMime != "application/octet-stream"
            ? responseMime
            : (webhookMime ?? responseMime ?? string.Empty);

        // Strip parameters such as "; codecs=opus" so the switch matches.
        var semicolon = mime.IndexOf(';');
        if (semicolon > 0)
            mime = mime[..semicolon].Trim();

        return mime switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "audio/ogg" => ".ogg",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/mp4" or "audio/aac" or "audio/x-m4a" or "audio/m4a" => ".m4a",
            "audio/wav" or "audio/x-wav" => ".wav",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "text/plain" => ".txt",
            _ when mediaType?.Contains("image", StringComparison.OrdinalIgnoreCase) == true => ".jpg",
            _ when mediaType?.Contains("audio", StringComparison.OrdinalIgnoreCase) == true => ".ogg",
            _ when mediaType?.Contains("video", StringComparison.OrdinalIgnoreCase) == true => ".mp4",
            _ => ".bin"
        };
    }

    private static bool IsMediaContentSane(byte[] data, string? mediaType, string? mimeFromResponse)
    {
        if (data.Length == 0)
            return false;

        var responseMime = NormalizeMimeType(mimeFromResponse);
        var webhookMime = NormalizeMimeType(mediaType);
        var expectedMime = !string.IsNullOrWhiteSpace(responseMime) && responseMime != "application/octet-stream"
            ? responseMime
            : webhookMime;

        if (string.IsNullOrWhiteSpace(expectedMime))
            return true;

        if (expectedMime.StartsWith("image/", StringComparison.Ordinal))
            return HasKnownImageSignature(data);

        return expectedMime switch
        {
            "application/pdf" => data.Length >= 4 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46, // %PDF
            "audio/ogg" => HasAsciiAt(data, 0, "OggS"),
            "audio/mpeg" or "audio/mp3" => HasAsciiAt(data, 0, "ID3")
                                            || (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0),
            "audio/wav" or "audio/x-wav" => data.Length >= 12
                                             && HasAsciiAt(data, 0, "RIFF")
                                             && HasAsciiAt(data, 8, "WAVE"),
            "audio/mp4" or "audio/aac" or "audio/x-m4a" or "audio/m4a" or "video/mp4" => HasFtypBox(data),
            "video/webm" => data.Length >= 4 && data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "application/vnd.openxmlformats-officedocument.presentationml.presentation"
                or "application/zip" => data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04,
            _ => true
        };
    }

    private static string? NormalizeMimeType(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime))
            return null;

        var normalized = mime.Trim().ToLowerInvariant();
        var semicolon = normalized.IndexOf(';');
        if (semicolon >= 0)
            normalized = normalized[..semicolon].Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool HasKnownImageSignature(byte[] data)
    {
        if (data.Length < 4)
            return false;

        // JPEG
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return true;

        // PNG
        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return true;

        // GIF
        if (HasAsciiAt(data, 0, "GIF87a") || HasAsciiAt(data, 0, "GIF89a"))
            return true;

        // WEBP
        if (data.Length >= 12 &&
            HasAsciiAt(data, 0, "RIFF") &&
            HasAsciiAt(data, 8, "WEBP"))
            return true;

        return false;
    }

    private static bool HasFtypBox(byte[] data)
    {
        return HasAsciiAt(data, 4, "ftyp");
    }

    private static bool HasAsciiAt(byte[] data, int offset, string ascii)
    {
        if (offset < 0 || offset + ascii.Length > data.Length)
            return false;

        for (var i = 0; i < ascii.Length; i++)
        {
            if (data[offset + i] != ascii[i])
                return false;
        }

        return true;
    }

    private static bool IsLocalUploadUrl(string? mediaUrl)
    {
        return !string.IsNullOrWhiteSpace(mediaUrl) &&
               mediaUrl.StartsWith(Consts.Midia.UploadUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveLocalUploadPath(string mediaUrl, out string filePath)
    {
        filePath = string.Empty;
        if (!IsLocalUploadUrl(mediaUrl))
            return false;

        var relativeFile = mediaUrl[Consts.Midia.UploadUrlPrefix.Length..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativeFile))
            return false;

        var uploadsDir = _mediaUploadsDirectory;
        var candidate = Path.Combine(uploadsDir, relativeFile);
        var resolvedPath = Path.GetFullPath(candidate);
        var resolvedDir = Path.GetFullPath(uploadsDir);
        var relativePath = Path.GetRelativePath(resolvedDir, resolvedPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            return false;

        filePath = resolvedPath;
        return true;
    }

    private bool IsCorruptedLocalMedia(Mensagem mensagem, out string reason)
    {
        reason = string.Empty;

        if (!IsLocalUploadUrl(mensagem.MediaUrl))
            return false;

        if (!TryResolveLocalUploadPath(mensagem.MediaUrl!, out var filePath))
        {
            reason = "invalid-local-path";
            return true;
        }

        if (!System.IO.File.Exists(filePath))
        {
            reason = "missing-file";
            return true;
        }

        byte[] data;
        try
        {
            data = System.IO.File.ReadAllBytes(filePath);
        }
        catch (IOException)
        {
            reason = "unreadable-file";
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "unreadable-file";
            return true;
        }

        if (data.Length == 0)
        {
            reason = "empty-file";
            return true;
        }

        if (!IsMediaContentSane(data, mensagem.MediaType, null))
        {
            reason = "invalid-signature";
            return true;
        }

        return false;
    }

    /// <summary>
    /// [APENAS MOCK/DEV] Simula o envio de uma mensagem por um cliente externo via WhatsApp.
    /// Cria um Atendimento com status "Pendente" e uma Mensagem "Inbound" no banco de dados,
    /// e coloca a notificação na fila de processamento.
    /// Disponível apenas para Administradores e Desenvolvedores.
    /// </summary>
    [HttpPost("mock/mensagem")]
    [Authorize(Roles = "Administrador,Desenvolvedor")]
    public async Task<IActionResult> SimularMensagemCliente([FromBody] SimularMensagemRequest request)
    {
        try
        {
            var normalizedFrom = request.IsGroup
                ? SanitizeGroupJid(request.From)
                : NormalizeWhatsAppId(request.From);
            if (string.IsNullOrWhiteSpace(normalizedFrom))
                return BadRequest(new { success = false, error = "Número de origem inválido." });

            var cliente = request.IsGroup
                ? null
                : await _crmService.GetOrCreateClienteByWhatsApp(
                    request.FarmaciaId.ToString(),
                    normalizedFrom);
            if (!request.IsGroup && cliente == null)
                return StatusCode(500, new { success = false, error = "Não foi possível identificar o cliente da mensagem." });

            // Encontrar ou criar atendimento ativo para esse número/farmácia
            var atendimentosAtivosQuery = _context.Atendimentos
                .Where(a =>
                    a.FarmaciaId == request.FarmaciaId &&
                    a.StatusEnum != AtendimentoStatusEnum.Finalizado);

            atendimentosAtivosQuery = request.IsGroup
                ? atendimentosAtivosQuery.Where(a => a.WhatsAppId == normalizedFrom)
                : atendimentosAtivosQuery.Where(a => a.ClienteId == cliente!.Id || a.WhatsAppId == normalizedFrom);

            var atendimento = await atendimentosAtivosQuery
                .OrderByDescending(a => a.DataInicio)
                .FirstOrDefaultAsync();

            var novoAtendimento = false;
            var novoCliente = cliente?.TotalAtendimentos == 0;
            if (atendimento == null)
            {
                atendimento = new Atendimento
                {
                    FarmaciaId = request.FarmaciaId,
                    Farmacia = null!,
                    ClienteId = cliente?.Id,
                    Cliente = null!,
                    Atendente = null!,
                    WhatsAppId = normalizedFrom,
                    CanalEnum = CanalEnum.WhatsApp,
                    StatusEnum = AtendimentoStatusEnum.Pendente,
                    DataInicio = DateTime.UtcNow
                };
                _context.Atendimentos.Add(atendimento);
                await _context.SaveChangesAsync();
                novoAtendimento = true;
            }
            else if (!request.IsGroup && atendimento.ClienteId == null)
            {
                // Backfill ClienteId for existing attendances that are missing it
                atendimento.ClienteId = cliente!.Id;
                await _context.SaveChangesAsync();
            }

            // Registrar a mensagem recebida
            var mensagem = new Mensagem
            {
                AtendimentoId = atendimento.Id,
                MessageId = $"mock-{Guid.NewGuid()}",
                Direction = MessageDirection.Inbound,
                Type = MessageType.Text,
                Status = MessageStatus.Recebida,
                Text = request.Text,
                ReceivedAt = DateTime.UtcNow
            };
            _context.Mensagens.Add(mensagem);
            await _context.SaveChangesAsync();

            // Enfileirar para o processador de mensagens notificar os atendentes
            try
            {
                await _messageQueue.EnqueueAsync(Consts.Filas.NovasMensagens, new
                {
                    FarmaciaId = request.FarmaciaId.ToString(),
                    AtmendimentoId = atendimento.Id,
                    IsNewAtendimento = novoAtendimento
                });
            }
            catch (Exception queueEx)
            {
                _logger.LogWarning(queueEx, "Could not enqueue message notification (Redis may be unavailable). Continuing without queue.");
            }

            _logger.LogInformation(
                "🎭 MOCK: Mensagem simulada de {From} para farmácia {FarmaciaId} — atendimento #{AtendimentoId} ({IsNew}), clienteId={ClienteId}",
                request.From, request.FarmaciaId, atendimento.Id, novoAtendimento ? "novo" : "existente", cliente?.Id);

            return Ok(new
            {
                success = true,
                atendimentoId = atendimento.Id,
                mensagemId = mensagem.Id,
                clienteId = cliente?.Id,
                novoCliente,
                novoAtendimento,
                status = atendimento.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating client message");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Corrige mensagens existentes que ainda possuem URLs de mídia temporárias do WhatsApp CDN,
    /// baixando e armazenando os arquivos localmente.
    /// </summary>
    [HttpPost("fix-media-urls")]
    [Authorize(Roles = "Administrador,Desenvolvedor")]
    public async Task<IActionResult> FixExistingMediaUrls([FromQuery] int maxCount = 100)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (string.IsNullOrEmpty(farmaciaIdStr))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var farmacia = await _context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Id.ToString() == farmaciaIdStr);
            var evolutionApiKey = farmacia?.Integracoes?.WhatsAppApiKey;

            var mensagens = await _context.Mensagens
                .Where(m => m.MediaUrl != null &&
                            m.Atendimento!.FarmaciaId.ToString() == farmaciaIdStr &&
                            (m.MediaUrl.StartsWith(Consts.WhatsAppCdn.MmgPrefix) ||
                             m.MediaUrl.StartsWith(Consts.WhatsAppCdn.MmgFilesPrefix) ||
                             m.MediaUrl.Contains(Consts.WhatsAppCdn.WhatsAppNetDomain) ||
                             m.MediaUrl.StartsWith("/v/") ||
                             m.MediaUrl.StartsWith(Consts.Midia.UploadUrlPrefix)))
                .OrderBy(m => m.ReceivedAt)
                .Take(maxCount)
                .ToListAsync();

            if (!mensagens.Any())
                return Ok(new { success = true, message = "Nenhuma mensagem com URL de CDN encontrada.", fixedCount = 0 });

            var fixedCount = 0;
            var errorCount = 0;
            var clearedCount = 0;

            var evolutionApiUrl = farmacia?.Integracoes?.WhatsAppApiUrl;

            foreach (var mensagem in mensagens)
            {
                try
                {
                    var isLocalUpload = IsLocalUploadUrl(mensagem.MediaUrl);
                    var isCorruptedLocalUpload = false;
                    var corruptionReason = string.Empty;
                    if (isLocalUpload)
                    {
                        isCorruptedLocalUpload = IsCorruptedLocalMedia(mensagem, out corruptionReason);
                        if (!isCorruptedLocalUpload)
                            continue;
                    }

                    if (isCorruptedLocalUpload)
                    {
                        _logger.LogWarning("FixExistingMediaUrls: corrupted local media detected for mensagem {MensagemId} ({Reason})",
                            mensagem.Id, corruptionReason);
                    }

                    // Old messages may have directPath stored as MediaUrl (before we separated them)
                    var storedUrl = mensagem.MediaUrl!;
                    if (!string.IsNullOrWhiteSpace(storedUrl) && !storedUrl.StartsWith("http"))
                    {
                        // Looks like a directPath, construct CDN URL
                        var path = storedUrl.StartsWith("/") ? storedUrl : $"/{storedUrl}";
                        storedUrl = $"{Consts.WhatsAppCdn.MmgPrefix}{path}";
                        _logger.LogDebug("FixExistingMediaUrls: converted stored path to CDN URL: {CdnUrl}", storedUrl);
                    }

                    var localUrl = isLocalUpload
                        ? null
                        : await DownloadAndStoreMediaLocally(storedUrl, mensagem.MediaType, evolutionApiKey, mensagemId: mensagem.Id);
                    if (string.IsNullOrWhiteSpace(localUrl) && !string.IsNullOrWhiteSpace(evolutionApiUrl))
                    {
                        // Try Evolution API fallback
                        var meta = mensagem.Metadata;
                        var rawPld = meta?.TryGetValue("rawDataPayload", out var rdp) == true ? rdp as string
                            : meta?.TryGetValue("rawMessagePayload", out var rmp) == true ? rmp as string : null;
                        var mk = meta?.TryGetValue("mediaKey", out var mkVal) == true ? mkVal as string : null;
                        var encT = meta?.TryGetValue("encType", out var et) == true ? et as string : null;
                        var fs256 = meta?.TryGetValue("fileSha256", out var fs) == true ? fs as string : null;
                        var fes256 = meta?.TryGetValue("fileEncSha256", out var fes) == true ? fes as string : null;
                        var dp = meta?.TryGetValue("directPath", out var dpVal) == true ? dpVal as string : null;

                        // Backward compat: extract missing individual fields from old rawMessagePayload
                        if (!string.IsNullOrWhiteSpace(rawPld) &&
                            (string.IsNullOrWhiteSpace(mk) || string.IsNullOrWhiteSpace(dp)))
                        {
                            TryExtractMediaFieldsFromPayload(rawPld, ref mk, ref dp, ref fs256, ref fes256, ref encT);
                        }

                        localUrl = await DownloadMediaViaEvolutionApi(
                            evolutionApiUrl, evolutionApiKey,
                            mensagem.MessageId, mensagem.MediaType,
                            mk, rawPld, encT, fs256, fes256, dp,
                            farmacia?.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false);
                    }

                    if (!string.IsNullOrWhiteSpace(localUrl))
                    {
                        _logger.LogInformation(
                            "Media fix: mensagem {MensagemId} — {OldUrl} → {NewUrl}",
                            mensagem.Id, mensagem.MediaUrl, localUrl);
                        mensagem.MediaUrl = localUrl;
                        fixedCount++;
                    }
                    else if (isLocalUpload)
                    {
                        _logger.LogWarning("FixExistingMediaUrls: could not reprocess corrupted local media for mensagem {MensagemId}; clearing MediaUrl",
                            mensagem.Id);
                        mensagem.MediaUrl = null;
                        clearedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to fix media for mensagem {MensagemId}: {MediaUrl}",
                        mensagem.Id, mensagem.MediaUrl);
                    errorCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"{fixedCount} mídia(s) corrigida(s) com sucesso. {clearedCount} mídia(s) removida(s). {errorCount} falha(s).",
                fixedCount,
                clearedCount,
                errorCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing existing media URLs");
            return StatusCode(500, new { success = false, error = "Erro ao corrigir mídias existentes." });
        }
    }
}
