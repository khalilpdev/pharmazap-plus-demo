using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.Campanhas;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.Shared;
using System.Text.Json;

namespace PharmazapPlus.API.BackgroundServices;

public class MessageQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageQueueProcessor> _logger;
    private readonly IConfiguration _configuration;

    private Timer? _timer;
    private bool _isProcessing = false;

    public MessageQueueProcessor(
        IServiceProvider serviceProvider,
        ILogger<MessageQueueProcessor> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Queue Processor iniciado");

        _timer = new Timer(async _ => await ProcessQueuesAsync(),
            null, TimeSpan.Zero, TimeSpan.FromSeconds(Consts.ProcessamentoDeMensagens.IntervalSeconds));

        return Task.CompletedTask;
    }

    private async Task ProcessQueuesAsync()
    {
        if (_isProcessing) return;

        _isProcessing = true;

        var cycleStart = DateTime.UtcNow;
        _logger.LogInformation("ProcessQueuesAsync ciclo iniciado em {cycleStart}", cycleStart);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<IMessageQueue>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<PharmaHub>>();

            // Processar fila de novas mensagens
            await ProcessNewMessagesQueue(queueService, hubContext);

            // Processar fila de notificações
            await ProcessNotificationsQueue(queueService, hubContext);

            // Processar fila de agendamentos
            await ProcessAgendamentosQueue(queueService, hubContext);

            // Processar campanhas agendadas com disparo pendente
            await ProcessarCampanhasPendentesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing queues");
        }
        finally
        {
            _isProcessing = false;
            _logger.LogInformation("ProcessQueuesAsync ciclo concluído em {Elapsed}ms", (DateTime.UtcNow - cycleStart).TotalMilliseconds);
        }
    }

    private async Task ProcessNewMessagesQueue(IMessageQueue queueService, IHubContext<PharmaHub> hubContext)
    {
        try
        {
            var queueLength = await queueService.GetQueueLengthAsync(Consts.Filas.NovasMensagens);

            if (queueLength > 0)
            {
                _logger.LogInformation("ProcessNewMessagesQueue: {Count} mensagem(ns) encontrada(s) na fila {Queue}",
                    queueLength, Consts.Filas.NovasMensagens);

                for (int i = 0; i < Math.Min(queueLength, Consts.ProcessamentoDeMensagens.MaxMensagensPorCiclo); i++)
                {
                    var message = await queueService.DequeueAsync<object>(Consts.Filas.NovasMensagens);

                    if (message is not null)
                    {
                        _logger.LogInformation("ProcessNewMessagesQueue: mensagem {Index}/{Total} dequeued: {Content}",
                            i + 1, Math.Min(queueLength, Consts.ProcessamentoDeMensagens.MaxMensagensPorCiclo),
                            JsonSerializer.Serialize(message));
                        await NotifyNewMessage(hubContext, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing new-messages queue [{ex.ToString()}]");
        }
    }

    private async Task NotifyNewMessage(IHubContext<PharmaHub> hubContext, object message)
    {
        try
        {
            string? farmaciaId = null;
            int atendimentoId = 0;
            bool isNewAtendimento = false;

            if (message is JsonElement jsonMessage)
            {
                if (jsonMessage.TryGetProperty("FarmaciaId", out var farmaciaIdElement))
                {
                    farmaciaId = farmaciaIdElement.ValueKind == JsonValueKind.String
                        ? farmaciaIdElement.GetString()
                        : farmaciaIdElement.ToString();
                }

                JsonElement atendimentoIdElement;
                var hasAtendimentoIdProperty = jsonMessage.TryGetProperty("AtmendimentoId", out atendimentoIdElement)
                    || jsonMessage.TryGetProperty("AtendimentoId", out atendimentoIdElement);

                if (hasAtendimentoIdProperty)
                {
                    if (atendimentoIdElement.ValueKind == JsonValueKind.Number)
                    {
                        atendimentoId = atendimentoIdElement.GetInt32();
                    }
                    else if (atendimentoIdElement.ValueKind == JsonValueKind.String)
                    {
                        int.TryParse(atendimentoIdElement.GetString(), out atendimentoId);
                    }
                }

                if (jsonMessage.TryGetProperty("IsNewAtendimento", out var isNewAtendimentoElement))
                {
                    if (isNewAtendimentoElement.ValueKind == JsonValueKind.True || isNewAtendimentoElement.ValueKind == JsonValueKind.False)
                    {
                        isNewAtendimento = isNewAtendimentoElement.GetBoolean();
                    }
                    else if (isNewAtendimentoElement.ValueKind == JsonValueKind.String)
                    {
                        bool.TryParse(isNewAtendimentoElement.GetString(), out isNewAtendimento);
                    }
                }
            }
            else
            {
                var messageType = message.GetType();
                farmaciaId = messageType.GetProperty("FarmaciaId")?.GetValue(message)?.ToString();

                var atendimentoIdProperty = messageType.GetProperty("AtmendimentoId")
                    ?? messageType.GetProperty("AtendimentoId");
                var atendimentoIdValue = atendimentoIdProperty?.GetValue(message);
                int.TryParse(atendimentoIdValue?.ToString(), out atendimentoId);

                bool.TryParse(messageType.GetProperty("IsNewAtendimento")?.GetValue(message)?.ToString(), out isNewAtendimento);
            }

            _logger.LogInformation("NotifyNewMessage: farmaciaId={FarmaciaId}, atendimentoId={AtendimentoId}, isNewAtendimento={IsNew}",
                farmaciaId, atendimentoId, isNewAtendimento);

            if (string.IsNullOrEmpty(farmaciaId))
            {
                _logger.LogWarning("NotifyNewMessage: farmaciaId vazio — notificação ignorada. Mensagem recebida: {Content}",
                    JsonSerializer.Serialize(message));
                return;
            }

            if (atendimentoId == 0)
            {
                _logger.LogWarning("NotifyNewMessage: atendimentoId == 0 — notificação ignorada. Mensagem recebida: {Content}",
                    JsonSerializer.Serialize(message));
                return;
            }

            // Notificar farmácia sobre nova mensagem
            _logger.LogInformation("NotifyNewMessage: enviando {Metodo} para grupo {Grupo}",
                Consts.SignalR.Metodos.NovaMensagemRecebida, Consts.SignalR.GrupoFarmacia(farmaciaId));

            await hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(farmaciaId))
                .SendAsync(Consts.SignalR.Metodos.NovaMensagemRecebida, new
                {
                    AtendimentoId = atendimentoId,
                    IsNewAtendimento = isNewAtendimento,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("NotifyNewMessage: {Metodo} enviado com sucesso para farmacia {FarmaciaId}, atendimento {AtendimentoId}",
                Consts.SignalR.Metodos.NovaMensagemRecebida, farmaciaId, atendimentoId);

            // Se for novo atendimento, notificar atendentes
            if (isNewAtendimento)
            {
                _logger.LogInformation("NotifyNewMessage: enviando {Metodo} para grupo {Grupo}",
                    Consts.SignalR.Metodos.NovoAtendimentoDisponivel, Consts.SignalR.GrupoAtendentes(farmaciaId));

                await hubContext.Clients.Group(Consts.SignalR.GrupoAtendentes(farmaciaId))
                    .SendAsync(Consts.SignalR.Metodos.NovoAtendimentoDisponivel, new
                    {
                        AtendimentoId = atendimentoId,
                        Timestamp = DateTime.UtcNow
                    });

                _logger.LogInformation("NotifyNewMessage: {Metodo} enviado com sucesso para farmacia {FarmaciaId}, atendimento {AtendimentoId}",
                    Consts.SignalR.Metodos.NovoAtendimentoDisponivel, farmaciaId, atendimentoId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying about new message");
        }
    }

    private async Task ProcessNotificationsQueue(IMessageQueue queueService, IHubContext<PharmaHub> hubContext)
    {
        try
        {
            var queueLength = await queueService.GetQueueLengthAsync(Consts.Filas.Notificacoes);

            if (queueLength > 0)
            {
                _logger.LogInformation("ProcessNotificationsQueue: {Count} notificação(ões) encontrada(s) na fila {Queue}",
                    queueLength, Consts.Filas.Notificacoes);

                for (int i = 0; i < Math.Min(queueLength, Consts.ProcessamentoDeMensagens.MaxNotificacoesPorCiclo); i++)
                {
                    var notification = await queueService.DequeueAsync<NotificationMessage>(Consts.Filas.Notificacoes);

                    if (notification != null)
                    {
                        await SendNotification(hubContext, notification);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notifications queue");
        }
    }

    private async Task SendNotification(IHubContext<PharmaHub> hubContext, NotificationMessage notification)
    {
        try
        {
            if (!string.IsNullOrEmpty(notification.FarmaciaId))
            {
                await hubContext.Clients.Group(Consts.SignalR.GrupoFarmacia(notification.FarmaciaId))
                    .SendAsync(Consts.SignalR.Metodos.Notification, new
                    {
                        notification.Type,
                        notification.Title,
                        notification.Message,
                        notification.Data,
                        Timestamp = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
        }
    }

    private async Task ProcessAgendamentosQueue(IMessageQueue queueService, IHubContext<PharmaHub> hubContext)
    {
        try
        {
            var queueLength = await queueService.GetQueueLengthAsync(Consts.Filas.Agendamentos);

            if (queueLength > 0)
            {
                _logger.LogInformation("ProcessAgendamentosQueue: {Count} agendamento(s) encontrado(s) na fila {Queue}",
                    queueLength, Consts.Filas.Agendamentos);

                var agendamentos = await queueService.PeekAsync<AgendamentoReminder>(Consts.Filas.Agendamentos, Consts.ProcessamentoDeMensagens.MaxAgendamentosParaVerificar);

                foreach (var agendamento in agendamentos)
                {
                    if (agendamento?.ReminderTime <= DateTime.UtcNow)
                    {
                        // Processar agendamento
                        await ProcessAgendamentoReminder(hubContext, agendamento);

                        // Remover da fila
                        await queueService.DequeueAsync<AgendamentoReminder>(Consts.Filas.Agendamentos);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agendamentos queue");
        }
    }

    private async Task ProcessAgendamentoReminder(IHubContext<PharmaHub> hubContext, AgendamentoReminder agendamento)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PharmazapDbContext>();

            // Buscar agendamento no banco
            var agendamentoDb = await dbContext.AgendamentosRecorrentes
                .Include(a => a.Cliente)
                .Include(a => a.Farmacia)
                .FirstOrDefaultAsync(a => a.Id == agendamento.AgendamentoId);

            if (agendamentoDb != null && agendamentoDb.Cliente != null)
            {
                // Notificar atendentes
                await hubContext.Clients.Group(Consts.SignalR.GrupoAtendentes(agendamentoDb.FarmaciaId.ToString()))
                    .SendAsync(Consts.SignalR.Metodos.AgendamentoReminder, new
                    {
                        AgendamentoId = agendamentoDb.Id,
                        ClienteNome = agendamentoDb.Cliente.Nome,
                        Medicamento = agendamentoDb.Medicamento,
                        ProximoDisparo = agendamentoDb.ProximoDisparo,
                        Timestamp = DateTime.UtcNow
                    });

                // Atualizar próximo disparo
                agendamentoDb.ProximoDisparo = agendamentoDb.ProximoDisparo.AddDays(agendamentoDb.FrequenciaDias);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Processed agendamento reminder {AgendamentoId} for cliente {ClienteId}",
                    agendamentoDb.Id, agendamentoDb.ClienteId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agendamento reminder");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message Queue Processor parando");
        _timer?.Change(Timeout.Infinite, 0);
        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessarCampanhasPendentesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var campanhaService = scope.ServiceProvider.GetRequiredService<ICampanhaService>();
            await campanhaService.ProcessarCampanhasPendentesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar campanhas pendentes [{ex.Message}]");
        }
    }
}
