using PharmazapPlus.Shared.Models;

namespace PharmazapPlus.Web.Services
{
    public interface IAuthService
    {
        // Membros existentes
        Task<bool> Login(string email, string password);
        Task Logout();
        Task<UserInfo?> GetUserInfo();
        bool IsAuthenticated { get; }

        UserInfo? CurrentUser { get; }  // Propriedade
        Task Initialize();              // Método
        bool HasRole(params string[] roles);
        Task UpdateTheme(string theme);
        Task UpdateAccentColor(string? accentColor);

        /// <summary>
        /// Registra o aceite dos Termos de Uso e Política de Privacidade (LGPD) pelo administrador da farmácia.
        /// </summary>
        Task<bool> AceitarTermos();

        /// <summary>
        /// Atualiza a FarmaciaLogoUrl do usuário atual em memória e no localStorage.
        /// Chamado após upload/atualização de logo para que o NavMenu reflita a nova logo imediatamente.
        /// </summary>
        Task UpdateFarmaciaLogoUrl(string? logoUrl);

        /// <summary>
        /// Disparado sempre que FarmaciaLogoUrl do usuário atual é atualizada (upload ou remoção).
        /// Componentes como NavMenu assinam este evento para re-renderizar imediatamente.
        /// </summary>
        event Action? OnFarmaciaLogoUrlChanged;
    }
}
