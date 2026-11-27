using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public class AdministradorService : IAdministradorService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<AdministradorService> _logger;

        public AdministradorService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ILocalStorageService localStorage,
            ILogger<AdministradorService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _localStorage = localStorage;
            _logger = logger;
        }

        private async Task<HttpClient> CreateAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("PharmazapApi");

            var token = _authService.CurrentUser?.Token;
            if (string.IsNullOrEmpty(token))
            {
                try
                {
                    token = await _localStorage.GetItemAsync<string>("pharmazap_token");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading token from local storage [{Message}]", ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        public async Task<List<AdministradorDto>> GetAdministradoresAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/usuarios?role=Administrador&pageSize=100");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get administradores. Status: {Status}", response.StatusCode);
                    return new List<AdministradorDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<UsuariosListResponse>();
                if (result?.Success == true && result.Data != null)
                    return result.Data;

                return new List<AdministradorDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting administradores [{Message}]", ex.Message);
                return new List<AdministradorDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateAdministradorAsync(CreateAdministradorDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/usuarios", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Administrador criado com sucesso" : "Erro ao criar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating administrador [{Message}]", ex.Message);
                return (false, "Erro ao criar administrador");
            }
        }

        public async Task<(bool Success, string Message)> UpdateAdministradorAsync(int id, UpdateAdministradorDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/usuarios/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Administrador atualizado com sucesso" : "Erro ao atualizar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating administrador {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao atualizar administrador");
            }
        }

        public async Task<(bool Success, string Message)> InativarAdministradorAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/usuarios/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Administrador inativado com sucesso" : "Erro ao inativar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inativating administrador {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao inativar administrador");
            }
        }

        public async Task<(bool Success, string Message)> AtivarAdministradorAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/usuarios/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Administrador ativado com sucesso" : "Erro ao ativar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating administrador {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao ativar administrador");
            }
        }

        public async Task<(bool Success, string Message)> ResetSenhaAdministradorAsync(int id, string newPassword)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/usuarios/{id}/reset-password", new { NewPassword = newPassword });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                var fallback = response.IsSuccessStatusCode ? "Senha redefinida com sucesso" : "Erro ao redefinir senha";
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? fallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for administrador {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao redefinir senha");
            }
        }

        private class UsuariosListResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<AdministradorDto>? Data { get; set; }
        }

        private class ApiSimpleResponse
        {
            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }
    }
}
