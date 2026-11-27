using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.Web.Services
{
    public class CampanhaService : ICampanhaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<CampanhaService> _logger;

        public CampanhaService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ILocalStorageService localStorage,
            ILogger<CampanhaService> logger)
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

        public async Task<List<Campanha>> GetCampanhasAsync(string? status = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/campanhas";
                if (!string.IsNullOrWhiteSpace(status))
                    url += $"?status={Uri.EscapeDataString(status)}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get campanhas. Status: {Status}. Body: {Body}", response.StatusCode, body);
                    return new List<Campanha>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                var content = await response.Content.ReadFromJsonAsync<CampanhaListResponse>(options);

                if (content?.Success == true && content.Data != null)
                    return content.Data;

                _logger.LogWarning("No campanhas data returned. Success: {Success}, Data is null: {IsNull}",
                    content?.Success, content?.Data == null);
                return new List<Campanha>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campanhas [{Message}]", ex.Message);
                return new List<Campanha>();
            }
        }

        public async Task<(bool Success, string? Message, Campanha? Data)> CreateCampanhaAsync(CreateCampanhaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/campanhas", request);
                var rawContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var content = JsonSerializer.Deserialize<CampanhaResponse>(rawContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return (true, "Campanha criada com sucesso", content?.Data);
                    }
                    catch
                    {
                        return (true, "Campanha criada com sucesso", null);
                    }
                }

                string? errorMessage = null;
                try
                {
                    var content = JsonSerializer.Deserialize<CampanhaResponse>(rawContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    errorMessage = content?.Error;
                }
                catch
                {
                }

                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = rawContent;

                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao criar campanha"}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating campanha [{Message}]", ex.Message);
                return (false, "Erro ao criar campanha", null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateCampanhaAsync(int id, UpdateCampanhaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/campanhas/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                if (response.IsSuccessStatusCode)
                    return (true, content?.Message ?? "Campanha atualizada com sucesso");

                var errorMessage = content?.Error ?? content?.Message;
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    try { errorMessage = await response.Content.ReadAsStringAsync(); } catch { }
                }
                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao atualizar campanha"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating campanha {Id}", id);
                return (false, "Erro ao atualizar campanha");
            }
        }

        public async Task<(bool Success, string Message)> DeleteCampanhaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/campanhas/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                if (response.IsSuccessStatusCode)
                    return (true, content?.Message ?? "Campanha excluída com sucesso");

                var errorMessage = content?.Error ?? content?.Message;
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    try { errorMessage = await response.Content.ReadAsStringAsync(); } catch { }
                }
                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao excluir campanha"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting campanha {Id}", id);
                return (false, "Erro ao excluir campanha");
            }
        }

        public async Task<(bool Success, string Message)> AgendarCampanhaAsync(int id, DateTime? dataAgendamento)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync($"api/campanhas/{id}/agendar",
                    new { DataAgendamento = dataAgendamento });

                var rawContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, "Campanha agendada com sucesso");

                string? errorMessage = null;
                try
                {
                    var content = JsonSerializer.Deserialize<CampanhaResponse>(rawContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    errorMessage = content?.Error;
                }
                catch
                {
                }

                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = rawContent;

                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao agendar campanha"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao agendar campanha {Id}", id);
                return (false, "Erro ao agendar campanha");
            }
        }

        public async Task<(bool Success, string Message)> CancelarCampanhaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync($"api/campanhas/{id}/cancelar", new { });

                var rawContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, "Campanha cancelada com sucesso");

                string? errorMessage = null;
                try
                {
                    var content = JsonSerializer.Deserialize<CampanhaResponse>(rawContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    errorMessage = content?.Error;
                }
                catch
                {
                }

                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = rawContent;

                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao cancelar campanha"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling campanha {Id}", id);
                return (false, "Erro ao cancelar campanha");
            }
        }

        public async Task<(bool Success, string Message)> ExecutarCampanhaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync($"api/campanhas/{id}/executar", new { });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                if (response.IsSuccessStatusCode)
                    return (true, content?.Message ?? "Campanha executada com sucesso");

                var errorMessage = content?.Error ?? content?.Message;
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    try { errorMessage = await response.Content.ReadAsStringAsync(); } catch { }
                }
                return (false, $"[{(int)response.StatusCode}] {errorMessage ?? "Erro ao executar campanha"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing campanha {Id}", id);
                return (false, "Erro ao executar campanha");
            }
        }

        private class ApiSimpleResponse
        {
            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }

        private class CampanhaListResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<Campanha>? Data { get; set; }
        }

        private class CampanhaResponse
        {
            [JsonPropertyName("data")]
            public Campanha? Data { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }
    }
}
