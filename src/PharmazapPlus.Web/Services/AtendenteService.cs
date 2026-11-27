using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public class AtendenteService : IAtendenteService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<AtendenteService> _logger;

        public AtendenteService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ILocalStorageService localStorage,
            ILogger<AtendenteService> logger)
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

        public async Task<List<AtendenteDto>> GetAtendentesAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/atendentes");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get atendentes. Status: {Status}", response.StatusCode);
                    throw new HttpRequestException($"Falha ao buscar atendentes. Status: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<AtendenteListResponse>();
                if (result?.Success == true && result.Data != null)
                    return result.Data;

                return new List<AtendenteDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting atendentes [{Message}]", ex.Message);
                return new List<AtendenteDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateAtendenteAsync(CreateAtendenteDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/atendentes", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Atendente criado com sucesso" : "Erro ao criar atendente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating atendente [{Message}]", ex.Message);
                return (false, "Erro ao criar atendente");
            }
        }

        public async Task<(bool Success, string Message)> UpdateAtendenteAsync(int id, UpdateAtendenteDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/atendentes/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Atendente atualizado com sucesso" : "Erro ao atualizar atendente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating atendente {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao atualizar atendente");
            }
        }

        public async Task<(bool Success, string Message)> InativarAtendenteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/atendentes/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Atendente inativado com sucesso" : "Erro ao inativar atendente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inativating atendente {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao inativar atendente");
            }
        }

        public async Task<(bool Success, string Message)> AtivarAtendenteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/atendentes/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Atendente ativado com sucesso" : "Erro ao ativar atendente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating atendente {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao ativar atendente");
            }
        }

        public async Task<(bool Success, string Message)> ResetSenhaAtendenteAsync(int id, string newPassword)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/atendentes/{id}/reset-senha", new { NewPassword = newPassword });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                var fallback = response.IsSuccessStatusCode ? "Senha redefinida com sucesso" : "Erro ao redefinir senha";
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? fallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for atendente {Id} [{Message}]", id, ex.Message);
                return (false, "Erro ao redefinir senha");
            }
        }

        private class AtendenteListResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<AtendenteDto>? Data { get; set; }
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
