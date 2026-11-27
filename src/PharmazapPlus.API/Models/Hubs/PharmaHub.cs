#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Models.Hubs;
using PharmazapPlus.API.Services.Hubs;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Security.Claims;

namespace PharmazapPlus.API.Hubs;

[Authorize]
public class PharmaHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<PharmaHub> _logger;
    private readonly PharmazapDbContext _dbContext;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IHubContext<PharmaHub> _hubContext;

    public PharmaHub(
        IConnectionManager connectionManager,
        ILogger<PharmaHub> logger,
        PharmazapDbContext dbContext,
        IWhatsAppService whatsAppService,
        IHubContext<PharmaHub> hubContext)
    {
        _connectionManager = connectionManager;
        _logger = logger;
        _dbContext = dbContext;
        _whatsAppService = whatsAppService;
        _hubContext = hubContext;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var farmaciaId = user?.FindFirst(Consts.Claims.FarmaciaId)?.Value;
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(farmaciaId) && !string.IsNullOrEmpty(userId))
        {
            // Adicionar ao grupo da farmácia
            await Groups.AddToGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoFarmacia(farmaciaId));

            // Se for atendente, adicionar ao grupo de atendentes
            if (role == UserRoles.Atendente)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoAtendentes(farmaciaId));
            }

            _connectionManager.AddConnection(userId, Context.ConnectionId, farmaciaId, role);

            _logger.LogInformation("User {UserId} connected to farmacia {FarmaciaId}", userId, farmaciaId);

            // Notificar que o usuário está online
            await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
                .SendAsync(Consts.SignalR.Metodos.UserStatusChanged, new
                {
                    UserId = userId,
                    IsOnline = true,
                    Role = role,
                    Timestamp = DateTime.UtcNow
                });
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        var farmaciaId = _connectionManager.GetFarmaciaId(userId);
        var role = _connectionManager.GetUserRole(userId);

        if (!string.IsNullOrEmpty(farmaciaId))
        {
            // Remover dos grupos
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoFarmacia(farmaciaId));

            if (role == UserRoles.Atendente)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoAtendentes(farmaciaId));
            }

            // Se for atendente, verificar se precisa transferir atendimentos
            if (role == UserRoles.Atendente)
            {
                await TransferAtendimentos(userId, farmaciaId);
            }

            _connectionManager.RemoveConnection(userId);

            _logger.LogInformation("User {UserId} disconnected from farmacia {FarmaciaId}", userId, farmaciaId);

            // Notificar que o usuário está offline
            await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
                .SendAsync(Consts.SignalR.Metodos.UserStatusChanged, new
                {
                    UserId = userId,
                    IsOnline = false,
                    Role = role,
                    Timestamp = DateTime.UtcNow
                });
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinAtendimento(int atendimentoId)
    {
        var user = Context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var farmaciaId = user?.FindFirst(Consts.Claims.FarmaciaId)?.Value;

        if (string.IsNullOrEmpty(farmaciaId) || string.IsNullOrEmpty(userId))
        {
            throw new HubException("Usuário não autenticado");
        }

        // Verificar se o atendimento pertence à farmácia
        var atendimento = await _dbContext.Atendimentos
            .FirstOrDefaultAsync(a => a.Id == atendimentoId &&
                                     a.FarmaciaId.ToString() == farmaciaId);

        if (atendimento == null)
        {
            throw new HubException("Atendimento não encontrado ou sem permissão");
        }

        // Atualizar atendente no atendimento
        atendimento.AtendenteId = int.TryParse(userId, out int uid) ? uid : null;
        atendimento.StatusEnum = AtendimentoStatusEnum.EmAndamento;

        await _dbContext.SaveChangesAsync();

        // Entrar no grupo do atendimento
        await Groups.AddToGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoAtendimento(atendimentoId));

        // Notificar que o atendimento foi assumido
        await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
            .SendAsync(Consts.SignalR.Metodos.AtendimentoAssumido, new
            {
                AtendimentoId = atendimentoId,
                AtendenteId = userId,
                AtendenteNome = user!.FindFirst(ClaimTypes.Name)?.Value,
                Timestamp = DateTime.UtcNow
            });

        // Enviar histórico de mensagens para o novo atendente
        var mensagens = await _dbContext.Mensagens
            .Where(m => m.AtendimentoId == atendimentoId)
            .OrderBy(m => m.ReceivedAt)
            .Select(m => new
            {
                m.Id,
                m.Text,
                m.Type,
                m.Direction,
                m.Status,
                m.ReceivedAt,
                m.MediaUrl,
                m.MediaCaption
            })
            .ToListAsync();

        await Clients.Caller.SendAsync(Consts.SignalR.Metodos.LoadMensagens, mensagens);
    }

    public async Task LeaveAtendimento(int atendimentoId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Consts.SignalR.GrupoAtendimento(atendimentoId));
    }

    public async Task SendMessage(int atendimentoId, string text, string messageType = "text")
    {
        var user = Context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var farmaciaId = user?.FindFirst(Consts.Claims.FarmaciaId)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(farmaciaId))
        {
            throw new HubException("Usuário não autenticado");
        }

        // Buscar atendimento e cliente
        var atendimento = await _dbContext.Atendimentos
            .Include(a => a.Cliente)
            .FirstOrDefaultAsync(a => a.Id == atendimentoId &&
                                     a.FarmaciaId.ToString() == farmaciaId);

        if (atendimento == null || atendimento.Cliente == null)
        {
            throw new HubException("Atendimento ou cliente não encontrado");
        }

        // Enviar mensagem via WhatsApp
        var response = await _whatsAppService.SendTextMessage(
            farmaciaId, atendimento.Cliente.WhatsAppId, text);

        if (response != null && response.Success)
        {
            // Criar mensagem no banco
            var mensagem = new Mensagem
            {
                AtendimentoId = atendimentoId,
                MessageId = response.MessageId,
                Direction = MessageDirection.Outbound,
                Type = MessageType.Text,
                Status = MessageStatus.Enviada,
                Text = text,
                SentAt = DateTime.UtcNow
            };

            await _dbContext.Mensagens.AddAsync(mensagem);
            await _dbContext.SaveChangesAsync();

            // Broadcast para todos no atendimento
            await _hubContext.Clients.Group(Consts.SignalR.GrupoAtendimento(atendimentoId))
                .SendAsync(Consts.SignalR.Metodos.NewMessage, new
                {
                    mensagem.Id,
                    mensagem.Text,
                    mensagem.Type,
                    mensagem.Direction,
                    mensagem.Status,
                    mensagem.SentAt,
                    Sender = new
                    {
                        Id = userId,
                        Name = user!.FindFirst(ClaimTypes.Name)?.Value,
                        Role = user!.FindFirst(ClaimTypes.Role)?.Value
                    }
                });
        }
        else
        {
            throw new HubException($"Erro ao enviar mensagem: {response?.ErrorMessage ?? "Erro desconhecido"}");
        }
    }

    public async Task Typing(int atendimentoId, bool isTyping)
    {
        var user = Context.User;
        await _hubContext.Clients.Group(Consts.SignalR.GrupoAtendimento(atendimentoId))
            .SendAsync(Consts.SignalR.Metodos.UserTyping, new
            {
                UserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserName = user?.FindFirst(ClaimTypes.Name)?.Value,
                IsTyping = isTyping,
                Timestamp = DateTime.UtcNow
            });
    }

    public async Task UpdateMessageStatus(string messageId, MessageStatus status)
    {
        var mensagem = await _dbContext.Mensagens
            .FirstOrDefaultAsync(m => m.MessageId == messageId);

        if (mensagem != null)
        {
            mensagem.Status = status;

            switch (status)
            {
                case MessageStatus.Entregue:
                    mensagem.DeliveredAt = DateTime.UtcNow;
                    break;
                case MessageStatus.Lida:
                    mensagem.ReadAt = DateTime.UtcNow;
                    break;
            }

            await _dbContext.SaveChangesAsync();

            // Notificar atualização de status
            await _hubContext.Clients.Group(Consts.SignalR.GrupoAtendimento(mensagem.AtendimentoId))
                .SendAsync(Consts.SignalR.Metodos.MessageStatusUpdated, new
                {
                    MessageId = messageId,
                    Status = status,
                    Timestamp = DateTime.UtcNow
                });
        }
    }

    public async Task TransferAtendimento(int atendimentoId, string targetAtendenteId)
    {
        var user = Context.User;
        var farmaciaId = user?.FindFirst(Consts.Claims.FarmaciaId)?.Value;

        if (string.IsNullOrEmpty(farmaciaId))
        {
            throw new HubException("Usuário não autenticado");
        }

        // Verificar permissões
        if (user!.FindFirst(ClaimTypes.Role)?.Value != UserRoles.Administrador)
        {
            throw new HubException("Apenas administradores podem transferir atendimentos");
        }

        var atendimento = await _dbContext.Atendimentos
            .FirstOrDefaultAsync(a => a.Id == atendimentoId &&
                                     a.FarmaciaId.ToString() == farmaciaId);

        if (atendimento == null)
        {
            throw new HubException("Atendimento não encontrado");
        }

        // Atualizar atendente
        atendimento.AtendenteId = int.TryParse(targetAtendenteId, out int targetId) ? targetId : null;
        await _dbContext.SaveChangesAsync();

        // Notificar transferência
        await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
            .SendAsync(Consts.SignalR.Metodos.AtendimentoTransferido, new
            {
                AtendimentoId = atendimentoId,
                FromAtendenteId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                ToAtendenteId = targetAtendenteId,
                Timestamp = DateTime.UtcNow
            });
    }

    public async Task NotifyClienteUpdated(int clienteId, string farmaciaId, string action)
    {
        await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
            .SendAsync(Consts.SignalR.Metodos.ClienteUpdated, new
            {
                ClienteId = clienteId,
                Action = action, // "created", "updated", "deleted"
                Timestamp = DateTime.UtcNow
            });
    }

    private async Task TransferAtendimentos(string atendenteId, string farmaciaId)
    {
        try
        {
            // Buscar atendimentos em andamento deste atendente
            var atendimentos = await _dbContext.Atendimentos
                .Where(a => a.FarmaciaId.ToString() == farmaciaId &&
                           a.AtendenteId.ToString() == atendenteId &&
                           a.Status == AtendimentoStatusEnum.EmAndamento.ToString())
                .ToListAsync();

            if (atendimentos.Any())
            {
                // Buscar outro atendente online da mesma farmácia
                var outroAtendente = _connectionManager
                    .GetOnlineAtendentes(farmaciaId)
                    .FirstOrDefault(c => c.UserId != atendenteId);

                if (outroAtendente != null)
                {
                    foreach (var atendimento in atendimentos)
                    {
                        atendimento.AtendenteId = int.TryParse(outroAtendente.UserId, out int newId) ? newId : null;

                        // Notificar transferência
                        await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
                            .SendAsync(Consts.SignalR.Metodos.AtendimentoTransferidoAuto, new
                            {
                                AtendimentoId = atendimento.Id,
                                FromAtendenteId = atendenteId,
                                ToAtendenteId = outroAtendente.UserId,
                                Reason = "Atendente desconectado",
                                Timestamp = DateTime.UtcNow
                            });
                    }

                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    // Se não há outro atendente, marcar como pendente
                    foreach (var atendimento in atendimentos)
                    {
                        atendimento.AtendenteId = null;
                        atendimento.StatusEnum = AtendimentoStatusEnum.Pendente;
                    }

                    await _dbContext.SaveChangesAsync();

                    await _hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
                        .SendAsync(Consts.SignalR.Metodos.AtendimentosReabertos, new
                        {
                            AtendimentoIds = atendimentos.Select(a => a.Id).ToList(),
                            Reason = "Atendente desconectado - sem substituto",
                            Timestamp = DateTime.UtcNow
                        });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring atendimentos for atendente {AtendenteId}", atendenteId);
        }
    }
}