using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Models.Vendas;
using PharmazapPlus.Shared.Services;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AtendimentosController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<AtendimentosController> _logger;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IHubContext<PharmaHub> _hubContext;
    private readonly PedidoService _pedidoService;
    private readonly IRemetenteBloqueadoService _remetenteBloqueadoService;

    public AtendimentosController(
        PharmazapDbContext context,
        ILogger<AtendimentosController> logger,
        IWhatsAppService whatsAppService,
        IHubContext<PharmaHub> hubContext,
        PedidoService pedidoService,
        IRemetenteBloqueadoService remetenteBloqueadoService)
    {
        _context = context;
        _logger = logger;
        _whatsAppService = whatsAppService;
        _hubContext = hubContext;
        _pedidoService = pedidoService;
        _remetenteBloqueadoService = remetenteBloqueadoService;
    }

    public record EnviarMensagemRequest(
        [property: JsonPropertyName("text")] string Text);

    public record TransferirAtendimentoRequest(
        [property: JsonPropertyName("targetAtendenteId")] int TargetAtendenteId);

    public record FinalizarAtendimentoRequest(
        [property: JsonPropertyName("motivo")] string? Motivo,
        [property: JsonPropertyName("valorVenda")] decimal? ValorVenda,
        [property: JsonPropertyName("motivoEncerramentoId")] int? MotivoEncerramentoId,
        [property: JsonPropertyName("comprovanteUrl")] string? ComprovanteUrl);

    public record BloqueRemetenteRequest(
        [property: JsonPropertyName("escopo")] string Escopo = "Local",
        [property: JsonPropertyName("motivo")] string? Motivo = null);

    /// <summary>
    /// Lista atendimentos da farmácia autenticada.
    /// Use ?status=Pendente para filtrar apenas os que aguardam atendimento.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAtendimentos([FromQuery] string? status = null)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var query = _context.Atendimentos
                .Include(a => a.Cliente)
                .Include(a => a.Atendente)
                .Include(a => a.Mensagens)
                .Include(a => a.Etiqueta)
                .Where(a => a.FarmaciaId == farmaciaId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<AtendimentoStatusEnum>(status, out var statusEnum))
                    query = query.Where(a => a.StatusEnum == statusEnum);
                else
                    query = query.Where(_ => false);
            }

            // Role-based filtering: Atendentes only see their own atendimentos + pending ones
            var deveAplicarFiltroDeAtendente = !User.IsInRole(UserRoles.Administrador) && !User.IsInRole(UserRoles.Gestor);
            if (deveAplicarFiltroDeAtendente)
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out var userId))
                    query = query.Where(a => a.AtendenteId == null || a.AtendenteId == userId);
                else
                    // Cannot identify user — return only unassigned (pending) atendimentos
                    query = query.Where(a => a.AtendenteId == null);
            }

            var atendimentosDb = await query
                .OrderByDescending(a => a.DataInicio)
                .ToListAsync();

            var atendimentos = atendimentosDb
                .Select(a =>
                {
                    var ultimaMensagem = a.Mensagens
                        .OrderByDescending(m => m.ReceivedAt)
                        .FirstOrDefault();

                    return new
                    {
                    a.Id,
                    a.Status,
                    a.AtendenteId,
                    a.WhatsAppId,
                    a.Canal,
                    a.IsGrupo,
                    a.DataInicio,
                    a.DataFim,
                    ClienteNome = a.Cliente != null ? (a.Cliente.Nome + " " + a.Cliente.Sobrenome).Trim() : null,
                    AtendenteNome = a.Atendente != null ? a.Atendente.Nome : null,
                    TotalMensagens = a.Mensagens.Count,
                    UltimaMensagem = GetUltimaMensagemText(ultimaMensagem),
                    UltimaMensagemEm = (DateTime?)ultimaMensagem?.ReceivedAt,
                    EtiquetaId = a.EtiquetaId,
                    EtiquetaNome = a.Etiqueta != null ? a.Etiqueta.Nome : null,
                    EtiquetaCor = a.Etiqueta != null ? a.Etiqueta.Cor : null,
                    GroupName = a.IsGrupo
                        ? GetMensagemMetadataValue(
                            ultimaMensagem,
                            Consts.EvolutionGoWebhook.GroupNameMetadataKey)
                        : null,
                    SenderName = a.IsGrupo
                        ? GetMensagemMetadataValue(
                            ultimaMensagem,
                            Consts.EvolutionGoWebhook.SenderNameMetadataKey)
                        : null
                    };
                })
                .ToList();

            return Ok(new { success = true, total = atendimentos.Count, data = atendimentos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing atendimentos");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Retorna todas as mensagens de um atendimento específico.
    /// </summary>
    [HttpGet("{id}/mensagens")]
    public async Task<IActionResult> GetMensagens(int id)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Mensagens)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            var mensagens = atendimento.Mensagens
                .OrderBy(m => m.ReceivedAt)
                .Select(m => new
                {
                    m.Id,
                    m.MessageId,
                    Direcao = m.Direction.ToString(),
                    Tipo = m.Type.ToString(),
                    Status = m.Status.ToString(),
                    m.Text,
                    m.MediaUrl,
                    m.MediaType,
                    m.MediaCaption,
                    m.ReceivedAt,
                    m.SentAt,
                    m.DeliveredAt,
                    m.ReadAt,
                    GroupName = atendimento.IsGrupo
                        ? GetMensagemMetadataValue(m, Consts.EvolutionGoWebhook.GroupNameMetadataKey)
                        : null,
                    SenderName = atendimento.IsGrupo
                        ? GetMensagemMetadataValue(m, Consts.EvolutionGoWebhook.SenderNameMetadataKey)
                        : null
                });

            return Ok(new
            {
                success = true,
                atendimentoId = id,
                status = atendimento.Status,
                whatsAppId = atendimento.WhatsAppId,
                data = mensagens
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mensagens for atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Atendente assume o atendimento: muda status de Pendente para EmAndamento
    /// e registra qual atendente assumiu.
    /// </summary>
    [HttpPost("{id}/assumir")]
    [Authorize(Roles = $"{UserRoles.Gestor},{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> Assumir(int id)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendenteIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(atendenteIdStr, out var atendenteId))
                return BadRequest(new { success = false, error = "Atendente não identificado" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (atendimento.IsGrupo)
                return BadRequest(new { success = false, error = "Mensagens de grupo são apenas para visualização e não podem ser assumidas" });

            var isAdminOrGestor = User.IsInRole(UserRoles.Administrador) || User.IsInRole(UserRoles.Gestor);
            if (atendimento.StatusEnum != AtendimentoStatusEnum.Pendente &&
                !(isAdminOrGestor && atendimento.StatusEnum == AtendimentoStatusEnum.EmAndamento))
                return BadRequest(new { success = false, error = $"Atendimento está com status '{atendimento.Status}', não é possível assumir" });

            atendimento.StatusEnum = AtendimentoStatusEnum.EmAndamento;
            atendimento.AtendenteId = atendenteId;

            await _context.SaveChangesAsync();

            // Registrar atividade
            await RegistrarAtividadeAsync(
                farmaciaId,
                atendenteId,
                TipoAtividadeEnum.AtendimentoAssumido,
                $"Assumiu atendimento com {GetClienteNome(atendimento)}",
                atendimento.Id,
                GetClienteNome(atendimento));

            _logger.LogInformation("Atendimento {Id} assumido pelo atendente {AtendenteId}", id, atendenteId);

            return Ok(new
            {
                success = true,
                atendimentoId = id,
                status = atendimento.Status,
                atendenteId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assuming atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Envia uma mensagem de texto no contexto de um atendimento em andamento.
    /// A mensagem é persistida no banco e enviada via WhatsApp.
    /// </summary>
    [HttpPost("{id}/mensagens")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> EnviarMensagem(int id, [FromBody] EnviarMensagemRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Text))
                return BadRequest(new { success = false, error = "Texto da mensagem é obrigatório" });

            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (atendimento.IsGrupo)
                return BadRequest(new { success = false, error = "Mensagens de grupo são apenas para visualização e não permitem resposta pelo atendimento" });

            if (atendimento.StatusEnum != AtendimentoStatusEnum.EmAndamento)
                return BadRequest(new { success = false, error = $"Atendimento está com status '{atendimento.Status}', não é possível enviar mensagem" });

            // Regra #139: somente o atendente responsável pode enviar mensagens.
            // Gestor e Administrador sem ownership são barrados aqui (defesa em profundidade).
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(new { success = false, error = "Não foi possível identificar o usuário autenticado" });
            if (atendimento.AtendenteId != userId)
                return StatusCode(403, new { success = false, error = "Você não é o atendente responsável por este atendimento" });

            // Verificar se é a primeira mensagem enviada neste atendimento (antes de inserir, sem race condition)
            var isFirstOutbound = !await _context.Mensagens
                .AnyAsync(m => m.AtendimentoId == id && m.Direction == MessageDirection.Outbound);

            // Persistir mensagem no banco
            var mensagem = new Mensagem
            {
                AtendimentoId = id,
                Direction = MessageDirection.Outbound,
                Type = MessageType.Text,
                Status = MessageStatus.Processando,
                Text = request.Text.Trim(),
                ReceivedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };
            _context.Mensagens.Add(mensagem);
            await _context.SaveChangesAsync();

            // Registrar atividade de envio de mensagem (apenas a primeira por atendimento para evitar ruído)
            if (isFirstOutbound)
            {
                await RegistrarAtividadeAsync(
                    farmaciaId,
                    userId,
                    TipoAtividadeEnum.MensagemEnviada,
                    $"Respondeu ao atendimento com {GetClienteNome(atendimento)}",
                    atendimento.Id,
                    GetClienteNome(atendimento));
            }

            // Enviar via WhatsApp (tolera erro de envio: mensagem já está persistida; status marcado como Falha se falhar)
            string? sendError = null;
            try
            {
                if (!string.IsNullOrEmpty(atendimento.WhatsAppId))
                {
                    var waResponse = await _whatsAppService.SendTextMessage(
                        farmaciaId.ToString(), atendimento.WhatsAppId, request.Text.Trim());
                    if (waResponse?.Success == true)
                    {
                        mensagem.Status = MessageStatus.Enviada;
                        mensagem.MessageId = waResponse.MessageId;
                    }
                    else
                    {
                        mensagem.Status = MessageStatus.Falha;
                        mensagem.ErrorMessage = waResponse?.ErrorMessage ?? "A Evolution não confirmou o envio da mensagem.";
                        mensagem.ErrorCode = int.TryParse(waResponse?.ErrorCode, out var errorCode) ? errorCode : null;
                        sendError = mensagem.ErrorMessage;
                        _logger.LogWarning(
                            "Falha ao enviar mensagem WhatsApp no atendimento {AtendimentoId}. Success={Success} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                            id,
                            waResponse?.Success ?? false,
                            waResponse?.ErrorCode ?? "N/A",
                            waResponse?.ErrorMessage ?? "Resposta nula");
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception exWa)
            {
                _logger.LogWarning(exWa, "WhatsApp send failed for atendimento {Id}; message persisted anyway", id);
                mensagem.Status = MessageStatus.Falha;
                mensagem.ErrorMessage = "Não foi possível comunicar com o provedor WhatsApp.";
                sendError = mensagem.ErrorMessage;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Envio de mensagem no atendimento {AtendimentoId} concluído com status {Status}", id, mensagem.Status);

            return Ok(new
            {
                success = mensagem.Status != MessageStatus.Falha,
                mensagemId = mensagem.Id,
                status = mensagem.Status.ToString(),
                error = sendError
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message in atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Transfere um atendimento para outro atendente (apenas Administrador/Gestor).
    /// </summary>
    [HttpPost("{id}/transferir")]
    [Authorize(Roles = $"{UserRoles.Gestor},{UserRoles.Administrador}")]
    public async Task<IActionResult> Transferir(int id, [FromBody] TransferirAtendimentoRequest request)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (atendimento.IsGrupo)
                return BadRequest(new { success = false, error = "Mensagens de grupo são apenas para visualização e não podem ser transferidas" });

            // Transferência só é permitida para atendimentos que ainda estão em aberto
            if (atendimento.StatusEnum == AtendimentoStatusEnum.Finalizado)
                return BadRequest(new { success = false, error = "Atendimentos finalizados não podem ser transferidos" });

            // Verificar se o atendente de destino existe e pertence à mesma farmácia
            var targetAtendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == request.TargetAtendenteId && u.FarmaciaId == farmaciaId);

            if (targetAtendente == null)
                return BadRequest(new { success = false, error = "Atendente de destino não encontrado" });

            var previousAtendenteId = atendimento.AtendenteId;
            atendimento.AtendenteId = request.TargetAtendenteId;

            // Transfer promotes the atendimento to EmAndamento so it can no longer be "assumed"
            atendimento.StatusEnum = AtendimentoStatusEnum.EmAndamento;

            await _context.SaveChangesAsync();

            // Registrar atividade
            var transferidorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(transferidorIdStr, out var transferidorId))
            {
                await RegistrarAtividadeAsync(
                    farmaciaId,
                    transferidorId,
                    TipoAtividadeEnum.AtendimentoTransferido,
                    $"Transferiu atendimento com {GetClienteNome(atendimento)} para {targetAtendente.Nome}",
                    atendimento.Id,
                    GetClienteNome(atendimento));
            }

            _logger.LogInformation(
                "Atendimento {Id} transferido de atendente {From} para {To}",
                id, previousAtendenteId, request.TargetAtendenteId);

            // Notify all users in the farmácia group so boards / lists refresh
            await _hubContext.Clients
                .Group(Consts.SignalR.GrupoFarmacia(farmaciaIdStr))
                .SendAsync(Consts.SignalR.Metodos.AtendimentoTransferido, new
                {
                    AtendimentoId = id,
                    FromAtendenteId = previousAtendenteId,
                    ToAtendenteId = request.TargetAtendenteId,
                    ToAtendenteNome = targetAtendente.Nome,
                    Timestamp = DateTime.UtcNow
                });

            return Ok(new
            {
                success = true,
                atendimentoId = id,
                targetAtendenteId = request.TargetAtendenteId,
                targetAtendenteNome = targetAtendente.Nome
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Volta um atendimento em andamento para pendente, liberando-o para outro atendente assumir.
    /// Somente o atendente responsável pode executar esta ação.
    /// </summary>
    [HttpPost("{id}/voltar-pendente")]
    [Authorize(Roles = UserRoles.Atendente)]
    public async Task<IActionResult> VoltarPendente(int id)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (atendimento.IsGrupo)
                return BadRequest(new { success = false, error = "Mensagens de grupo são apenas para visualização e não podem voltar para pendente" });

            if (atendimento.StatusEnum != AtendimentoStatusEnum.EmAndamento)
                return BadRequest(new { success = false, error = "Somente atendimentos em andamento podem ser voltados para pendente" });

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId) || atendimento.AtendenteId != userId)
                return StatusCode(403, new { success = false, error = "Você não é o atendente responsável por este atendimento" });

            var previousAtendenteId = atendimento.AtendenteId;
            atendimento.StatusEnum = AtendimentoStatusEnum.Pendente;
            atendimento.AtendenteId = null;

            await _context.SaveChangesAsync();

            if (int.TryParse(userIdStr, out var requesterId))
            {
                await RegistrarAtividadeAsync(
                    farmaciaId,
                    requesterId,
                    TipoAtividadeEnum.AtendimentoVoltadoPendente,
                    $"Voltou atendimento com {GetClienteNome(atendimento)} para pendente",
                    atendimento.Id,
                    GetClienteNome(atendimento));
            }

            _logger.LogInformation("Atendimento {Id} voltado para pendente pelo atendente {AtendenteId}", id, previousAtendenteId);

            await _hubContext.Clients
                .Group(Consts.SignalR.GrupoFarmacia(farmaciaIdStr))
                .SendAsync(Consts.SignalR.Metodos.AtendimentoVoltadoPendente, new
                {
                    AtendimentoId = id,
                    FromAtendenteId = previousAtendenteId,
                    Timestamp = DateTime.UtcNow
                });

            return Ok(new
            {
                success = true,
                atendimentoId = id,
                status = atendimento.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning atendimento {Id} to pending", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Finaliza um atendimento em andamento, registrando DataFim, motivo e mudando status para Finalizado.
    /// Valida regras do motivo de encerramento cadastrado (exigência de valor e comprovante).
    /// </summary>
    [HttpPost("{id}/finalizar")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> Finalizar(int id, [FromBody] FinalizarAtendimentoRequest? request = null)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (atendimento.IsGrupo)
                return BadRequest(new { success = false, error = "Mensagens de grupo são apenas para visualização e não podem ser encerradas como atendimento" });

            if (atendimento.StatusEnum == AtendimentoStatusEnum.Finalizado)
                return BadRequest(new { success = false, error = "Atendimento já está finalizado" });

            // Somente o atendente responsável pode encerrar o atendimento
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId) || atendimento.AtendenteId != userId)
            {
                var errorMsg = atendimento.AtendenteId.HasValue
                    ? "Você não pode encerrar um atendimento atribuído a outro atendente."
                    : "Você não pode encerrar um atendimento que não está atribuído a você.";
                return StatusCode(403, new { success = false, error = errorMsg });
            }

            // Validar motivo de encerramento cadastrado
            MotivoEncerramento? motivoCadastrado = null;
            if (request?.MotivoEncerramentoId.HasValue == true)
            {
                motivoCadastrado = await _context.MotivosEncerramento
                    .FirstOrDefaultAsync(m => m.Id == request.MotivoEncerramentoId.Value
                                           && m.FarmaciaId == farmaciaId
                                           && m.Ativo);

                if (motivoCadastrado == null)
                    return BadRequest(new { success = false, error = "Motivo de encerramento não encontrado ou inativo" });

                if (motivoCadastrado.ExigeValorVenda && (request.ValorVenda == null || request.ValorVenda <= 0))
                    return BadRequest(new { success = false, error = "O motivo selecionado exige que o valor da venda seja informado" });

                if (motivoCadastrado.ExigeComprovante && string.IsNullOrWhiteSpace(request.ComprovanteUrl))
                    return BadRequest(new { success = false, error = "O motivo selecionado exige que um comprovante (imagem) seja anexado" });
            }

            atendimento.StatusEnum = AtendimentoStatusEnum.Finalizado;
            atendimento.DataFim = DateTime.UtcNow;

            // Registrar motivo (usa o nome do cadastro ou o texto livre)
            if (motivoCadastrado != null)
            {
                atendimento.MotivoEncerramentoId = motivoCadastrado.Id;
                atendimento.ComentarioAvaliacao = motivoCadastrado.Nome;
            }
            else if (!string.IsNullOrWhiteSpace(request?.Motivo))
            {
                atendimento.ComentarioAvaliacao = request.Motivo.Trim();
            }

            if (request?.ValorVenda.HasValue == true && request.ValorVenda.Value > 0)
                atendimento.ValorVenda = request.ValorVenda.Value;

            if (!string.IsNullOrWhiteSpace(request?.ComprovanteUrl))
                atendimento.ComprovanteUrl = request.ComprovanteUrl.Trim();

            if (request?.ValorVenda > 0)
            {
                atendimento.ValorVenda = request.ValorVenda.Value;

                if (atendimento.Cliente != null)
                {
                    atendimento.Cliente.AdicionarPedido(request.ValorVenda.Value);
                    atendimento.Cliente.AtualizarPerfilCompra();

                    if (atendimento.Cliente.Status == ClienteStatus.Potencial)
                        atendimento.Cliente.Status = ClienteStatus.Ativo;
                }

                var pedido = _pedidoService.CriarDeAtendimento(atendimento, request.ValorVenda.Value);
                _context.Pedidos.Add(pedido);
            }

            await _context.SaveChangesAsync();

            // Registrar atividade
            var finalizadorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(finalizadorIdStr, out var finalizadorId))
            {
                await RegistrarAtividadeAsync(
                    farmaciaId,
                    finalizadorId,
                    TipoAtividadeEnum.AtendimentoFinalizado,
                    $"Finalizou atendimento com {GetClienteNome(atendimento)}",
                    atendimento.Id,
                    GetClienteNome(atendimento));
            }

            _logger.LogInformation("Atendimento {Id} finalizado. MotivoId: {MotivoId}, Motivo: {Motivo}",
                id, request?.MotivoEncerramentoId, atendimento.ComentarioAvaliacao);

            return Ok(new
            {
                success = true,
                atendimentoId = id,
                status = atendimento.Status,
                dataFim = atendimento.DataFim
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finalizing atendimento {id} [{ex.Message}]");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Upload de comprovante (imagem) para finalização de atendimento.
    /// Retorna a URL do arquivo salvo para ser incluída na requisição de finalização.
    /// </summary>
    [HttpPost("{id}/comprovante")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> UploadComprovante(int id, IFormFile file)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, error = "Arquivo não enviado" });

            // Validar tipo de arquivo (apenas imagens)
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { success = false, error = "Tipo de arquivo não permitido. Envie uma imagem (JPEG, PNG, GIF ou WebP)" });

            // Validar tamanho (máx 10MB)
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, error = "Arquivo muito grande. Tamanho máximo: 10MB" });

            // Validate file extension against whitelist
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { success = false, error = "Extensão de arquivo não permitida." });

            // Validate magic bytes match claimed content type
            using var headerStream = file.OpenReadStream();
            var headerBytes = new byte[8];
            var bytesRead = await headerStream.ReadAsync(headerBytes, 0, headerBytes.Length);
            headerStream.Position = 0;

            if (!IsValidImageMagicBytes(headerBytes, bytesRead))
                return BadRequest(new { success = false, error = "O conteúdo do arquivo não corresponde a uma imagem válida." });

            // Salvar em uploads/comprovantes/ with sanitized filename (GUID-based, no user input in path)
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "comprovantes");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{farmaciaId}_{id}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Verify resolved path is within expected directory (prevent path traversal)
            var resolvedPath = Path.GetFullPath(filePath);
            var resolvedDir = Path.GetFullPath(uploadsDir);
            if (!resolvedPath.StartsWith(resolvedDir + Path.DirectorySeparatorChar) && resolvedPath != resolvedDir)
                return BadRequest(new { success = false, error = "Caminho de arquivo inválido." });

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await headerStream.CopyToAsync(stream);
            }

            var comprovanteUrl = $"/uploads/comprovantes/{fileName}";

            _logger.LogInformation("Comprovante uploaded for atendimento {Id}: {Url}", id, comprovanteUrl);

            return Ok(new { success = true, comprovanteUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading comprovante for atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro ao fazer upload do comprovante" });
        }
    }

    /// <summary>
    /// Atualiza a etiqueta de um atendimento (Administrador ou Atendente).
    /// </summary>
    [HttpPut("{id}/etiqueta")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> AtualizarEtiqueta(int id, [FromBody] AtualizarEtiquetaAtendimentoRequest request)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimento = await _context.Atendimentos
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            if (request.EtiquetaId.HasValue)
            {
                var etiquetaValida = await _context.Etiquetas
                    .AnyAsync(e => e.Id == request.EtiquetaId.Value && e.FarmaciaId == farmaciaId && e.Ativo);
                if (!etiquetaValida)
                    return BadRequest(new { success = false, error = "Etiqueta não encontrada ou inativa" });

                atendimento.EtiquetaId = request.EtiquetaId.Value;
            }
            else
            {
                atendimento.EtiquetaId = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Etiqueta do atendimento {Id} atualizada para {EtiquetaId}", id, atendimento.EtiquetaId);

            return Ok(new { success = true, message = "Etiqueta atualizada com sucesso", etiquetaId = atendimento.EtiquetaId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating etiqueta for atendimento {Id}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    private static string GetClienteNome(Atendimento atendimento)
    {
        if (atendimento.Cliente != null)
        {
            var nome = $"{atendimento.Cliente.Nome} {atendimento.Cliente.Sobrenome}".Trim();
            return string.IsNullOrEmpty(nome) ? atendimento.WhatsAppId.FormatTelefone() : nome;
        }
        return atendimento.WhatsAppId.FormatTelefone();
    }

    private static string? GetMensagemMetadataValue(Mensagem? mensagem, string key)
    {
        if (mensagem == null || string.IsNullOrWhiteSpace(key))
            return null;

        var metadata = mensagem.Metadata;
        if (!metadata.TryGetValue(key, out var value))
            return null;

        var valueAsString = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(valueAsString) ? null : valueAsString;
    }

    /// <summary>
    /// Returns a displayable text for the last message preview in the conversation list,
    /// delegating to <see cref="Mensagem.DisplayText"/>.
    /// </summary>
    private static string? GetUltimaMensagemText(Mensagem? mensagem) => mensagem?.DisplayText;

    private async Task RegistrarAtividadeAsync(
        int farmaciaId,
        int usuarioId,
        TipoAtividadeEnum tipo,
        string descricao,
        int? atendimentoId = null,
        string? clienteNome = null)
    {
        try
        {
            var usuarioNome = await _context.UsuariosFarmacia
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Nome)
                .FirstOrDefaultAsync() ?? string.Empty;

            _context.AtividadesRecentes.Add(new AtividadeRecente
            {
                FarmaciaId = farmaciaId,
                UsuarioId = usuarioId,
                UsuarioNome = usuarioNome,
                TipoAtividade = tipo,
                Descricao = descricao,
                AtendimentoId = atendimentoId,
                ClienteNome = clienteNome,
                DataHora = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Activity recording should never break the main flow
            _logger.LogWarning(ex, "Failed to register activity for user {UserId}", usuarioId);
        }
    }

    /// <summary>
    /// Validates the first bytes of a file match known image format magic bytes.
    /// </summary>
    private static bool IsValidImageMagicBytes(byte[] bytes, int bytesRead)
    {
        if (bytesRead < 3) return false;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47
        if (bytesRead >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;

        // GIF: 47 49 46
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return true;

        // WEBP: RIFF....WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        if (bytesRead >= 4 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
            return true;

        return false;
    }

    /// <summary>
    /// Bloqueia um remetente (adiciona à lista negra) e deleta seu atendimento.
    /// Atendentes bloqueiam localmente; Administradores podem escolher o escopo.
    /// </summary>
    /// <param name="id">ID do atendimento</param>
    /// <param name="request">Escopo (Local/Global) e motivo do bloqueio</param>
    [HttpDelete("{id}/bloquear-remetente")]
    public async Task<IActionResult> BloqueRemetente(int id, [FromBody] BloqueRemetenteRequest request)
    {
        try
        {
            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
                return BadRequest(new { success = false, error = "Usuário não identificado" });

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Buscar atendimento
            var atendimento = await _context.Atendimentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == id && a.FarmaciaId == farmaciaId);

            if (atendimento == null)
                return NotFound(new { success = false, error = "Atendimento não encontrado" });

            // Verificar se o bloqueio de remetentes está habilitado para a farmácia
            var farmaciaIntegracoes = await _context.FarmaciaIntegracoes
                .FirstOrDefaultAsync(fi => fi.FarmaciaId == farmaciaId);

            if (farmaciaIntegracoes == null || !farmaciaIntegracoes.HabilitarBloqueioRemetente)
                return BadRequest(new { success = false, error = "Bloqueio de remetentes não está habilitado para esta farmácia" });

            // Validar permissão e escopo
            var escopo = RemetentoBloqueioEscopoEnum.Local;
            
            if (userRole != UserRoles.Administrador && userRole != UserRoles.Gestor)
            {
                // Atendente sempre bloqueia localmente
                escopo = RemetentoBloqueioEscopoEnum.Local;
            }
            else
            {
                // Admin/Gestor podem escolher
                if (Enum.TryParse<RemetentoBloqueioEscopoEnum>(request.Escopo, out var escopoRequest))
                    escopo = escopoRequest;
            }

            // Buscar nome do usuário
            var usuario = await _context.UsuariosFarmacia.FirstOrDefaultAsync(u => u.Id == usuarioId);
            var nomeUsuario = usuario?.Nome ?? "Desconhecido";

            // Bloquear remetente
            await _remetenteBloqueadoService.BloqueRemetenteAsync(
                atendimento.WhatsAppId,
                farmaciaId,
                escopo,
                usuarioId,
                nomeUsuario,
                request.Motivo);

            // Deletar mensagens do atendimento
            var mensagens = await _context.Mensagens
                .Where(m => m.AtendimentoId == atendimento.Id)
                .ToListAsync();

            _context.Mensagens.RemoveRange(mensagens);

            // Deletar atendimento
            _context.Atendimentos.Remove(atendimento);

            await _context.SaveChangesAsync();

            // Notificar via SignalR
            await _hubContext.Clients.All.SendAsync("AtendimentoDeleted", atendimento.Id);

            _logger.LogInformation(
                "Remetente bloqueado: {numero} (Atendimento {id}, Escopo: {escopo}, Usuário: {usuario})",
                atendimento.WhatsAppId,
                atendimento.Id,
                escopo,
                nomeUsuario);

            return Ok(new
            {
                success = true,
                message = $"Remetente bloqueado com sucesso (Escopo: {escopo})",
                escopo = escopo.ToString(),
                atendimentoId = atendimento.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao bloquear remetente no atendimento {id}", id);
            return StatusCode(500, new { success = false, error = "Erro ao bloquear remetente" });
        }
    }
}

public record AtualizarEtiquetaAtendimentoRequest(int? EtiquetaId);
