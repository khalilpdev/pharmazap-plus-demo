using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.API.Services.Campanhas;

public interface ICampanhaService
{
    Task<List<Campanha>> GetCampanhasAsync(int farmaciaId, CampanhaStatusEnum? status = null);
    Task<Campanha?> GetCampanhaByIdAsync(int id, int farmaciaId);
    Task<Campanha> CreateCampanhaAsync(Campanha campanha);
    Task<Campanha> UpdateCampanhaAsync(Campanha campanha);
    Task<bool> DeleteCampanhaAsync(int id, int farmaciaId);
    Task<Campanha> AgendarCampanhaAsync(int id, int farmaciaId, DateTime? dataAgendamento);
    Task<Campanha> CancelarCampanhaAsync(int id, int farmaciaId);
    Task<Campanha> ExecutarCampanhaAsync(int id, int farmaciaId);
    Task ProcessarCampanhasPendentesAsync();
}
