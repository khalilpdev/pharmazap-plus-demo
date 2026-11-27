namespace PharmazapPlus.Web.Services
{
    public interface IAtendenteService
    {
        Task<List<AtendenteDto>> GetAtendentesAsync();
        Task<(bool Success, string Message)> CreateAtendenteAsync(CreateAtendenteDto request);
        Task<(bool Success, string Message)> UpdateAtendenteAsync(int id, UpdateAtendenteDto request);
        Task<(bool Success, string Message)> InativarAtendenteAsync(int id);
        Task<(bool Success, string Message)> AtivarAtendenteAsync(int id);
        Task<(bool Success, string Message)> ResetSenhaAtendenteAsync(int id, string newPassword);
    }
}
