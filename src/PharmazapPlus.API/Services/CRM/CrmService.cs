#nullable enable

// PharmazapPlus.API/Services/CRM/CrmService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Services.CRM;

public class CrmService : ICrmService
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<CrmService> _logger;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IHubContext<PharmaHub> _hubContext;
    private readonly IClienteImporter _clienteImporter;
    private readonly IClienteExporter _clienteExporter;

    public CrmService(
        PharmazapDbContext context,
        ILogger<CrmService> logger,
        IWhatsAppService whatsAppService,
        IHubContext<PharmaHub> hubContext,
        IClienteImporter clienteImporter,
        IClienteExporter clienteExporter)
    {
        _context = context;
        _logger = logger;
        _whatsAppService = whatsAppService;
        _hubContext = hubContext;
        _clienteImporter = clienteImporter;
        _clienteExporter = clienteExporter;
    }

    public async Task<Cliente> GetOrCreateClienteByWhatsApp(string farmaciaId, string whatsAppId, string? nome = null)
        => await GetOrCreateClienteByCanalContato(farmaciaId, whatsAppId, CanalEnum.WhatsApp, nome);

    public async Task<Cliente> GetOrCreateClienteByCanalContato(string farmaciaId, string contatoId, CanalEnum canal, string? nome = null)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var normalizedContatoId = NormalizeContatoId(contatoId, canal);
            if (string.IsNullOrWhiteSpace(normalizedContatoId))
                throw new ArgumentException("Identificador de contato inválido");

            var clientesBaseQuery = _context.Clientes
                .Include(c => c.Tags)
                .Include(c => c.Agendamentos)
                .OrderBy(c => c.Id)
                .Where(c => c.FarmaciaId == farmaciaIdInt);

            List<Cliente> clientes;
            if (canal == CanalEnum.WhatsApp)
            {
                var lastEightDigits = normalizedContatoId.GetWhatsAppLastEightDigits();
                if (lastEightDigits.Length == 8)
                {
                    var clientesFiltrados = await clientesBaseQuery
                        .Where(c =>
                            c.WhatsAppId.EndsWith(lastEightDigits) ||
                            c.WhatsAppId
                                .Replace("(", "")
                                .Replace(")", "")
                                .Replace("-", "")
                                .Replace(" ", "")
                                .Replace("+", "")
                                .EndsWith(lastEightDigits))
                        .ToListAsync();

                    clientes = clientesFiltrados.Count > 0
                        ? clientesFiltrados
                        : await clientesBaseQuery.ToListAsync();
                }
                else
                {
                    clientes = await clientesBaseQuery.ToListAsync();
                }
            }
            else
            {
                clientes = await clientesBaseQuery.Where(c => c.WhatsAppId == normalizedContatoId).ToListAsync();
            }

            var cliente = clientes.FirstOrDefault(c =>
                string.Equals(
                    NormalizeContatoId(c.WhatsAppId, canal),
                    normalizedContatoId,
                    StringComparison.OrdinalIgnoreCase) ||
                (canal == CanalEnum.WhatsApp &&
                 NormalizeContatoId(c.WhatsAppId, canal).IsEquivalentWhatsAppIdentifier(normalizedContatoId)));

            if (cliente != null)
            {
                _logger.LogDebug("Cliente encontrado: {ClienteId} para contato {ContatoId} no canal {Canal}",
                    cliente.Id, normalizedContatoId, canal);
                if (canal == CanalEnum.WhatsApp)
                {
                    var whatsappNormalizadoAtual = NormalizeContatoId(cliente.WhatsAppId, canal);
                    var whatsappPreferido = whatsappNormalizadoAtual.SelectPreferredWhatsAppIdentifier(normalizedContatoId);
                    if (!string.Equals(cliente.WhatsAppId, whatsappPreferido, StringComparison.Ordinal))
                        cliente.WhatsAppId = whatsappPreferido;

                    if (string.IsNullOrWhiteSpace(cliente.Telefone))
                        cliente.Telefone = normalizedContatoId;

                    if (!string.IsNullOrWhiteSpace(nome) &&
                        (string.IsNullOrWhiteSpace(cliente.Nome) ||
                         string.Equals(cliente.Nome, whatsappNormalizadoAtual, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cliente.Nome, normalizedContatoId, StringComparison.OrdinalIgnoreCase)))
                    {
                        cliente.Nome = nome.Trim();
                    }
                }
                else if (!string.Equals(cliente.WhatsAppId, normalizedContatoId, StringComparison.Ordinal))
                {
                    cliente.WhatsAppId = normalizedContatoId;
                }
                cliente.UltimoContato = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return cliente;
            }

            // Criar novo cliente
            var nomeCliente = !string.IsNullOrWhiteSpace(nome) ? nome.Trim() : normalizedContatoId;
            cliente = new Cliente
            {
                FarmaciaId = farmaciaIdInt,
                Farmacia = null!,
                WhatsAppId = normalizedContatoId,
                Nome = nomeCliente,
                Telefone = canal == CanalEnum.WhatsApp ? normalizedContatoId : null,
                CodigoCliente = await GerarCodigoCliente(farmaciaIdInt),
                Status = ClienteStatus.Potencial,
                Tipo = ClienteTipo.PessoaFisica,
                Segmento = ClienteSegmentoEnum.Novo.ToString(),
                OrigemCadastro = Consts.ClienteDefaults.OrigemCadastro,
                DataCadastro = DateTime.UtcNow,
                UltimoContato = DateTime.UtcNow,
                CriadoPor = Consts.ClienteDefaults.CriadoPor,
                AceitaMarketing = true,
                AceitaNotificacoes = true,
                PreferenciasContato = Consts.ClienteDefaults.PreferenciasContato,
                PerfilCompra = ClientePerfilCompraEnum.Prospecto
            };

            await _context.Clientes.AddAsync(cliente);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Novo cliente criado: {ClienteId} para contato {ContatoId} no canal {Canal}",
                cliente.Id, normalizedContatoId, canal);

            // Notificar criação do cliente
            await NotificarNovoCliente(cliente);

            return cliente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar/criar cliente por contato {ContatoId} no canal {Canal}", contatoId, canal);
            throw;
        }
    }

    private static string NormalizeContatoId(string? contatoId, CanalEnum canal)
    {
        if (string.IsNullOrWhiteSpace(contatoId))
            return string.Empty;

        if (canal == CanalEnum.WhatsApp)
        {
            return contatoId.NormalizeWhatsAppIdentifier();
        }

        return contatoId.Trim();
    }

    public async Task<Cliente> GetCliente(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var cliente = await _context.Clientes
                .Include(c => c.Tags)
                .Include(c => c.Observacoes.Where(o => !o.Concluida))
                .Include(c => c.Agendamentos.Where(a => a.Ativo && a.ProximoDisparo >= DateTime.UtcNow))
                .Include(c => c.Atendimentos)
                    .ThenInclude(a => a.Mensagens.OrderByDescending(m => m.ReceivedAt).Take(5))
                .Include(c => c.Pedidos.OrderByDescending(p => p.DataPedido).Take(10))
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.FarmaciaId == farmaciaIdInt);

            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            return cliente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<IEnumerable<Cliente>> SearchClientes(string farmaciaId, ClienteSearchFilter filter)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var query = _context.Clientes
                .Where(c => c.FarmaciaId == farmaciaIdInt)
                .AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrEmpty(filter.Query))
            {
                query = query.Where(c =>
                    c.Nome.Contains(filter.Query) ||
                    (c.Sobrenome != null && c.Sobrenome.Contains(filter.Query)) ||
                    c.WhatsAppId.Contains(filter.Query) ||
                    (c.Email != null && c.Email.Contains(filter.Query)) ||
                    (c.Telefone != null && c.Telefone.Contains(filter.Query)) ||
                    (c.Cpf != null && c.Cpf.Contains(filter.Query)) ||
                    (c.CodigoCliente != null && c.CodigoCliente.Contains(filter.Query)));
            }

            if (filter.Status.HasValue)
                query = query.Where(c => c.Status == filter.Status.Value);

            if (filter.Tipo.HasValue)
                query = query.Where(c => c.Tipo == filter.Tipo.Value);

            if (!string.IsNullOrEmpty(filter.Segmento))
                query = query.Where(c => c.Segmento == filter.Segmento);

            if (filter.UsoContinuo.HasValue)
                query = query.Where(c => c.UsoContinuo == filter.UsoContinuo.Value);

            if (!string.IsNullOrEmpty(filter.Tag))
            {
                query = query.Where(c => c.Tags.Any(t => t.Tag == filter.Tag));
            }

            if (filter.DataInicio.HasValue)
                query = query.Where(c => c.DataCadastro >= filter.DataInicio.Value);

            if (filter.DataFim.HasValue)
                query = query.Where(c => c.DataCadastro <= filter.DataFim.Value);

            // Ordenação
            query = filter.OrderBy.ToLower() switch
            {
                "nome" => filter.OrderDesc ? query.OrderByDescending(c => c.Nome) : query.OrderBy(c => c.Nome),
                "datacadastro" => filter.OrderDesc ? query.OrderByDescending(c => c.DataCadastro) : query.OrderBy(c => c.DataCadastro),
                "ultimocontato" => filter.OrderDesc ? query.OrderByDescending(c => c.UltimoContato) : query.OrderBy(c => c.UltimoContato),
                "totalgasto" => filter.OrderDesc ? query.OrderByDescending(c => c.TotalGasto) : query.OrderBy(c => c.TotalGasto),
                _ => query.OrderBy(c => c.Nome)
            };

            // Paginação
            var clientes = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Include(c => c.Tags.Take(3))
                .ToListAsync();

            return clientes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar clientes com filtro");
            throw;
        }
    }

    public async Task<Cliente> CreateCliente(Cliente cliente)
    {
        try
        {
            // Validar dados obrigatórios
            if (string.IsNullOrEmpty(cliente.Nome))
                throw new ArgumentException("Nome do cliente é obrigatório");

            if (cliente.FarmaciaId <= 0)
                throw new ArgumentException("Farmácia é obrigatória");

            // Gerar código do cliente
            cliente.CodigoCliente = await GerarCodigoCliente(cliente.FarmaciaId);

            // Configurar valores padrão
            cliente.DataCadastro = DateTime.UtcNow;
            cliente.UltimoContato = DateTime.UtcNow;
            cliente.Status = ClienteStatus.Ativo;
            cliente.Segmento = "Novo";

            if (string.IsNullOrEmpty(cliente.OrigemCadastro))
                cliente.OrigemCadastro = "Manual";

            if (string.IsNullOrEmpty(cliente.CriadoPor))
                cliente.CriadoPor = "Sistema";

            await _context.Clientes.AddAsync(cliente);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente criado manualmente: {ClienteId} - {Nome}", cliente.Id, cliente.Nome);

            // Notificar criação
            await NotificarNovoCliente(cliente);

            return cliente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar cliente");
            throw;
        }
    }

    public async Task<Cliente> UpdateCliente(int clienteId, ClienteUpdateDto updateDto)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(clienteId);
            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            // Atualizar campos permitidos
            if (!string.IsNullOrEmpty(updateDto.Nome))
                cliente.Nome = updateDto.Nome;

            if (!string.IsNullOrEmpty(updateDto.Sobrenome))
                cliente.Sobrenome = updateDto.Sobrenome;

            if (!string.IsNullOrEmpty(updateDto.Email))
                cliente.Email = updateDto.Email;

            if (!string.IsNullOrEmpty(updateDto.Telefone))
                cliente.Telefone = updateDto.Telefone;

            if (!string.IsNullOrEmpty(updateDto.Cpf))
                cliente.Cpf = updateDto.Cpf;

            if (updateDto.DataNascimento.HasValue)
                cliente.DataNascimento = updateDto.DataNascimento.Value;

            if (!string.IsNullOrEmpty(updateDto.Endereco))
                cliente.Endereco = updateDto.Endereco;

            if (!string.IsNullOrEmpty(updateDto.Cidade))
                cliente.Cidade = updateDto.Cidade;

            if (!string.IsNullOrEmpty(updateDto.Estado))
                cliente.Estado = updateDto.Estado;

            if (!string.IsNullOrEmpty(updateDto.Cep))
                cliente.Cep = updateDto.Cep;

            if (updateDto.UsoContinuo.HasValue)
            {
                cliente.UsoContinuo = updateDto.UsoContinuo.Value;
                // Recalculate profile when continuous-use flag changes
                cliente.AtualizarPerfilCompra();
            }

            if (!string.IsNullOrEmpty(updateDto.MedicamentosContinuos))
                cliente.MedicamentosContinuos = updateDto.MedicamentosContinuos;

            if (!string.IsNullOrEmpty(updateDto.Alergias))
                cliente.Alergias = updateDto.Alergias;

            if (!string.IsNullOrEmpty(updateDto.ObservacoesSaude))
                cliente.ObservacoesSaude = updateDto.ObservacoesSaude;

            if (updateDto.AceitaMarketing.HasValue)
                cliente.AceitaMarketing = updateDto.AceitaMarketing.Value;

            if (updateDto.AceitaNotificacoes.HasValue)
                cliente.AceitaNotificacoes = updateDto.AceitaNotificacoes.Value;

            if (!string.IsNullOrEmpty(updateDto.PreferenciasContato))
                cliente.PreferenciasContato = updateDto.PreferenciasContato;

            if (!string.IsNullOrEmpty(updateDto.ObservacoesGerais))
                cliente.ObservacoesGerais = updateDto.ObservacoesGerais;

            if (updateDto.ClearEtiqueta)
                cliente.EtiquetaId = null;
            else if (updateDto.EtiquetaId.HasValue)
                cliente.EtiquetaId = updateDto.EtiquetaId.Value;

            if (updateDto.PerfilCompra.HasValue)
                cliente.PerfilCompra = updateDto.PerfilCompra.Value;

            if (!string.IsNullOrEmpty(updateDto.Apelido))
                cliente.Apelido = updateDto.Apelido;

            if (!string.IsNullOrEmpty(updateDto.Sexo))
                cliente.Sexo = updateDto.Sexo;

            if (!string.IsNullOrEmpty(updateDto.Numero))
                cliente.Numero = updateDto.Numero;

            if (!string.IsNullOrEmpty(updateDto.Complemento))
                cliente.Complemento = updateDto.Complemento;

            if (!string.IsNullOrEmpty(updateDto.Bairro))
                cliente.Bairro = updateDto.Bairro;

            if (updateDto.Tipo.HasValue)
                cliente.Tipo = updateDto.Tipo.Value;

            if (updateDto.Status.HasValue)
            {
                if (cliente.Status == ClienteStatus.Potencial && updateDto.Status.Value == ClienteStatus.Ativo)
                    cliente.PerfilCompra = ClientePerfilCompraEnum.Prospecto;
                if (updateDto.Status.Value == ClienteStatus.Inativo)
                    cliente.DataInativacao = DateTime.UtcNow;
                else if (cliente.Status == ClienteStatus.Inativo)
                    cliente.DataInativacao = null;
                cliente.Status = updateDto.Status.Value;
            }

            if (!string.IsNullOrEmpty(updateDto.Segmento))
                cliente.Segmento = updateDto.Segmento;

            if (!string.IsNullOrEmpty(updateDto.CrmCrf))
                cliente.CrmCrf = updateDto.CrmCrf;

            if (!string.IsNullOrEmpty(updateDto.Especialidade))
                cliente.Especialidade = updateDto.Especialidade;

            if (!string.IsNullOrEmpty(updateDto.LocalTrabalho))
                cliente.LocalTrabalho = updateDto.LocalTrabalho;

            cliente.AtualizadoEm = DateTime.UtcNow;
            cliente.AtualizadoPor = updateDto.AtualizadoPor ?? "Sistema";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente atualizado: {ClienteId}", clienteId);

            return cliente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> DeleteCliente(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                return (false, "Farmácia inválida");

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.FarmaciaId == farmaciaIdInt);

            if (cliente == null)
                return (false, "Cliente não encontrado");

            var hasActiveAtendimentos = await _context.Atendimentos
                .AnyAsync(a => a.ClienteId == clienteId && 
                              a.FarmaciaId == farmaciaIdInt &&
                              (a.StatusEnum == AtendimentoStatusEnum.Pendente || 
                               a.StatusEnum == AtendimentoStatusEnum.EmAndamento));

            if (hasActiveAtendimentos)
            {
                _logger.LogWarning("Tentativa de remover cliente com atendimentos ativos: {ClienteId}", clienteId);
                return (false, "Não é possível remover cliente com atendimentos ativos ou pendentes");
            }

            // Marcar como inativo em vez de deletar
            cliente.Status = ClienteStatus.Inativo;
            cliente.DataInativacao = DateTime.UtcNow;
            cliente.AtualizadoEm = DateTime.UtcNow;
            cliente.AtualizadoPor = "Sistema";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente marcado como inativo: {ClienteId}", clienteId);

            return (true, "Cliente removido com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar cliente {ClienteId}", clienteId);
            return (false, "Erro ao remover cliente");
        }
    }

    public async Task<(bool Success, string Message)> ReativarCliente(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                return (false, "Farmácia inválida");

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.FarmaciaId == farmaciaIdInt);

            if (cliente == null)
                return (false, "Cliente não encontrado");

            if (cliente.Status == ClienteStatus.Ativo)
                return (true, "Cliente já está ativo");

            cliente.Status = ClienteStatus.Ativo;
            cliente.Ativo = true;
            cliente.DataInativacao = null;
            cliente.AtualizadoEm = DateTime.UtcNow;
            cliente.AtualizadoPor = "Sistema";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente reativado: {ClienteId}", clienteId);

            return (true, "Cliente reativado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reativar cliente {ClienteId}", clienteId);
            return (false, "Erro ao reativar cliente");
        }
    }

    public async Task<Atendimento> CreateAtendimento(Atendimento atendimento)
    {
        try
        {
            // Validar
            if (atendimento.FarmaciaId <= 0)
                throw new ArgumentException("Farmácia é obrigatória");

            if (string.IsNullOrEmpty(atendimento.WhatsAppId))
                throw new ArgumentException("WhatsAppId é obrigatório");

            // Configurar valores padrão — CanalEnum.WhatsApp (0) é o padrão do enum, não é necessário forçar

            atendimento.DataInicio = DateTime.UtcNow;

            await _context.Atendimentos.AddAsync(atendimento);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Atendimento criado: {AtendimentoId}", atendimento.Id);

            return atendimento;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar atendimento");
            throw;
        }
    }

    public async Task<IEnumerable<Atendimento>> GetAtendimentosByCliente(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var atendimentos = await _context.Atendimentos
                .Where(a => a.ClienteId == clienteId && a.FarmaciaId == farmaciaIdInt)
                .Include(a => a.Mensagens.OrderByDescending(m => m.ReceivedAt).Take(20))
                .Include(a => a.Atendente)
                .OrderByDescending(a => a.DataInicio)
                .Take(50)
                .ToListAsync();

            return atendimentos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar atendimentos do cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<Atendimento?> GetAtendimentoAtivo(string farmaciaId, string whatsAppId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var atendimento = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmaciaIdInt &&
                           a.WhatsAppId == whatsAppId &&
                           a.Status != AtendimentoStatusEnum.Finalizado.ToString() &&
                           a.Status != "Cancelado")
                .OrderByDescending(a => a.DataInicio)
                .FirstOrDefaultAsync();

            return atendimento;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar atendimento ativo para WhatsApp {WhatsAppId}", whatsAppId);
            throw;
        }
    }

    public async Task<ClienteEstatisticas> GetEstatisticasCliente(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var cliente = await _context.Clientes
                .Include(c => c.Atendimentos)
                .Include(c => c.Pedidos)
                .Include(c => c.Tags)
                .Include(c => c.Observacoes)
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.FarmaciaId == farmaciaIdInt);

            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            var estatisticas = new ClienteEstatisticas
            {
                TotalAtendimentos = cliente.TotalAtendimentos,
                TotalPedidos = cliente.TotalPedidos,
                TotalGasto = cliente.TotalGasto,
                TicketMedio = cliente.TicketMedio,
                UltimaCompra = cliente.UltimaCompra,
                UltimoContato = cliente.UltimoContato,
                Tags = cliente.Tags.Select(t => t.Tag).ToList(),
                ObservacoesPendentes = cliente.Observacoes
                    .Where(o => !o.Concluida)
                    .OrderByDescending(o => o.DataCriacao)
                    .Take(5)
                    .ToList()
            };

            // Calcular atendimentos por mês (últimos 6 meses)
            var seisMesesAtras = DateTime.UtcNow.AddMonths(-6);
            var atendimentosPorMes = cliente.Atendimentos
                .Where(a => a.DataInicio >= seisMesesAtras)
                .GroupBy(a => new { a.DataInicio.Year, a.DataInicio.Month })
                .Select(g => new
                {
                    Periodo = $"{g.Key.Month:00}/{g.Key.Year}",
                    Quantidade = g.Count()
                })
                .ToDictionary(x => x.Periodo, x => x.Quantidade);

            estatisticas.AtendimentosPorMes = atendimentosPorMes;

            // Calcular compras por mês (últimos 6 meses)
            var comprasPorMes = cliente.Pedidos
                .Where(p => p.DataPedido >= seisMesesAtras)
                .GroupBy(p => new { p.DataPedido.Year, p.DataPedido.Month })
                .Select(g => new
                {
                    Periodo = $"{g.Key.Month:00}/{g.Key.Year}",
                    Total = g.Sum(p => p.ValorFinal)
                })
                .ToDictionary(x => x.Periodo, x => x.Total);

            estatisticas.ComprasPorMes = comprasPorMes;

            return estatisticas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar estatísticas do cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<CrmDashboard> GetDashboardCrm(string farmaciaId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            startDate ??= DateTime.UtcNow.AddMonths(-1);
            endDate ??= DateTime.UtcNow;

            var dashboard = new CrmDashboard();

            // Estatísticas básicas
            var clientesQuery = _context.Clientes.Where(c => c.FarmaciaId == farmaciaIdInt);

            dashboard.TotalClientes = await clientesQuery.CountAsync();
            dashboard.NovosClientes30Dias = await clientesQuery
                .CountAsync(c => c.DataCadastro >= DateTime.UtcNow.AddDays(-30));
            dashboard.ClientesAtivos = await clientesQuery
                .CountAsync(c => c.Status == ClienteStatus.Ativo);
            dashboard.ClientesInativos = await clientesQuery
                .CountAsync(c => c.Status == ClienteStatus.Inativo);

            // Receita
            var pedidosQuery = _context.Pedidos.Where(p => p.FarmaciaId == farmaciaIdInt);
            dashboard.ReceitaTotal = await pedidosQuery.SumAsync(p => p.ValorFinal);
            dashboard.ReceitaMediaPorCliente = dashboard.TotalClientes > 0 ?
                dashboard.ReceitaTotal / dashboard.TotalClientes : 0;

            // Taxa de retenção (clientes com pelo menos 2 compras)
            var clientesComMaisDeUmaCompra = await clientesQuery
                .CountAsync(c => c.TotalPedidos >= 2);
            dashboard.TaxaRetencao = dashboard.TotalClientes > 0 ?
                (decimal)clientesComMaisDeUmaCompra / dashboard.TotalClientes * 100 : 0;

            // Agrupamentos
            dashboard.ClientesPorSegmento = await clientesQuery
                .GroupBy(c => c.Segmento)
                .Select(g => new { Segmento = g.Key ?? "Sem Segmento", Quantidade = g.Count() })
                .ToDictionaryAsync(x => x.Segmento, x => x.Quantidade);

            dashboard.ClientesPorStatus = await clientesQuery
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key.ToString(), Quantidade = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Quantidade);

            dashboard.ClientesPorTipo = await clientesQuery
                .GroupBy(c => c.Tipo)
                .Select(g => new { Tipo = g.Key.ToString(), Quantidade = g.Count() })
                .ToDictionaryAsync(x => x.Tipo, x => x.Quantidade);

            // Top clientes
            dashboard.TopClientes = await clientesQuery
                .Where(c => c.TotalPedidos > 0)
                .OrderByDescending(c => c.TotalGasto)
                .Take(10)
                .Select(c => new TopCliente
                {
                    ClienteId = c.Id,
                    Nome = c.NomeCompleto,
                    TotalGasto = c.TotalGasto,
                    TotalPedidos = c.TotalPedidos,
                    Segmento = c.Segmento ?? string.Empty
                })
                .ToListAsync();

            // Clientes recentes
            dashboard.ClientesRecentes = await clientesQuery
                .OrderByDescending(c => c.DataCadastro)
                .Take(10)
                .Select(c => new ClienteRecente
                {
                    ClienteId = c.Id,
                    Nome = c.NomeCompleto,
                    DataCadastro = c.DataCadastro,
                    OrigemCadastro = c.OrigemCadastro ?? string.Empty,
                    TotalAtendimentos = c.TotalAtendimentos
                })
                .ToListAsync();

            // Clientes inativos (sem contato há mais de 60 dias)
            var sessentaDiasAtras = DateTime.UtcNow.AddDays(-60);
            dashboard.ClientesInativosRecentes = await clientesQuery
                .Where(c => c.UltimoContato < sessentaDiasAtras && c.Status == ClienteStatus.Ativo)
                .OrderBy(c => c.UltimoContato)
                .Take(10)
                .Select(c => new ClienteInativo
                {
                    ClienteId = c.Id,
                    Nome = c.NomeCompleto,
                    UltimoContato = c.UltimoContato,
                    DiasInativo = (int)(DateTime.UtcNow - (c.UltimoContato ?? c.DataCadastro)).TotalDays,
                    TotalGastoHistorico = c.TotalGasto
                })
                .ToListAsync();

            // Timeline - novos clientes por dia (últimos 15 dias)
            var quinzeDiasAtras = DateTime.UtcNow.AddDays(-15);
            dashboard.NovosClientesTimeline = await clientesQuery
                .Where(c => c.DataCadastro >= quinzeDiasAtras)
                .GroupBy(c => c.DataCadastro.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Data = g.Key, Quantidade = g.Count() })
                .ToDictionaryAsync(x => x.Data, x => x.Quantidade);

            // Timeline - receita por dia (últimos 15 dias)
            dashboard.ReceitaTimeline = await pedidosQuery
                .Where(p => p.DataPedido >= quinzeDiasAtras)
                .GroupBy(p => p.DataPedido.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Data = g.Key, Receita = g.Sum(p => p.ValorFinal) })
                .ToDictionaryAsync(x => x.Data, x => x.Receita);

            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar dashboard CRM para farmácia {FarmaciaId}", farmaciaId);
            throw;
        }
    }

    public async Task<ClienteTag> AddTagToCliente(int clienteId, string tag, string? cor = null, string? usuario = null)
    {
        try
        {
            var cliente = await _context.Clientes
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == clienteId);

            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            // Verificar se tag já existe
            var existingTag = cliente.Tags.FirstOrDefault(t => t.Tag == tag);
            if (existingTag != null)
                return existingTag;

            // Criar nova tag
            var newTag = new ClienteTag
            {
                ClienteId = clienteId,
                Cliente = null!,
                Tag = tag,
                Cor = cor ?? GetTagColor(tag),
                DataAdicao = DateTime.UtcNow,
                AdicionadoPor = usuario ?? "Sistema"
            };

            cliente.Tags.Add(newTag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag '{Tag}' adicionada ao cliente {ClienteId}", tag, clienteId);

            return newTag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar tag ao cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<bool> RemoveTagFromCliente(int clienteId, int tagId)
    {
        try
        {
            var tag = await _context.ClienteTags
                .FirstOrDefaultAsync(t => t.Id == tagId && t.ClienteId == clienteId);

            if (tag == null)
                return false;

            _context.ClienteTags.Remove(tag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag removida do cliente {ClienteId}", clienteId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover tag do cliente {ClienteId}", clienteId);
            return false;
        }
    }

    public async Task<ClienteObservacao> AddObservacaoToCliente(int clienteId, ClienteObservacaoDto observacaoDto)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(clienteId);
            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            var observacao = new ClienteObservacao
            {
                ClienteId = clienteId,
                Cliente = null!,
                Titulo = observacaoDto.Titulo,
                Descricao = observacaoDto.Descricao,
                Tipo = observacaoDto.Tipo ?? "Geral",
                Prioridade = observacaoDto.Prioridade ?? "Media",
                Responsavel = observacaoDto.Responsavel,
                DataCriacao = DateTime.UtcNow,
                Concluida = false,
                CriadoPor = !string.IsNullOrWhiteSpace(observacaoDto.Responsavel) ? observacaoDto.Responsavel : "Sistema"
            };

            await _context.ClienteObservacoes.AddAsync(observacao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Observação adicionada ao cliente {ClienteId}", clienteId);

            return observacao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar observação ao cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<bool> UpdateObservacao(int observacaoId, ClienteObservacaoUpdateDto updateDto)
    {
        try
        {
            var observacao = await _context.ClienteObservacoes.FindAsync(observacaoId);
            if (observacao == null)
                return false;

            if (!string.IsNullOrEmpty(updateDto.Descricao))
                observacao.Descricao = updateDto.Descricao;

            if (!string.IsNullOrEmpty(updateDto.Prioridade))
                observacao.Prioridade = updateDto.Prioridade;

            if (updateDto.Concluida.HasValue)
            {
                observacao.Concluida = updateDto.Concluida.Value;
                observacao.DataConclusao = updateDto.Concluida.Value ? DateTime.UtcNow : null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Observação {ObservacaoId} atualizada", observacaoId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar observação {ObservacaoId}", observacaoId);
            return false;
        }
    }

    public async Task<ImportResult> ImportClientesFromCsv(string farmaciaId, Stream csvStream)
    {
        try
        {
            return await _clienteImporter.ImportFromCsvAsync(farmaciaId, csvStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar clientes do CSV");
            throw;
        }
    }

    public async Task<Stream> ExportClientesToCsv(string farmaciaId, ClienteExportFilter filter)
    {
        try
        {
            return await _clienteExporter.ExportToCsvAsync(farmaciaId, filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar clientes para CSV");
            throw;
        }
    }

    public async Task<Stream> ExportClientesToExcel(string farmaciaId, ClienteExportFilter filter)
    {
        try
        {
            return await _clienteExporter.ExportToExcelAsync(farmaciaId, filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar clientes para Excel");
            throw;
        }
    }

    public async Task<IEnumerable<ClienteSegmento>> GetSegmentosClientes(string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var segmentos = await _context.Clientes
                .Where(c => c.FarmaciaId == farmaciaIdInt && c.Status == ClienteStatus.Ativo)
                .GroupBy(c => c.Segmento ?? "Sem Segmento")
                .Select(g => new ClienteSegmento
                {
                    Nome = g.Key,
                    Quantidade = g.Count(),
                    ReceitaTotal = g.Sum(c => c.TotalGasto),
                    ReceitaMedia = g.Average(c => c.TotalGasto)
                })
                .OrderByDescending(s => s.ReceitaTotal)
                .ToListAsync();

            return segmentos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar segmentos de clientes");
            throw;
        }
    }

    public async Task<IEnumerable<Cliente>> GetClientesBySegmento(string farmaciaId, string segmento)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var clientes = await _context.Clientes
                .Where(c => c.FarmaciaId == farmaciaIdInt &&
                           c.Segmento == segmento &&
                           c.Status == ClienteStatus.Ativo)
                .Include(c => c.Tags)
                .OrderByDescending(c => c.TotalGasto)
                .Take(100)
                .ToListAsync();

            return clientes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar clientes por segmento {Segmento}", segmento);
            throw;
        }
    }

    public async Task<bool> SendClienteNotification(int clienteId, string tipo, string mensagem, Dictionary<string, object>? dados = null)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(clienteId);
            if (cliente == null)
                return false;

            if (!cliente.AceitaNotificacoes)
            {
                _logger.LogWarning("Cliente {ClienteId} não aceita notificações", clienteId);
                return false;
            }

            // Enviar notificação via WhatsApp
            var response = await _whatsAppService.SendTextMessage(
                cliente.FarmaciaId.ToString(),
                cliente.WhatsAppId,
                mensagem);

            if (response.Success)
            {
                _logger.LogInformation("Notificação enviada para cliente {ClienteId} via WhatsApp", clienteId);
                return true;
            }
            else
            {
                _logger.LogError("Erro ao enviar notificação WhatsApp para cliente {ClienteId}: {Error}",
                    clienteId, response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar notificação para cliente {ClienteId}", clienteId);
            return false;
        }
    }

    // Métodos auxiliares privados
    private async Task<string> GerarCodigoCliente(int farmaciaId)
    {
        var ultimoCodigo = await _context.Clientes
            .Where(c => c.FarmaciaId == farmaciaId && c.CodigoCliente != null)
            .OrderByDescending(c => c.CodigoCliente)
            .Select(c => c.CodigoCliente)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(ultimoCodigo))
            return "CL-001";

        var numero = int.Parse(ultimoCodigo.Split('-')[1]);
        return $"CL-{(numero + 1):000}";
    }

    private async Task NotificarNovoCliente(Cliente cliente)
    {
        try
        {
            await _hubContext.Clients.Group($"farmacia-{cliente.FarmaciaId}")
                .SendAsync("NovoClienteCadastrado", new
                {
                    ClienteId = cliente.Id,
                    Nome = cliente.NomeCompleto,
                    WhatsAppId = cliente.WhatsAppId,
                    OrigemCadastro = cliente.OrigemCadastro,
                    DataCadastro = cliente.DataCadastro,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar novo cliente");
        }
    }

    private string GetTagColor(string tag)
    {
        var tagLower = tag.ToLower();

        return tagLower switch
        {
            var t when t.Contains("diabet") => "#d13438",
            var t when t.Contains("hipertens") => "#0078d4",
            var t when t.Contains("gestant") => "#e3008c",
            var t when t.Contains("idos") => "#8661c5",
            var t when t.Contains("alerg") => "#ff8c00",
            var t when t.Contains("crônic") => "#107c10",
            var t when t.Contains("vip") => "#e6b700",
            var t when t.Contains("fidel") => "#0099bc",
            _ => GetRandomColor(tag)
        };
    }

    private string GetRandomColor(string seed)
    {
        var colors = new[]
        {
            "#0078d4", "#107c10", "#e3008c", "#d13438",
            "#ff8c00", "#8661c5", "#0099bc", "#e6b700",
            "#744da9", "#038387", "#4f6bed", "#c239b3"
        };

        var hash = seed.GetHashCode();
        var index = Math.Abs(hash) % colors.Length;
        return colors[index];
    }
}
