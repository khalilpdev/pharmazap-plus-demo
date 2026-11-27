using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Web.Services;

public class ChannelService : IChannelService
{
    // A API serializa enums como string camelCase (Program.cs: JsonStringEnumConverter),
    // então o cliente precisa do mesmo converter para não falhar ao desserializar
    // campos como MetaConfigSource (ex.: "own", "pharmazapCentral").
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILocalStorageService localStorage,
        ILogger<ChannelService> logger)
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
                _logger.LogDebug(ex, "Erro ao buscar token do local storage");
            }
        }

        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public async Task<(bool Success, string Message)> CreateChannelAsync(ChannelCreateRequest request)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.PostAsJsonAsync("api/channels", request);
            var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();

            if (response.IsSuccessStatusCode)
                return (true, content?.Message ?? "Canal criado com sucesso");

            var message = content?.Error ?? content?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = await response.Content.ReadAsStringAsync();

            return (false, $"[{(int)response.StatusCode}] {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar canal");
            return (false, "Erro ao criar canal");
        }
    }

    public async Task<(bool Success, List<ChannelListItem> Channels, string Message)> GetChannelsAsync()
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.GetAsync("api/channels");

            if (!response.IsSuccessStatusCode)
                return (false, new(), "Erro ao carregar canais");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("data", out var data))
                return (false, new(), "Resposta inválida da API");

            var channels = JsonSerializer.Deserialize<List<ChannelListItem>>(data.GetRawText(), JsonOptions) ?? new();
            return (true, channels, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar canais");
            return (false, new(), "Erro ao listar canais");
        }
    }

    public async Task<(bool Success, ChannelDetailDto? Channel, string Message)> GetChannelAsync(int type, bool isOfficial)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.GetAsync($"api/channels/{type}/{isOfficial}");

            if (!response.IsSuccessStatusCode)
                return (false, null, "Erro ao carregar canal");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("data", out var data))
                return (false, null, "Resposta inválida da API");

            var channel = JsonSerializer.Deserialize<ChannelDetailDto>(data.GetRawText(), JsonOptions);
            return (true, channel, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter canal");
            return (false, null, "Erro ao obter canal");
        }
    }

    public async Task<(bool Success, ChannelDetailDto? Channel, string Message)> GetChannelByIdAsync(int id)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.GetAsync($"api/channels/id/{id}");

            if (!response.IsSuccessStatusCode)
                return (false, null, "Erro ao carregar canal");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("data", out var data))
                return (false, null, "Resposta inválida da API");

            var channel = JsonSerializer.Deserialize<ChannelDetailDto>(data.GetRawText(), JsonOptions);
            return (true, channel, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter canal por ID");
            return (false, null, "Erro ao obter canal");
        }
    }

    public async Task<(bool Success, string Message)> UpdateChannelAsync(int type, bool isOfficial, ChannelCreateRequest request)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.PutAsJsonAsync($"api/channels/{type}/{isOfficial}", request);
            var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();

            if (response.IsSuccessStatusCode)
                return (true, content?.Message ?? "Canal atualizado com sucesso");

            var message = content?.Error ?? content?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = await response.Content.ReadAsStringAsync();

            return (false, $"[{(int)response.StatusCode}] {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar canal");
            return (false, "Erro ao atualizar canal");
        }
    }

    public async Task<(bool Success, string Message)> UpdateChannelByIdAsync(int id, ChannelCreateRequest request)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.PutAsJsonAsync($"api/channels/id/{id}", request);
            var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();

            if (response.IsSuccessStatusCode)
                return (true, content?.Message ?? "Canal atualizado com sucesso");

            var message = content?.Error ?? content?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = await response.Content.ReadAsStringAsync();

            return (false, $"[{(int)response.StatusCode}] {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar canal por ID");
            return (false, "Erro ao atualizar canal");
        }
    }

    public async Task<(bool Success, string Message)> DeleteChannelAsync(int id)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.DeleteAsync($"api/channels/{id}");
            var content = await response.Content.ReadFromJsonAsync<ApiSimpleResponse>();

            if (response.IsSuccessStatusCode)
                return (true, content?.Message ?? "Canal excluído com sucesso");

            var message = content?.Error ?? content?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = await response.Content.ReadAsStringAsync();

            return (false, $"[{(int)response.StatusCode}] {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir canal");
            return (false, "Erro ao excluir canal");
        }
    }

    public async Task<QrCodeResult> GenerateQrCodeAsync(string instanceName, string apiUrl, string apiKey)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var request = new QrCodeGenerateRequest
            {
                InstanceName = instanceName,
                ApiUrl = apiUrl,
                ApiKey = apiKey
            };
            var response = await client.PostAsJsonAsync(Consts.PharmazapApi.Endpoints.ChannelsQrCode, request);
            var rawBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                "GenerateQrCodeAsync: status={Status} instance={Instance} bytes={Bytes}",
                response.StatusCode,
                instanceName,
                rawBody.Length);

            if (!TryParseJson(rawBody, out var json))
            {
                _logger.LogWarning(
                    "GenerateQrCodeAsync: resposta nao-JSON para {Instance}: {Body}",
                    instanceName,
                    TruncateForLog(rawBody));
                return new QrCodeResult { Success = false, Error = "Erro ao gerar QR Code" };
            }

            var apiReportedSuccess = json.TryGetProperty("success", out var successEl)
                && successEl.ValueKind == JsonValueKind.True;

            if (response.IsSuccessStatusCode && apiReportedSuccess)
            {
                if (json.TryGetProperty("connected", out var connectedEl) &&
                    connectedEl.ValueKind == JsonValueKind.True)
                {
                    return new QrCodeResult { Success = true, AlreadyConnected = true };
                }

                var imageUrl = ExtractString(json, "imageUrl");
                var base64 = ExtractString(json, "base64");

                if (!string.IsNullOrWhiteSpace(imageUrl) || !string.IsNullOrWhiteSpace(base64))
                {
                    _logger.LogInformation(
                        "GenerateQrCodeAsync: QR recebido para {Instance} imageUrl={ImageUrl} base64Bytes={Base64Bytes}",
                        instanceName,
                        imageUrl ?? "(none)",
                        base64?.Length ?? 0);
                    return new QrCodeResult
                    {
                        Success = true,
                        Base64 = base64,
                        ImageUrl = string.IsNullOrWhiteSpace(imageUrl)
                            ? string.Empty
                            : ToQrCodeProxyUrl(imageUrl)
                    };
                }
            }

            var errorMessage = ExtractString(json, "error");
            _logger.LogWarning(
                "GenerateQrCodeAsync: falha para {Instance} status={Status} error={Error} body={Body}",
                instanceName,
                response.StatusCode,
                errorMessage ?? "(sem error)",
                TruncateForLog(rawBody));
            return new QrCodeResult
            {
                Success = false,
                Error = errorMessage ?? "Erro ao gerar QR Code"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar QR Code para {Instance}", instanceName);
            return new QrCodeResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    private static string ToQrCodeProxyUrl(string imageUrl)
    {
        var fileName = Path.GetFileName(Uri.UnescapeDataString(imageUrl));
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        return $"/qr-code-proxy/{Uri.EscapeDataString(fileName)}?v={Guid.NewGuid():N}";
    }

    public async Task<PairingCodeResult> GeneratePairingCodeAsync(string instanceName, string apiUrl, string apiKey, string phoneNumber)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var request = new
            {
                InstanceName = instanceName,
                ApiUrl = apiUrl,
                ApiKey = apiKey,
                PhoneNumber = phoneNumber
            };
            var response = await client.PostAsJsonAsync(Consts.PharmazapApi.Endpoints.ChannelsPairingCode, request);
            var rawBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                "GeneratePairingCodeAsync: status={Status} instance={Instance} phoneDigits={PhoneDigits} bytes={Bytes}",
                response.StatusCode,
                instanceName,
                phoneNumber?.Length ?? 0,
                rawBody.Length);

            if (!TryParseJson(rawBody, out var json))
            {
                _logger.LogWarning(
                    "GeneratePairingCodeAsync: resposta nao-JSON para {Instance}: {Body}",
                    instanceName,
                    TruncateForLog(rawBody));
                return new PairingCodeResult { Success = false, Error = "Erro ao gerar código de pareamento" };
            }

            var apiReportedSuccess = json.TryGetProperty("success", out var successEl)
                && successEl.ValueKind == JsonValueKind.True;

            if (response.IsSuccessStatusCode && apiReportedSuccess && TryExtractPairingCode(json, out var code))
            {
                _logger.LogInformation(
                    "GeneratePairingCodeAsync: codigo recebido para {Instance}: {Code}",
                    instanceName,
                    code);
                return new PairingCodeResult { Success = true, Code = code };
            }

            var errorMessage = ExtractString(json, "error");
            _logger.LogWarning(
                "GeneratePairingCodeAsync: falha para {Instance} status={Status} error={Error} body={Body}",
                instanceName,
                response.StatusCode,
                errorMessage ?? "(sem error)",
                TruncateForLog(rawBody));
            return new PairingCodeResult { Success = false, Error = errorMessage ?? "Erro ao gerar código de pareamento" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar código de pareamento para {Instance}", instanceName);
            return new PairingCodeResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    private static bool TryParseJson(string body, out JsonElement json)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                json = JsonDocument.Parse(body).RootElement.Clone();
                return true;
            }
            catch (JsonException)
            {
            }
        }

        json = default;
        return false;
    }

    private static string? ExtractString(JsonElement json, string propertyName)
    {
        return json.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryExtractPairingCode(JsonElement json, out string? code)
    {
        code = ExtractString(json, "code")
            ?? ExtractString(json, "pairingCode")
            ?? ExtractString(json, "pairing_code")
            ?? ExtractString(json, "pairCode");

        if (!string.IsNullOrWhiteSpace(code))
            return true;

        if (json.TryGetProperty("data", out var data))
        {
            code = ExtractString(data, "code")
                ?? ExtractString(data, "pairingCode")
                ?? ExtractString(data, "pairing_code")
                ?? ExtractString(data, "pairCode");
        }

        return !string.IsNullOrWhiteSpace(code);
    }

    private static string TruncateForLog(string? value, int maxLength = 1200)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }

    public async Task<WhatsAppStatusResult> GetWhatsAppStatusAsync(string instanceName, string apiUrl, string apiKey)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var encodedInstance = Uri.EscapeDataString(instanceName);
            var encodedUrl = Uri.EscapeDataString(apiUrl);
            var encodedKey = Uri.EscapeDataString(apiKey);
            var response = await client.GetAsync($"{Consts.PharmazapApi.Endpoints.ChannelsWhatsAppStatus}?instanceName={encodedInstance}&apiUrl={encodedUrl}&apiKey={encodedKey}");
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (response.IsSuccessStatusCode)
            {
                json.TryGetProperty("status", out var stEl);
                return new WhatsAppStatusResult { Success = true, Status = stEl.GetString() };
            }

            json.TryGetProperty("error", out var errEl);
            return new WhatsAppStatusResult { Success = false, Error = errEl.GetString() ?? "Erro ao consultar status" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status da instância");
            return new WhatsAppStatusResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    public async Task<ListInstancesResult> ListWhatsAppInstancesAsync(string apiUrl, string apiKey)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var encodedUrl = Uri.EscapeDataString(apiUrl);
            var encodedKey = Uri.EscapeDataString(apiKey);
            var response = await client.GetAsync($"{Consts.PharmazapApi.Endpoints.ChannelsWhatsAppInstances}?apiUrl={encodedUrl}&apiKey={encodedKey}");
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!response.IsSuccessStatusCode)
            {
                json.TryGetProperty("error", out var errEl);
                return new ListInstancesResult { Success = false, Error = errEl.GetString() ?? "Erro ao listar instâncias" };
            }

            if (json.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.False)
            {
                json.TryGetProperty("error", out var errEl);
                return new ListInstancesResult { Success = false, Error = errEl.GetString() ?? "Erro ao listar instâncias" };
            }

            var result = new ListInstancesResult { Success = true };
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (json.TryGetProperty("instances", out var instancesEl) && instancesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in instancesEl.EnumerateArray())
                {
                    if (!item.TryGetProperty("name", out var nameEl))
                    {
                        continue;
                    }

                    var name = nameEl.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
                    {
                        continue;
                    }

                    item.TryGetProperty("status", out var statusEl);
                    result.Instances.Add(new InstanceOption
                    {
                        Name = name,
                        Status = statusEl.ValueKind == JsonValueKind.String ? statusEl.GetString() : null
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar instâncias");
            return new ListInstancesResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    public async Task<InstanceInfoResult> CheckInstanceExistsAsync(string instanceName, string apiUrl, string apiKey)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var encodedInstance = Uri.EscapeDataString(instanceName);
            var encodedUrl = Uri.EscapeDataString(apiUrl);
            var encodedKey = Uri.EscapeDataString(apiKey);
            var response = await client.GetAsync($"{Consts.PharmazapApi.Endpoints.ChannelsWhatsAppInstanceInfo}?instanceName={encodedInstance}&apiUrl={encodedUrl}&apiKey={encodedKey}");
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (response.IsSuccessStatusCode)
            {
                json.TryGetProperty("exists", out var existsEl);
                json.TryGetProperty("status", out var stEl);
                return new InstanceInfoResult
                {
                    Success = true,
                    Exists = existsEl.ValueKind == JsonValueKind.True,
                    Status = stEl.ValueKind == JsonValueKind.String ? stEl.GetString() : null
                };
            }

            json.TryGetProperty("error", out var errEl);
            return new InstanceInfoResult { Success = false, Error = errEl.GetString() ?? "Erro ao verificar instância" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência da instância");
            return new InstanceInfoResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    public async Task<EnsureInstanceResult> EnsureInstanceCreatedAsync(string instanceName, string apiUrl, string apiKey = "", bool forceRecreate = false)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var request = new QrCodeGenerateRequest
            {
                InstanceName = instanceName,
                ApiUrl = apiUrl,
                ApiKey = apiKey,
                ForceRecreate = forceRecreate
            };
            var response = await client.PostAsJsonAsync(Consts.PharmazapApi.Endpoints.ChannelsWhatsAppEnsureInstance, request);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (response.IsSuccessStatusCode)
            {
                json.TryGetProperty("created", out var createdEl);
                json.TryGetProperty("alreadyExists", out var alreadyExistsEl);
                return new EnsureInstanceResult
                {
                    Success = true,
                    Created = createdEl.ValueKind == JsonValueKind.True,
                    AlreadyExists = alreadyExistsEl.ValueKind == JsonValueKind.True
                };
            }

            json.TryGetProperty("error", out var errEl);
            return new EnsureInstanceResult { Success = false, Error = errEl.GetString() ?? "Falha ao criar instância" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao garantir criação da instância");
            return new EnsureInstanceResult { Success = false, Error = "Erro de comunicação com a API" };
        }
    }

    public async Task<MetaOAuthStartResult> StartMetaOAuthAsync(
        ChannelType channelType,
        MetaConfigSource metaConfigSource,
        MetaAppMode metaAppMode,
        string? metaAppId,
        string? metaAppSecret,
        int? channelId = null)
    {
        try
        {
            var client = await CreateAuthenticatedClient();
            var response = await client.PostAsJsonAsync("api/meta/oauth/start", new
            {
                channelType = (int)channelType,
                configSource = (int)metaConfigSource,
                appMode = (int)metaAppMode,
                appId = metaAppId,
                appSecret = metaAppSecret,
                channelId
            });
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!response.IsSuccessStatusCode)
            {
                var error = json.TryGetProperty("error", out var errorEl) ? errorEl.GetString() : null;
                return new MetaOAuthStartResult
                {
                    Success = false,
                    Error = error ?? "Não foi possível iniciar a conexão com a Meta."
                };
            }

            return new MetaOAuthStartResult
            {
                Success = json.TryGetProperty("success", out var successEl) && successEl.GetBoolean(),
                AuthorizationUrl = json.TryGetProperty("authorizationUrl", out var urlEl) ? urlEl.GetString() : null,
                State = json.TryGetProperty("state", out var stateEl) ? stateEl.GetString() : null,
                Message = json.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar OAuth da Meta");
            return new MetaOAuthStartResult
            {
                Success = false,
                Error = "Erro ao iniciar conexão com a Meta."
            };
        }
    }
}
