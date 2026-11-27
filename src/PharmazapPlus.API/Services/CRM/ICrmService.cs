using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.API.Services.CRM;

public interface ICrmService
{
    // Clientes
    Task<Cliente> GetOrCreateClienteByWhatsApp(string farmaciaId, string whatsAppId, string? nome = null);
    Task<Cliente> GetOrCreateClienteByCanalContato(string farmaciaId, string contatoId, CanalEnum canal, string? nome = null);
    Task<Cliente> GetCliente(int clienteId, string farmaciaId);
    Task<IEnumerable<Cliente>> SearchClientes(string farmaciaId, ClienteSearchFilter filter);
    Task<Cliente> CreateCliente(Cliente cliente);
    Task<Cliente> UpdateCliente(int clienteId, ClienteUpdateDto updateDto);
    Task<(bool Success, string Message)> DeleteCliente(int clienteId, string farmaciaId);
    Task<(bool Success, string Message)> ReativarCliente(int clienteId, string farmaciaId);

    // Atendimentos
    Task<Atendimento> CreateAtendimento(Atendimento atendimento);
    Task<IEnumerable<Atendimento>> GetAtendimentosByCliente(int clienteId, string farmaciaId);
    Task<Atendimento?> GetAtendimentoAtivo(string farmaciaId, string whatsAppId);

    // Estatísticas
    Task<ClienteEstatisticas> GetEstatisticasCliente(int clienteId, string farmaciaId);
    Task<CrmDashboard> GetDashboardCrm(string farmaciaId, DateTime? startDate = null, DateTime? endDate = null);

    // Tags e Observações
    Task<ClienteTag> AddTagToCliente(int clienteId, string tag, string? cor = null, string? usuario = null);
    Task<bool> RemoveTagFromCliente(int clienteId, int tagId);
    Task<ClienteObservacao> AddObservacaoToCliente(int clienteId, ClienteObservacaoDto observacaoDto);
    Task<bool> UpdateObservacao(int observacaoId, ClienteObservacaoUpdateDto updateDto);

    // Importação/Exportação
    Task<ImportResult> ImportClientesFromCsv(string farmaciaId, Stream csvStream);
    Task<Stream> ExportClientesToCsv(string farmaciaId, ClienteExportFilter filter);
    Task<Stream> ExportClientesToExcel(string farmaciaId, ClienteExportFilter filter);

    // Segmentação
    Task<IEnumerable<ClienteSegmento>> GetSegmentosClientes(string farmaciaId);
    Task<IEnumerable<Cliente>> GetClientesBySegmento(string farmaciaId, string segmento);

    // Comunicação
    Task<bool> SendClienteNotification(int clienteId, string tipo, string mensagem, Dictionary<string, object>? dados = null);
}
