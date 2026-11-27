using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Campanhas;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.Campanhas;

public class CampanhaService : ICampanhaService
{
    private readonly PharmazapDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<CampanhaService> _logger;

    public CampanhaService(
        PharmazapDbContext context,
        IWhatsAppService whatsAppService,
        ILogger<CampanhaService> logger)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    public async Task<List<Campanha>> GetCampanhasAsync(int farmaciaId, CampanhaStatusEnum? status = null)
    {
        var query = _context.Campanhas
            .AsNoTracking()
            .Where(c => c.FarmaciaId == farmaciaId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task<Campanha?> GetCampanhaByIdAsync(int id, int farmaciaId)
    {
        return await _context.Campanhas
            .FirstOrDefaultAsync(c => c.Id == id && c.FarmaciaId == farmaciaId);
    }

    public async Task<Campanha> CreateCampanhaAsync(Campanha campanha)
    {
        campanha.Status = CampanhaStatusEnum.Rascunho;
        campanha.DataCriacao = DateTime.UtcNow;
        campanha.TotalDestinatarios = 0;
        campanha.TotalEnviados = 0;
        campanha.TotalErros = 0;

        _context.Campanhas.Add(campanha);
        await _context.SaveChangesAsync();
        return campanha;
    }

    public async Task<Campanha> UpdateCampanhaAsync(Campanha campanha)
    {
        campanha.AtualizadoEm = DateTime.UtcNow;
        _context.Campanhas.Update(campanha);
        await _context.SaveChangesAsync();
        return campanha;
    }

    public async Task<bool> DeleteCampanhaAsync(int id, int farmaciaId)
    {
        var campanha = await _context.Campanhas
            .FirstOrDefaultAsync(c => c.Id == id && c.FarmaciaId == farmaciaId);

        if (campanha == null)
            return false;

        if (campanha.Status == CampanhaStatusEnum.Executando)
            throw new InvalidOperationException("Não é possível excluir uma campanha em execução.");

        _context.Campanhas.Remove(campanha);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Campanha> AgendarCampanhaAsync(int id, int farmaciaId, DateTime? dataAgendamento)
    {
        var campanha = await _context.Campanhas
            .FirstOrDefaultAsync(c => c.Id == id && c.FarmaciaId == farmaciaId)
            ?? throw new KeyNotFoundException($"Campanha {id} não encontrada.");

        if (campanha.Status != CampanhaStatusEnum.Rascunho)
            throw new InvalidOperationException("Somente campanhas em rascunho podem ser agendadas.");

        campanha.DataAgendamento = dataAgendamento;
        campanha.Status = CampanhaStatusEnum.Agendada;
        campanha.AtualizadoEm = DateTime.UtcNow;

        // Pré-calcular destinatários
        campanha.TotalDestinatarios = await ContarDestinatariosAsync(campanha);

        await _context.SaveChangesAsync();
        return campanha;
    }

    public async Task<Campanha> CancelarCampanhaAsync(int id, int farmaciaId)
    {
        var campanha = await _context.Campanhas
            .FirstOrDefaultAsync(c => c.Id == id && c.FarmaciaId == farmaciaId)
            ?? throw new KeyNotFoundException($"Campanha {id} não encontrada.");

        if (campanha.Status == CampanhaStatusEnum.Concluida || campanha.Status == CampanhaStatusEnum.Cancelada)
            throw new InvalidOperationException("Campanha já foi concluída ou cancelada.");

        campanha.Status = CampanhaStatusEnum.Cancelada;
        campanha.AtualizadoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return campanha;
    }

    public async Task<Campanha> ExecutarCampanhaAsync(int id, int farmaciaId)
    {
        var campanha = await _context.Campanhas
            .FirstOrDefaultAsync(c => c.Id == id && c.FarmaciaId == farmaciaId)
            ?? throw new KeyNotFoundException($"Campanha {id} não encontrada.");

        if (campanha.Status != CampanhaStatusEnum.Agendada && campanha.Status != CampanhaStatusEnum.Rascunho)
            throw new InvalidOperationException("Somente campanhas em rascunho ou agendadas podem ser executadas.");

        await EnviarCampanhaAsync(campanha);

        await _context.SaveChangesAsync();
        return campanha;
    }

    public async Task ProcessarCampanhasPendentesAsync()
    {
        var campanhasPendentes = await _context.Campanhas
            .Where(c => c.Status == CampanhaStatusEnum.Agendada &&
                        (c.DataAgendamento == null || c.DataAgendamento <= DateTime.UtcNow))
            .ToListAsync();

        foreach (var campanha in campanhasPendentes)
        {
            try
            {
                _logger.LogInformation("Processando campanha agendada {Id}: {Nome}", campanha.Id, campanha.Nome);
                await EnviarCampanhaAsync(campanha);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar campanha {Id}", campanha.Id);
                campanha.Status = CampanhaStatusEnum.ComErro;
                campanha.Observacoes = $"Erro no processamento automático: {ex.Message}";
                campanha.AtualizadoEm = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    // ── Internal ────────────────────────────────────────────────────────────

    private async Task EnviarCampanhaAsync(Campanha campanha)
    {
        campanha.Status = CampanhaStatusEnum.Executando;
        campanha.DataExecucao = DateTime.UtcNow;
        campanha.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var clientes = await ObterDestinatariosAsync(campanha);
        campanha.TotalDestinatarios = clientes.Count;
        campanha.TotalEnviados = 0;
        campanha.TotalErros = 0;

        foreach (var cliente in clientes)
        {
            try
            {
                // Use the client's preferred channel — currently WhatsApp is the primary channel.
                // Future: check CanalEnum on last atendimento to route to Instagram, Telegram, etc.
                var whatsAppId = cliente.WhatsAppId;
                if (string.IsNullOrWhiteSpace(whatsAppId))
                {
                    _logger.LogWarning("Cliente {Id} sem WhatsAppId — pulando.", cliente.Id);
                    campanha.TotalErros++;
                    continue;
                }

                var result = await _whatsAppService.SendTextMessage(
                    campanha.FarmaciaId.ToString(),
                    whatsAppId,
                    campanha.Mensagem);

                if (result.Success)
                    campanha.TotalEnviados++;
                else
                {
                    campanha.TotalErros++;
                    _logger.LogWarning("Falha ao enviar para cliente {Id}: {Error}", cliente.Id, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                campanha.TotalErros++;
                _logger.LogError(ex, "Erro ao enviar mensagem para cliente {Id}", cliente.Id);
            }
        }

        campanha.Status = campanha.TotalErros > 0 && campanha.TotalEnviados == 0
            ? CampanhaStatusEnum.ComErro
            : CampanhaStatusEnum.Concluida;
        campanha.DataConclusao = DateTime.UtcNow;
        campanha.AtualizadoEm = DateTime.UtcNow;
    }

    private async Task<List<Cliente>> ObterDestinatariosAsync(Campanha campanha)
    {
        var query = _context.Clientes
            .Where(c => c.FarmaciaId == campanha.FarmaciaId && c.Ativo);

        if (campanha.EnviarParaAceitaMarketing)
            query = query.Where(c => c.AceitaMarketing);

        if (campanha.EnviarParaUsoContinuo && !campanha.EnviarParaTodosAtivos)
            query = query.Where(c => c.UsoContinuo ||
                (c.MedicamentosContinuos != null && c.MedicamentosContinuos != "[]" && c.MedicamentosContinuos != ""));

        var etiquetaIds = campanha.EtiquetaIds;
        if (etiquetaIds.Count > 0)
            // Filter clients that have ANY of the selected etiquetas (OR logic)
            query = query.Where(c => c.EtiquetaId.HasValue && etiquetaIds.Contains(c.EtiquetaId.Value));

        return await query.ToListAsync();
    }

    private async Task<int> ContarDestinatariosAsync(Campanha campanha)
    {
        var clientes = await ObterDestinatariosAsync(campanha);
        return clientes.Count;
    }
}
