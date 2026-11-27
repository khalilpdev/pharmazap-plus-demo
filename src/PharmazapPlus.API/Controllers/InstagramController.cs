using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Instagram;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Text.Json;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/instagram")]
public class InstagramController : ControllerBase
{
    private readonly IInstagramService _instagramService;
    private readonly ILogger<InstagramController> _logger;
    private readonly PharmazapDbContext _context;
    private readonly IMessageQueue _messageQueue;
    private readonly IConfiguration _configuration;
    private readonly ICrmService _crmService;

    public InstagramController(
        IInstagramService instagramService,
        ILogger<InstagramController> logger,
        PharmazapDbContext context,
        IMessageQueue messageQueue,
        IConfiguration configuration,
        ICrmService crmService)
    {
        _instagramService = instagramService;
        _logger = logger;
        _context = context;
        _messageQueue = messageQueue;
        _configuration = configuration;
        _crmService = crmService;
    }

    // Webhook verification (GET)
    [HttpGet("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string hub_mode,
        [FromQuery(Name = "hub.challenge")] string hub_challenge,
        [FromQuery(Name = "hub.verify_token")] string hub_verify_token)
    {
        _logger.LogInformation("Instagram webhook verification request: Mode={Mode}",
            hub_mode);

        var isValid = await _instagramService.ValidateWebhook(hub_mode, hub_verify_token, hub_challenge);

        if (isValid)
        {
            _logger.LogInformation("Instagram webhook verification successful");
            return Ok(hub_challenge);
        }

        _logger.LogWarning("Instagram webhook verification failed");
        return Unauthorized();
    }

    // Webhook message processing (POST)
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessWebhook([FromBody] Dictionary<string, object> payload)
    {
        try
        {
            _logger.LogInformation("Received Instagram webhook payload");

            var result = await _instagramService.ProcessWebhookPayload(payload);

            if (result.Success)
            {
                _logger.LogInformation("Instagram webhook processed successfully: {Message}", result.Message);
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    atendimentoId = result.Atendimento?.Id,
                    novoAtendimento = result.NewAtendimentoCreated,
                    novoCliente = result.NewClientCreated,
                    status = result.Atendimento?.Status.ToString()
                });
            }
            else
            {
                _logger.LogWarning("Instagram webhook processing failed: {Message}", result.Message);
                return BadRequest(new { success = false, error = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Instagram webhook");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
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

            var result = await _instagramService.SendTextMessage(farmaciaId, request.To, request.Text);

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
            _logger.LogError(ex, "Error sending Instagram text message");
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
            var webhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl)
                ? Consts.Endpoints.InstagramWebhook
                : request.WebhookUrl;
            var result = await _instagramService.SetupWebhook(farmaciaId.ToString(), webhookUrl);

            return Ok(new
            {
                success = true,
                webhookUrl = result,
                officialWebhook = Consts.Endpoints.InstagramWebhook,
                message = "Instagram webhook configurado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Instagram webhook for farmacia {FarmaciaId}", farmaciaId);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    // Obter perfil do Instagram Business
    [HttpGet("profile/{farmaciaId}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GetBusinessProfile(int farmaciaId)
    {
        try
        {
            var profile = await _instagramService.GetBusinessProfile(farmaciaId.ToString());

            return Ok(new { success = true, data = profile });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Instagram profile for farmacia {FarmaciaId}", farmaciaId);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    private async Task<string> UploadMediaToStorage(IFormFile file)
    {
        // Implementar upload para Azure Blob Storage
        // Por enquanto, retornar URL mock
        return $"https://pharmazapplus.blob.core.windows.net/media/{Guid.NewGuid()}/{file.FileName}";
    }

    /// <summary>
    /// [APENAS MOCK/DEV] Simula o envio de uma mensagem por um cliente via Instagram DM.
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
            var senderId = request.From?.Trim();
            if (string.IsNullOrWhiteSpace(senderId))
                return BadRequest(new { success = false, error = "Instagram ID (From) é obrigatório." });

            var farmacia = await _context.Farmacias
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == request.FarmaciaId);

            if (farmacia == null)
                return BadRequest(new { success = false, error = "Farmácia não encontrada." });

            var cliente = await _crmService.GetOrCreateClienteByCanalContato(
                request.FarmaciaId.ToString(),
                senderId,
                CanalEnum.Instagram);

            if (cliente == null)
                return StatusCode(500, new { success = false, error = "Não foi possível identificar o cliente da mensagem." });

            var atendimento = await _context.Atendimentos
                .Where(a =>
                    a.FarmaciaId == request.FarmaciaId &&
                    a.CanalEnum == CanalEnum.Instagram &&
                    (a.ClienteId == cliente.Id || a.WhatsAppId == senderId) &&
                    a.StatusEnum != AtendimentoStatusEnum.Finalizado)
                .OrderByDescending(a => a.DataInicio)
                .FirstOrDefaultAsync();

            var novoAtendimento = false;
            var novoCliente = cliente.TotalAtendimentos == 0;
            if (atendimento == null)
            {
                atendimento = new Atendimento
                {
                    FarmaciaId = request.FarmaciaId,
                    Farmacia = null!,
                    ClienteId = cliente.Id,
                    Cliente = null!,
                    Atendente = null!,
                    WhatsAppId = senderId,
                    CanalEnum = CanalEnum.Instagram,
                    StatusEnum = AtendimentoStatusEnum.Pendente,
                    DataInicio = DateTime.UtcNow
                };
                _context.Atendimentos.Add(atendimento);
                await _context.SaveChangesAsync();
                novoAtendimento = true;
            }
            else if (atendimento.ClienteId == null)
            {
                atendimento.ClienteId = cliente.Id;
                await _context.SaveChangesAsync();
            }

            var mensagem = new Mensagem
            {
                AtendimentoId = atendimento.Id,
                MessageId = $"mock-ig-{Guid.NewGuid():N}",
                Direction = MessageDirection.Inbound,
                Type = MessageType.Text,
                Status = MessageStatus.Recebida,
                Text = request.Text?.Trim() ?? string.Empty,
                ReceivedAt = DateTime.UtcNow
            };
            _context.Mensagens.Add(mensagem);
            await _context.SaveChangesAsync();

            try
            {
                await _messageQueue.EnqueueAsync(Consts.Filas.NovasMensagens, new
                {
                    FarmaciaId = request.FarmaciaId.ToString(),
                    AtendimentoId = atendimento.Id,
                    IsNewAtendimento = novoAtendimento,
                    Canal = "Instagram"
                });
            }
            catch (Exception queueEx)
            {
                _logger.LogWarning(queueEx, "Could not enqueue Instagram message notification");
            }

            return Ok(new
            {
                success = true,
                message = $"Instagram message processed from {senderId}",
                atendimentoId = atendimento.Id,
                novoAtendimento,
                novoCliente,
                status = atendimento.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating Instagram client message");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }
}
