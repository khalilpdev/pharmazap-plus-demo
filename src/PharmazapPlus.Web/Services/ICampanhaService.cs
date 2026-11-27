using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.Web.Services
{
    public interface ICampanhaService
    {
        Task<List<Campanha>> GetCampanhasAsync(string? status = null);
        Task<(bool Success, string? Message, Campanha? Data)> CreateCampanhaAsync(CreateCampanhaDto request);
        Task<(bool Success, string Message)> UpdateCampanhaAsync(int id, UpdateCampanhaDto request);
        Task<(bool Success, string Message)> DeleteCampanhaAsync(int id);
        Task<(bool Success, string Message)> AgendarCampanhaAsync(int id, DateTime? dataAgendamento);
        Task<(bool Success, string Message)> CancelarCampanhaAsync(int id);
        Task<(bool Success, string Message)> ExecutarCampanhaAsync(int id);
    }
}
