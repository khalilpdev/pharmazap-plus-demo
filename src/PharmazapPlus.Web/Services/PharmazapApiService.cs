using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IToastService = Microsoft.FluentUI.AspNetCore.Components.IToastService;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Implementação do serviço de chamadas autenticadas à API PharmazapPlus.
    /// Usa o named HttpClient "PharmazapApi" configurado em Program.cs.
    /// </summary>
    public class PharmazapApiService : IPharmazapApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<PharmazapApiService> _logger;
        private readonly IToastService _toastService;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        private const int ClientesPageSize = 200;

        public PharmazapApiService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ILocalStorageService localStorage,
            ILogger<PharmazapApiService> logger,
            IToastService toastService)
        {
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _localStorage = localStorage;
            _logger = logger;
            _toastService = toastService;
        }

        private async Task<HttpClient> CreateAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("PharmazapApi");

            // Prefer token from current user
            var token = _authService.CurrentUser?.Token;

            // Fallback to local storage token if auth service not initialized yet
            if (string.IsNullOrEmpty(token))
            {
                try
                {
                    var stored = await _localStorage.GetItemAsync<string>("pharmazap_token");
                    token = stored;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Error reading token from local storage [{ex.Message}]");
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        public async Task<DashboardStats?> GetDashboardStatsAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/account/dashboard-stats");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get dashboard stats. Status: {Status}", response.StatusCode);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<DashboardStatsResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting dashboard stats [{ex.Message}]");
                return null;
            }
        }

        public async Task<List<Atendimento>> GetAtendimentosAsync(string? status = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/atendimentos";
                if (!string.IsNullOrEmpty(status))
                    url += $"?status={Uri.EscapeDataString(status)}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to get atendimentos. Status: {Status}, Body: {Body}", response.StatusCode, errorContent);
                    return new List<Atendimento>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API Response: {Response}", jsonContent);

                // Create JsonSerializerOptions to handle camelCase
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var result = JsonSerializer.Deserialize<AtendimentoListResponse>(jsonContent, options);
                _logger.LogDebug("Deserialized result: Success={Success}, Total={Total}, Data count={DataCount}", 
                    result?.Success, result?.Total, result?.Data?.Count ?? 0);

                if (result?.Success == true && result.Data != null)
                {
                    var atendimentos = result.Data.Select(dto => dto.ToAtendimento()).ToList();
                    _logger.LogInformation("Successfully loaded {Count} atendimentos", atendimentos.Count);
                    return atendimentos;
                }

                _logger.LogWarning("API returned success=false or null data");
                return new List<Atendimento>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting atendimentos [{ex.Message}]");
                return new List<Atendimento>();
            }
        }

        public async Task<bool> AssumirAtendimentoAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsync($"api/atendimentos/{id}/assumir", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assuming atendimento {id} [{ex.Message}]");
                return false;
            }
        }

        public async Task<bool> VoltarPendenteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsync($"api/atendimentos/{id}/voltar-pendente", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error returning atendimento {id} to pending [{ex.Message}]");
                return false;
            }
        }

        public async Task<List<Cliente>> GetClientesAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var page = 1;
                var clientes = new List<Cliente>();
                var hasMorePages = true;

                _logger.LogInformation("🟡 GetClientesAsync - Authorization Header: {HasAuth}",
                    client.DefaultRequestHeaders.Authorization != null ? "YES" : "NO");

                while (hasMorePages)
                {
                    var url = $"api/clientes?page={page}&pageSize={ClientesPageSize}";
                    _logger.LogInformation("🟡 GetClientesAsync - About to call URL: {Url}", url);

                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("🔴 GetClientesAsync FAILED - Status: {Status}, Reason: {Reason}, Body: {Body}",
                            response.StatusCode, response.ReasonPhrase, errorContent);
                        if (page > 1)
                            _logger.LogWarning("⚠️ GetClientesAsync returning partial client list with {Count} records after page {Page} failure.", clientes.Count, page);
                        return page == 1 ? new List<Cliente>() : clientes;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("🟢 GetClientesAsync - Response 200 received. Content length: {Length}",
                        responseContent?.Length ?? 0);

                    ClienteListResponse? result;
                    try
                    {
                        result = System.Text.Json.JsonSerializer.Deserialize<ClienteListResponse>(
                            responseContent ?? string.Empty,
                            _jsonOptions);
                    }
                    catch (Exception deserializeEx)
                    {
                        _logger.LogError(deserializeEx, "🔴 Deserialization error. Raw response: {Response}", responseContent);
                        throw;
                    }

                    if (result?.Success != true || result.Data == null || result.Data.Count == 0)
                    {
                        hasMorePages = false;
                        continue;
                    }

                    clientes.AddRange(result.Data);
                    hasMorePages = result.Data.Count >= ClientesPageSize;
                    page++;
                }

                _logger.LogInformation("🟢 Clientes retrieved successfully: {Count}", clientes.Count);
                return clientes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔴 Error getting clientes [{Message}]", ex.Message);
                return new List<Cliente>();
            }
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
                _logger.LogError(ex, $"Error getting atendentes [{ex.Message}]");
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
                _logger.LogError(ex, $"Error creating atendente [{ex.Message}]");
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
                _logger.LogError(ex, $"Error updating atendente {id} [{ex.Message}]");
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
                _logger.LogError(ex, $"Error inativating atendente {id} [{ex.Message}]");
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
                _logger.LogError(ex, $"Error activating atendente {id} [{ex.Message}]");
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
                _logger.LogError(ex, $"Error resetting password for atendente {id} [{ex.Message}]");
                return (false, "Erro ao redefinir senha");
            }
        }

        public async Task<(bool Success, string Message)> CreateClienteAsync(CreateClienteDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/clientes", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Cliente criado com sucesso" : "Erro ao criar cliente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating cliente [{ex.Message}]");
                return (false, "Erro ao criar cliente");
            }
        }

        public async Task<(bool Success, string Message)> UpdateClienteAsync(int id, UpdateClienteDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/clientes/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Cliente atualizado com sucesso" : "Erro ao atualizar cliente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating cliente {id} [{ex.Message}]");
                return (false, "Erro ao atualizar cliente");
            }
        }

        public async Task<(bool Success, string Message)> DeleteClienteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/clientes/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Cliente inativado com sucesso" : "Erro ao inativar cliente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting cliente {id} [{ex.Message}]");
                return (false, "Erro ao inativar cliente");
            }
        }

        public async Task<(bool Success, string Message)> ReativarClienteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsync($"api/clientes/{id}/reativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode ? "Cliente reativado com sucesso" : "Erro ao reativar cliente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reactivating cliente {id} [{ex.Message}]");
                return (false, $"Erro ao reativar cliente - {ex.Message}");
            }
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
                return content?.Success == true && content.Data != null ? content.Data : new List<Campanha>();
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
                    errorMessage = "Erro ao criar campanha. Tente novamente.";

                return (false, $"[{(int)response.StatusCode}] {errorMessage}", null);
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
                var response = await client.PostAsJsonAsync($"api/campanhas/{id}/agendar", new { DataAgendamento = dataAgendamento });
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

        public async Task<List<MotivoEncerramentoDto>> GetMotivosEncerramentoAsync(bool? ativo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/motivos-encerramento";
                if (ativo.HasValue)
                    url += $"?ativo={ativo.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get motivos encerramento. Status: {Status}", response.StatusCode);
                    return new List<MotivoEncerramentoDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<MotivoEncerramentoListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<MotivoEncerramentoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting motivos encerramento [{ex.Message}]");
                return new List<MotivoEncerramentoDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateMotivoEncerramentoAsync(CreateMotivoEncerramentoDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/motivos-encerramento", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Motivo de encerramento criado com sucesso"
                        : "Erro ao criar motivo de encerramento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating motivo encerramento [{ex.Message}]");
                return (false, "Erro ao criar motivo de encerramento");
            }
        }

        public async Task<(bool Success, string Message)> UpdateMotivoEncerramentoAsync(int id, UpdateMotivoEncerramentoDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/motivos-encerramento/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Motivo de encerramento atualizado com sucesso"
                        : "Erro ao atualizar motivo de encerramento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating motivo encerramento {id} [{ex.Message}]");
                return (false, "Erro ao atualizar motivo de encerramento");
            }
        }

        public async Task<(bool Success, string Message)> InativarMotivoEncerramentoAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/motivos-encerramento/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Motivo de encerramento inativado com sucesso"
                        : "Erro ao inativar motivo de encerramento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inactivating motivo encerramento {id} [{ex.Message}]");
                return (false, "Erro ao inativar motivo de encerramento");
            }
        }

        public async Task<(bool Success, string Message)> AtivarMotivoEncerramentoAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/motivos-encerramento/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Motivo de encerramento ativado com sucesso"
                        : "Erro ao ativar motivo de encerramento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating motivo encerramento {id} [{ex.Message}]");
                return (false, "Erro ao ativar motivo de encerramento");
            }
        }

        public async Task<List<RecentActivityItem>> GetRecentActivitiesAsync(int limit = 20)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync($"api/account/recent-activity?limit={limit}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get recent activities. Status: {Status}", response.StatusCode);
                    return new List<RecentActivityItem>();
                }

                var result = await response.Content.ReadFromJsonAsync<RecentActivityResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.Success == true ? result.Data ?? new List<RecentActivityItem>() : new List<RecentActivityItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting recent activities [{ex.Message}]");
                return new List<RecentActivityItem>();
            }
        }

        public async Task<(bool Success, string Message)> FinalizarAtendimentoAsync(int atendimentoId, string? motivo = null, decimal? valorVenda = null, int? motivoEncerramentoId = null, string? comprovanteUrl = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var payload = new
                {
                    motivo,
                    valorVenda,
                    motivoEncerramentoId,
                    comprovanteUrl
                };
                var response = await client.PostAsJsonAsync($"api/atendimentos/{atendimentoId}/finalizar", payload);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Atendimento finalizado com sucesso"
                        : "Erro ao finalizar atendimento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finishing atendimento {atendimentoId} [{ex.Message}]");
                return (false, "Erro ao finalizar atendimento");
            }
        }

        public async Task<(bool Success, string? ComprovanteUrl, string Message)> UploadComprovanteAsync(int atendimentoId, Stream fileStream, string fileName, string contentType)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StreamContent(fileStream), "file", fileName);
                    var response = await client.PostAsync($"api/atendimentos/{atendimentoId}/comprovante", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to upload comprovante. Status: {Status}", response.StatusCode);
                        return (false, null, "Erro ao fazer upload do comprovante");
                    }

                    var result = await response.Content.ReadFromJsonAsync<ComprovanteUploadResponse>();
                    return (true, result?.ComprovanteUrl, result?.Message ?? "Comprovante enviado com sucesso");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading comprovante for atendimento {atendimentoId} [{ex.Message}]");
                return (false, null, "Erro ao fazer upload do comprovante");
            }
        }

        public async Task<(bool Success, byte[]? Content, string? ContentType, string Message)> DownloadMediaAsync(string mediaUrl, string? fallbackContentType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mediaUrl))
                    return (false, null, null, "URL da mídia inválida.");

                var trimmedUrl = mediaUrl.Trim();

                // Route locally-stored files and same-host URLs through the authenticated API client.
                // CDN URLs (e.g. *.whatsapp.net) are signed URLs that don't accept the API auth header,
                // so they must be fetched via a separate, unauthenticated client.
                HttpClient client;
                Uri requestUri;

                if (IsWhatsAppCdnUrl(trimmedUrl, out var cdnUri))
                {
                    client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    requestUri = cdnUri!;
                }
                else if (IsBlobMediaUrl(trimmedUrl, out var blobUri))
                {
                    client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    requestUri = blobUri!;
                }
                else
                {
                    client = await CreateAuthenticatedClient();
                    var apiUri = BuildMediaUri(client, trimmedUrl);
                    if (apiUri is null)
                        return (false, null, null, "URL da mídia não permitida.");
                    requestUri = apiUri;
                }

                var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return (false, null, null, "Mídia não encontrada.");
                    _logger.LogWarning("Failed to download media from {MediaUrl}. Status: {Status}", mediaUrl, response.StatusCode);
                    return (false, null, null, "Sem permissão para acessar esta mídia.");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0)
                    return (false, null, null, "Arquivo de mídia vazio.");

                var contentType = ResolveMediaContentType(
                    response.Content.Headers.ContentType?.MediaType,
                    fallbackContentType);
                if (contentType is null)
                    return (false, null, null, "Tipo de mídia não permitido.");

                return (true, bytes, contentType, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading media [{Message}]", ex.Message);
                return (false, null, null, "Erro ao baixar mídia.");
            }
        }

        /// <summary>
        /// Returns true only for HTTPS URLs whose host equals or ends with ".whatsapp.net",
        /// preventing SSRF attacks via arbitrary subdomains. Mirrors the API-side check.
        /// </summary>
        internal static bool IsWhatsAppCdnUrl(string url, out Uri? uri)
        {
            uri = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                return false;
            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;
            var host = parsed.Host.ToLowerInvariant();
            if (host == "whatsapp.net" || host.EndsWith(".whatsapp.net", StringComparison.Ordinal))
            {
                uri = parsed;
                return true;
            }
            return false;
        }

        internal static bool IsBlobMediaUrl(string url, out Uri? uri)
        {
            uri = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                return false;
            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(parsed.Host, Consts.Midia.BlobStorageHost, StringComparison.OrdinalIgnoreCase))
            {
                uri = parsed;
                return true;
            }
            return false;
        }

        private static Uri? BuildMediaUri(HttpClient client, string mediaUrl)
        {
            var trimmedUrl = mediaUrl.Trim();
            if (trimmedUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(trimmedUrl.TrimStart('/'), UriKind.Relative);
            }

            if (trimmedUrl.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri($"uploads/{trimmedUrl.TrimStart('/')}", UriKind.Relative);
            }

            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
                return null;

            if (absoluteUri.Scheme is not ("http" or "https") || client.BaseAddress is null)
                return null;

            return string.Equals(absoluteUri.Host, client.BaseAddress.Host, StringComparison.OrdinalIgnoreCase)
                ? absoluteUri
                : null;
        }

        private static bool IsAllowedMediaContentType(string? contentType)
        {
            return contentType.NormalizeAllowedMediaContentType() is not null;
        }

        private static string? ResolveMediaContentType(string? contentType, string? fallbackContentType)
        {
            return contentType.NormalizeAllowedMediaContentType()
                ?? fallbackContentType.NormalizeAllowedMediaContentType();
        }

        public async Task<(bool Success, string? LogoUrl, string Message)> UploadFarmaciaLogoAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "file", fileName);

                var response = await client.PostAsync("api/account/upload-farmacia-logo", content);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                    return (false, null, error?.Error ?? error?.Message ?? "Erro ao fazer upload da logo da farmácia");
                }

                var result = await response.Content.ReadFromJsonAsync<FarmaciaLogoUploadResponse>();
                return (true, result?.LogoUrl, result?.Message ?? "Logo enviada com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading farmacia logo [{ex.Message}]");
                return (false, null, "Erro ao fazer upload da logo da farmácia");
            }
        }

        public async Task<bool> GetSalvarLogCompletoMidiaApiGoAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/configuracoes/midia-api-go-log-completo");
                if (!response.IsSuccessStatusCode)
                    return false;

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                    return false;
                if (!doc.RootElement.TryGetProperty("data", out var dataProp))
                    return false;
                if (!dataProp.TryGetProperty("salvarLogCompletoMidiaApiGo", out var valueProp))
                    return false;
                return valueProp.ValueKind == JsonValueKind.True;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mídia API GO complete log setting [{Message}]", ex.Message);
                return false;
            }
        }

        public async Task<(bool Success, string Message)> UpdateSalvarLogCompletoMidiaApiGoAsync(bool enabled)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync("api/configuracoes/midia-api-go-log-completo", new { enabled });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(content?.Error) && string.IsNullOrWhiteSpace(content?.Message))
                {
                    _logger.LogWarning(
                        "UpdateSalvarLogCompletoMidiaApiGoAsync received unsuccessful response without error/message. Status: {StatusCode}",
                        response.StatusCode);
                }
                var fallback = response.IsSuccessStatusCode
                    ? "Configuração atualizada com sucesso"
                    : "Erro ao atualizar configuração";
                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? fallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating mídia API GO complete log setting [{Message}]", ex.Message);
                return (false, "Erro ao atualizar configuração");
            }
        }

        public async Task<RelatorioGestorData?> GetRelatorioGestorAsync(DateTime? dataInicio = null, DateTime? dataFim = null, int? farmaciaId = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = BuildRelatorioUrl("api/relatorios/gestor", dataInicio, dataFim);
                if (farmaciaId.HasValue)
                    url += $"&farmaciaId={farmaciaId}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<RelatorioGestorResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting relatorio gestor [{ex.Message}]");
                return null;
            }
        }

        public async Task<RelatorioAdministradorData?> GetRelatorioAdministradorAsync(DateTime? dataInicio = null, DateTime? dataFim = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = BuildRelatorioUrl("api/relatorios/administrador", dataInicio, dataFim);
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<RelatorioAdministradorResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting relatorio administrador [{ex.Message}]");
                return null;
            }
        }

        public async Task<RelatorioAtendenteData?> GetRelatorioAtendenteAsync(DateTime? dataInicio = null, DateTime? dataFim = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = BuildRelatorioUrl("api/relatorios/atendente", dataInicio, dataFim);
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<RelatorioAtendenteResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting relatorio atendente [{ex.Message}]");
                return null;
            }
        }

        public async Task<RelatorioFaturamentoData?> GetRelatorioFaturamentoAsync(DateTime? dataInicio = null, DateTime? dataFim = null, int? atendenteId = null, int? farmaciaId = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = BuildRelatorioUrl("api/relatorios/faturamento", dataInicio, dataFim);
                if (atendenteId.HasValue)
                    url += $"&atendenteId={atendenteId}";
                if (farmaciaId.HasValue)
                    url += $"&farmaciaId={farmaciaId}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<RelatorioFaturamentoResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting relatorio faturamento [{ex.Message}]");
                return null;
            }
        }

        public async Task<List<AtendimentoCompraDto>> GetAtendimentosClienteAsync(int clienteId)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync($"api/clientes/{clienteId}/atendimentos");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get atendimentos cliente. Status: {Status}", response.StatusCode);
                    return new List<AtendimentoCompraDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<AtendimentoClienteListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<AtendimentoCompraDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting atendimentos cliente {clienteId} [{ex.Message}]");
                return new List<AtendimentoCompraDto>();
            }
        }

        private static string BuildRelatorioUrl(string baseUrl, DateTime? dataInicio, DateTime? dataFim)
        {
            var separator = baseUrl.Contains('?') ? "&" : "?";
            if (dataInicio.HasValue)
            {
                baseUrl += $"{separator}dataInicio={dataInicio.Value:yyyy-MM-dd}";
                separator = "&";
            }
            if (dataFim.HasValue)
                baseUrl += $"{separator}dataFim={dataFim.Value:yyyy-MM-dd}";
            return baseUrl;
        }

        public async Task<byte[]?> ExportarRelatorioAsync(string tipo, DateTime? dataInicio = null, DateTime? dataFim = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = BuildRelatorioUrl($"api/relatorios/exportar?tipo={tipo}", dataInicio, dataFim);
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting relatorio [{ex.Message}]");
                return null;
            }
        }

        public async Task<QueueStats?> GetQueueStatsAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/messages/queue-stats");
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<QueueStatsResponse>();
                return result?.Success == true ? result.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting queue stats [{ex.Message}]");
                return null;
            }
        }

        public async Task<List<Mensagem>> GetMensagensAtendimentoAsync(int atendimentoId)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync($"api/atendimentos/{atendimentoId}/mensagens");
                if (!response.IsSuccessStatusCode)
                    return new List<Mensagem>();

                var result = await response.Content.ReadFromJsonAsync<MensagemListResponse>();
                return result?.Success == true && result.Data != null
                    ? result.Data.Select(dto => dto.ToMensagem()).ToList()
                    : new List<Mensagem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting mensagens for atendimento {atendimentoId} [{ex.Message}]");
                return new List<Mensagem>();
            }
        }

        public async Task<(bool Success, string Message)> EnviarMensagemAsync(int atendimentoId, string text)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var payload = new { text };
                var response = await client.PostAsJsonAsync($"api/atendimentos/{atendimentoId}/mensagens", payload);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode && content?.Success == true,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "A mensagem foi registrada, mas o provedor não confirmou o envio."
                        : "Não foi possível enviar a mensagem."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message for atendimento {atendimentoId} [{ex.Message}]");
                return (false, "Não foi possível comunicar com o servidor para enviar a mensagem.");
            }
        }

        public async Task<(bool Success, string Message)> TransferirAtendimentoAsync(int atendimentoId, int targetAtendenteId)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var payload = new { targetAtendenteId };
                var response = await client.PostAsJsonAsync($"api/atendimentos/{atendimentoId}/transferir", payload);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Atendimento transferido com sucesso"
                        : "Erro ao transferir atendimento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring atendimento {atendimentoId} [{ex.Message}]");
                return (false, "Erro ao transferir atendimento");
            }
        }

        public async Task<List<EtiquetaDto>> GetEtiquetasAsync(bool? ativo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/etiquetas";
                if (ativo.HasValue)
                    url += $"?ativo={ativo.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<EtiquetaDto>();

                var result = await response.Content.ReadFromJsonAsync<EtiquetaListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<EtiquetaDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting etiquetas [{ex.Message}]");
                return new List<EtiquetaDto>();
            }
        }

        public async Task<List<DepartamentoDto>> GetDepartamentosAsync(bool? ativo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/departamentos";
                if (ativo.HasValue)
                    url += $"?ativo={ativo.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<DepartamentoDto>();

                var result = await response.Content.ReadFromJsonAsync<DepartamentoListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<DepartamentoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting departamentos [{ex.Message}]");
                return new List<DepartamentoDto>();
            }
        }

        public async Task<List<DepartamentoDto>> GetDepartamentosByFarmaciaAsync(int farmaciaId, bool? ativo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = $"api/account/farmacia/{farmaciaId}/departamentos";
                if (ativo.HasValue)
                    url += $"?ativo={ativo.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<DepartamentoDto>();

                var result = await response.Content.ReadFromJsonAsync<DepartamentoListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<DepartamentoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting departamentos for farmacia {farmaciaId} [{ex.Message}]");
                return new List<DepartamentoDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateDepartamentoAsync(CreateDepartamentoDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/departamentos", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Departamento criado com sucesso"
                        : "Erro ao criar departamento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating departamento [{ex.Message}]");
                return (false, "Erro ao criar departamento");
            }
        }

        public async Task<(bool Success, string Message)> UpdateDepartamentoAsync(int id, UpdateDepartamentoDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/departamentos/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Departamento atualizado com sucesso"
                        : "Erro ao atualizar departamento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating departamento {id} [{ex.Message}]");
                return (false, "Erro ao atualizar departamento");
            }
        }

        public async Task<(bool Success, string Message)> InativarDepartamentoAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/departamentos/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Departamento inativado com sucesso"
                        : "Erro ao inativar departamento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inactivating departamento {id} [{ex.Message}]");
                return (false, "Erro ao inativar departamento");
            }
        }

        public async Task<(bool Success, string Message)> AtivarDepartamentoAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/departamentos/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Departamento ativado com sucesso"
                        : "Erro ao ativar departamento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating departamento {id} [{ex.Message}]");
                return (false, "Erro ao ativar departamento");
            }
        }

        public async Task<(bool Success, string Message)> CreateEtiquetaAsync(CreateEtiquetaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/etiquetas", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Etiqueta criada com sucesso"
                        : "Erro ao criar etiqueta"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating etiqueta [{ex.Message}]");
                return (false, "Erro ao criar etiqueta");
            }
        }

        public async Task<(bool Success, string Message)> UpdateEtiquetaAsync(int id, UpdateEtiquetaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/etiquetas/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Etiqueta atualizada com sucesso"
                        : "Erro ao atualizar etiqueta"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating etiqueta {id} [{ex.Message}]");
                return (false, "Erro ao atualizar etiqueta");
            }
        }

        public async Task<(bool Success, string Message)> InativarEtiquetaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/etiquetas/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Etiqueta inativada com sucesso"
                        : "Erro ao inativar etiqueta"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inactivating etiqueta {id} [{ex.Message}]");
                return (false, "Erro ao inativar etiqueta");
            }
        }

        public async Task<(bool Success, string Message)> AtivarEtiquetaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/etiquetas/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Etiqueta ativada com sucesso"
                        : "Erro ao ativar etiqueta"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating etiqueta {id} [{ex.Message}]");
                return (false, "Erro ao ativar etiqueta");
            }
        }

        public async Task<(bool Success, string Message)> AtualizarEtiquetaAtendimentoAsync(int atendimentoId, int? etiquetaId)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var payload = new { etiquetaId };
                var response = await client.PutAsJsonAsync($"api/atendimentos/{atendimentoId}/etiqueta", payload);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Etiqueta do atendimento atualizada com sucesso"
                        : "Erro ao atualizar etiqueta do atendimento"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating etiqueta for atendimento {atendimentoId} [{ex.Message}]");
                return (false, "Erro ao atualizar etiqueta do atendimento");
            }
        }

        public async Task<List<FarmaciaDto>> GetFarmaciasAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/account/farmacias");
                if (!response.IsSuccessStatusCode)
                    return new List<FarmaciaDto>();

                var result = await response.Content.ReadFromJsonAsync<FarmaciaListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<FarmaciaDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting farmacias [{ex.Message}]");
                return new List<FarmaciaDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateFarmaciaAsync(CreateFarmaciaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/account/register-farmacia", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Farmácia criada com sucesso"
                        : "Erro ao criar farmácia"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating farmacia [{ex.Message}]");
                return (false, "Erro ao criar farmácia");
            }
        }

        public async Task<(bool Success, string Message)> UpdateFarmaciaAsync(int id, UpdateFarmaciaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/account/update-farmacia/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Farmácia atualizada com sucesso"
                        : "Erro ao atualizar farmácia"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating farmacia {id} [{ex.Message}]");
                return (false, "Erro ao atualizar farmácia");
            }
        }

        public async Task<List<AdministradorDto>> GetAdministradoresAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/usuarios?role=Administrador");
                if (!response.IsSuccessStatusCode)
                    return new List<AdministradorDto>();

                var result = await response.Content.ReadFromJsonAsync<UsuariosListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<AdministradorDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting administradores [{ex.Message}]");
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
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Administrador criado com sucesso"
                        : "Erro ao criar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating administrador [{ex.Message}]");
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
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Administrador atualizado com sucesso"
                        : "Erro ao atualizar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating administrador {id} [{ex.Message}]");
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
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Administrador inativado com sucesso"
                        : "Erro ao inativar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inactivating administrador {id} [{ex.Message}]");
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
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Administrador ativado com sucesso"
                        : "Erro ao ativar administrador"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating administrador {id} [{ex.Message}]");
                return (false, "Erro ao ativar administrador");
            }
        }

        public async Task<(bool Success, string Message)> ResetSenhaAdministradorAsync(int id, string newPassword)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/usuarios/{id}/reset-password", new { newPassword });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Senha redefinida com sucesso"
                        : "Erro ao redefinir senha"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password for administrador {id} [{ex.Message}]");
                return (false, "Erro ao redefinir senha");
            }
        }

        public async Task<List<MensagemPreDefinidaDto>> GetMensagensPreDefinidasAsync(bool? ativo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var url = "api/mensagens-pre-definidas";
                if (ativo.HasValue)
                    url += $"?ativo={ativo.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get mensagens pré-definidas. Status: {Status}", response.StatusCode);
                    return new List<MensagemPreDefinidaDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<MensagemPreDefinidaListResponse>();
                return result?.Success == true && result.Data != null ? result.Data : new List<MensagemPreDefinidaDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting mensagens pré-definidas [{ex.Message}]");
                return new List<MensagemPreDefinidaDto>();
            }
        }

        public async Task<(bool Success, string Message)> CreateMensagemPreDefinidaAsync(CreateMensagemPreDefinidaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("api/mensagens-pre-definidas", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Mensagem pré-definida criada com sucesso"
                        : "Erro ao criar mensagem pré-definida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating mensagem pré-definida [{ex.Message}]");
                return (false, "Erro ao criar mensagem pré-definida");
            }
        }

        public async Task<(bool Success, string Message)> UpdateMensagemPreDefinidaAsync(int id, UpdateMensagemPreDefinidaDto request)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync($"api/mensagens-pre-definidas/{id}", request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Mensagem pré-definida atualizada com sucesso"
                        : "Erro ao atualizar mensagem pré-definida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating mensagem pré-definida {id} [{ex.Message}]");
                return (false, "Erro ao atualizar mensagem pré-definida");
            }
        }

        public async Task<(bool Success, string Message)> InativarMensagemPreDefinidaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/mensagens-pre-definidas/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Mensagem pré-definida inativada com sucesso"
                        : "Erro ao inativar mensagem pré-definida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inactivating mensagem pré-definida {id} [{ex.Message}]");
                return (false, "Erro ao inativar mensagem pré-definida");
            }
        }

        public async Task<(bool Success, string Message)> AtivarMensagemPreDefinidaAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsync($"api/mensagens-pre-definidas/{id}/ativar", null);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Mensagem pré-definida ativada com sucesso"
                        : "Erro ao ativar mensagem pré-definida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating mensagem pré-definida {id} [{ex.Message}]");
                return (false, "Erro ao ativar mensagem pré-definida");
            }
        }

        public async Task<(bool Success, string Message)> BloqueRemetenteAsync(int atendimentoId, string escopo, string? motivo = null)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var payload = new { escopo, motivo };
                var request = new HttpRequestMessage(HttpMethod.Delete, $"api/atendimentos/{atendimentoId}/bloquear-remetente")
                {
                    Content = JsonContent.Create(payload)
                };
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Remetente bloqueado com sucesso"
                        : "Erro ao bloquear remetente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error blocking sender for atendimento {atendimentoId} [{ex.Message}]");
                return (false, "Erro ao bloquear remetente");
            }
        }

        public async Task<List<RemetenteBloqueado>> ListarRemetentesBlockeadosAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/configuracoes/remetentes-bloqueados");
                if (!response.IsSuccessStatusCode)
                    return new List<RemetenteBloqueado>();

                var result = await response.Content.ReadFromJsonAsync<List<RemetenteBloqueado>>();
                return result ?? new List<RemetenteBloqueado>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing blocked senders [{ex.Message}]");
                return new List<RemetenteBloqueado>();
            }
        }

        public async Task<(bool Success, string Message)> DesbloqueRemetenteAsync(int id)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.DeleteAsync($"api/configuracoes/remetentes-bloqueados/{id}");
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                return (response.IsSuccessStatusCode,
                    content?.Error ?? content?.Message ?? (response.IsSuccessStatusCode
                        ? "Remetente desbloqueado com sucesso"
                        : "Erro ao desbloquear remetente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unblocking sender {id} [{ex.Message}]");
                return (false, "Erro ao desbloquear remetente");
            }
        }

        public async Task<bool> GetHabilitarBloqueioRemetenteAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/configuracoes/habilitar-bloqueio-remetente");

                if (!response.IsSuccessStatusCode)
                    return false;

                using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = jsonDoc.RootElement;
                if (!root.TryGetProperty("habilitarBloqueioRemetente", out var value))
                    return false;

                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return value.GetBoolean();

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error getting blocking configuration [{ex.Message}]");
                return false;
            }
        }

        public async Task<(bool Success, string Message)> UpdateHabilitarBloqueioRemetenteAsync(bool enabled)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync("api/configuracoes/habilitar-bloqueio-remetente", new { enabled });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                var fallback = response.IsSuccessStatusCode
                    ? "Configuração atualizada com sucesso"
                    : "Erro ao atualizar configuração";

                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? fallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sender blocking setting [{Message}]", ex.Message);
                return (false, "Erro ao atualizar configuração");
            }
        }

        public async Task<bool> GetPermitirGerenciarEtiquetasAtendenteAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/configuracoes/permitir-gerenciar-etiquetas-atendente");

                if (!response.IsSuccessStatusCode)
                    return false;

                using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = jsonDoc.RootElement;
                if (!root.TryGetProperty("permitirGerenciarEtiquetasAtendente", out var value))
                    return false;

                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return false;

                return value.GetBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting attendant etiqueta management setting [{Message}]", ex.Message);
                return false;
            }
        }

        public async Task<(bool Success, string Message)> UpdatePermitirGerenciarEtiquetasAtendenteAsync(bool enabled)
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.PutAsJsonAsync("api/configuracoes/permitir-gerenciar-etiquetas-atendente", new { enabled });
                var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();
                var fallback = response.IsSuccessStatusCode
                    ? "Configuração atualizada com sucesso"
                    : "Erro ao atualizar configuração";

                return (response.IsSuccessStatusCode, content?.Error ?? content?.Message ?? fallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendant etiqueta management setting [{Message}]", ex.Message);
                return (false, "Erro ao atualizar configuração");
            }
        }

        public async Task<bool> CanManageEtiquetasAsync()
        {
            try
            {
                var client = await CreateAuthenticatedClient();
                var response = await client.GetAsync("api/etiquetas/permissoes");

                if (!response.IsSuccessStatusCode)
                    return _authService.CurrentUser?.IsAtendente != true;

                var content = await response.Content.ReadAsStringAsync();

                var parsed = JsonSerializer.Deserialize<EtiquetaPermissoesResponse>(content, _jsonOptions);
                if (parsed?.Data != null)
                    return parsed.Data.PodeGerenciarEtiquetas;

                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;
                JsonElement data;
                if (!root.TryGetProperty("data", out data) && !root.TryGetProperty("Data", out data))
                    return _authService.CurrentUser?.IsAtendente != true;

                JsonElement value;
                if (!data.TryGetProperty("podeGerenciarEtiquetas", out value) && !data.TryGetProperty("PodeGerenciarEtiquetas", out value))
                    return _authService.CurrentUser?.IsAtendente != true;

                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return value.GetBoolean();

                return _authService.CurrentUser?.IsAtendente != true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting etiqueta management permission [{Message}]", ex.Message);
                return _authService.CurrentUser?.IsAtendente != true;
            }
        }

        private sealed record EtiquetaPermissoesResponse
        {
            public EtiquetaPermissoesData? Data { get; set; }
        }

        private sealed record EtiquetaPermissoesData
        {
            public bool PodeGerenciarEtiquetas { get; set; }
        }
    }
}
