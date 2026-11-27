namespace PharmazapPlus.Web.Services
{
    public interface IAdministradorService
    {
        Task<List<AdministradorDto>> GetAdministradoresAsync();
        Task<(bool Success, string Message)> CreateAdministradorAsync(CreateAdministradorDto request);
        Task<(bool Success, string Message)> UpdateAdministradorAsync(int id, UpdateAdministradorDto request);
        Task<(bool Success, string Message)> InativarAdministradorAsync(int id);
        Task<(bool Success, string Message)> AtivarAdministradorAsync(int id);
        Task<(bool Success, string Message)> ResetSenhaAdministradorAsync(int id, string newPassword);
    }
}
