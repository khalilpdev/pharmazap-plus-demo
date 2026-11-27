using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Config;
using System.Text.Json;

namespace PharmazapPlus.API.Services.MetaOAuth;

public sealed class MetaOAuthService : IMetaOAuthService
{
    private const string CachePrefix = "meta-oauth-state:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly MetaOAuthConfig _config;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetaOAuthService> _logger;

    public MetaOAuthService(
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache,
        IOptions<MetaOAuthConfig> config,
        IConfiguration configuration,
        ILogger<MetaOAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _config = config.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MetaOAuthStartResult> CreateAuthorizationUrlAsync(MetaOAuthStartRequest request)
    {
        var appId = ResolveAppId(request.ChannelType, request.ConfigSource, request.AppMode, request.AppId);
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new MetaOAuthStartResult(false, null, null, null, GetAppIdErrorMessage(request.ConfigSource));
        }

        var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var payload = new MetaOAuthStatePayload(
            request.ChannelType,
            request.ConfigSource,
            request.AppMode,
            request.AppId?.Trim(),
            request.AppSecret?.Trim(),
            DateTimeOffset.UtcNow,
            request.FarmaciaId,
            request.SaveFullLog);
        var serialized = JsonSerializer.Serialize(payload, JsonOptions);
        await _cache.SetStringAsync(
            CacheKey(state),
            serialized,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        var scopes = GetScopes(request.ChannelType);
        var redirectUri = NormalizeRedirectUri();
        var authorizationUrl = request.ChannelType == MetaOAuthChannelType.Instagram
            ? $"https://www.instagram.com/oauth/authorize" +
              $"?client_id={Uri.EscapeDataString(appId)}" +
              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
              $"&response_type=code" +
              $"&state={Uri.EscapeDataString(state)}" +
              $"&scope={Uri.EscapeDataString(scopes)}"
            : $"{Consts.MetaApi.LoginBaseUrl}{_config.ApiVersion}/dialog/oauth" +
              $"?client_id={Uri.EscapeDataString(appId)}" +
              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
              $"&response_type=code" +
              $"&state={Uri.EscapeDataString(state)}" +
              $"&scope={Uri.EscapeDataString(scopes)}";

        if (request.SaveFullLog)
        {
            _logger.LogInformation(
                "Meta OAuth [logCompleto] start | farmaciaId={FarmaciaId} | channelType={ChannelType} | appMode={AppMode} | authorizationUrl={AuthorizationUrl}",
                request.FarmaciaId,
                request.ChannelType,
                request.AppMode,
                authorizationUrl);
        }

        return new MetaOAuthStartResult(true, authorizationUrl, state, "URL de autorização gerada.", null);
    }

    public async Task<MetaOAuthConnectionResult> CompleteAuthorizationAsync(string code, string state)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new MetaOAuthConnectionResult(false, "Código OAuth ausente.", null, null, null, null, null, null, null, "missing_code");

        if (string.IsNullOrWhiteSpace(state))
            return new MetaOAuthConnectionResult(false, "State OAuth ausente.", null, null, null, null, null, null, null, "missing_state");

        var payloadJson = await _cache.GetStringAsync(CacheKey(state));
        if (string.IsNullOrWhiteSpace(payloadJson))
            return new MetaOAuthConnectionResult(false, "Sessão OAuth expirada ou inválida.", null, null, null, null, null, null, null, "invalid_state");

        var payload = JsonSerializer.Deserialize<MetaOAuthStatePayload>(payloadJson, JsonOptions);
        if (payload == null)
            return new MetaOAuthConnectionResult(false, "Sessão OAuth inválida.", null, null, null, null, null, null, null, "invalid_state");

        await _cache.RemoveAsync(CacheKey(state));

        var appId = ResolveAppId(payload.ChannelType, payload.ConfigSource, payload.AppMode, payload.AppId);
        if (string.IsNullOrWhiteSpace(appId))
            return new MetaOAuthConnectionResult(false, GetAppIdErrorMessage(payload.ConfigSource), null, null, null, null, null, null, null, "invalid_app_mode");

        if (payload.SaveFullLog)
        {
            _logger.LogInformation(
                "Meta OAuth [logCompleto] callback start | farmaciaId={FarmaciaId} | channelType={ChannelType} | appMode={AppMode} | state={State} | codeLength={CodeLength}",
                payload.FarmaciaId,
                payload.ChannelType,
                payload.AppMode,
                state,
                code.Length);
        }

        var tokenResult = await ExchangeCodeForTokenAsync(
            code,
            appId,
            payload.ChannelType,
            payload.ConfigSource,
            payload.AppSecret,
            payload.SaveFullLog,
            payload.FarmaciaId);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            return new MetaOAuthConnectionResult(false, tokenResult.ErrorMessage ?? "Falha ao obter access token.", null, null, null, null, null, null, null, tokenResult.ErrorCode);

        var longLivedToken = await ExchangeForLongLivedTokenAsync(
            tokenResult.AccessToken,
            appId,
            payload.ChannelType,
            payload.ConfigSource,
            payload.AppSecret,
            payload.SaveFullLog,
            payload.FarmaciaId);
        var accessToken = !string.IsNullOrWhiteSpace(longLivedToken.AccessToken)
            ? longLivedToken.AccessToken
            : tokenResult.AccessToken;

        var discovered = await DiscoverLinkedAccountAsync(accessToken, payload.ChannelType, payload.SaveFullLog, payload.FarmaciaId);
        if (!discovered.Success)
        {
            return new MetaOAuthConnectionResult(
                false,
                discovered.Message ?? "Conexão concluída, mas não foi possível descobrir os dados vinculados.",
                accessToken,
                null,
                null,
                null,
                null,
                null,
                null,
                discovered.ErrorCode);
        }

        return discovered with { AccessToken = accessToken };
    }

    private async Task<MetaTokenResult> ExchangeCodeForTokenAsync(
        string code,
        string appId,
        MetaOAuthChannelType channelType,
        MetaConfigSource configSource,
        string? appSecretOverride,
        bool saveFullLog,
        int? farmaciaId)
    {
        var appSecret = ResolveAppSecret(channelType, configSource, appSecretOverride);
        if (string.IsNullOrWhiteSpace(appSecret))
            return MetaTokenResult.Fail(GetAppSecretErrorMessage(configSource), "missing_app_secret");

        var redirectUri = NormalizeRedirectUri();
        if (channelType == MetaOAuthChannelType.Instagram)
        {
            return await PostInstagramCodeForTokenAsync(code, appId, appSecret, redirectUri, saveFullLog, farmaciaId);
        }

        var url =
            $"{Consts.MetaApi.BaseUrl.TrimEnd('/')}/{_config.ApiVersion}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(appId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&code={Uri.EscapeDataString(code)}";

        return await GetTokenAsync(url, saveFullLog, farmaciaId, "facebook-code-exchange");
    }

    private async Task<MetaTokenResult> ExchangeForLongLivedTokenAsync(
        string shortLivedToken,
        string appId,
        MetaOAuthChannelType channelType,
        MetaConfigSource configSource,
        string? appSecretOverride,
        bool saveFullLog,
        int? farmaciaId)
    {
        var appSecret = ResolveAppSecret(channelType, configSource, appSecretOverride);
        if (string.IsNullOrWhiteSpace(appSecret))
            return MetaTokenResult.Fail(GetAppSecretErrorMessage(configSource), "missing_app_secret");

        if (channelType == MetaOAuthChannelType.Instagram)
        {
            var instagramUrl =
                $"https://graph.instagram.com/access_token" +
                $"?grant_type=ig_exchange_token" +
                $"&client_secret={Uri.EscapeDataString(appSecret)}" +
                $"&access_token={Uri.EscapeDataString(shortLivedToken)}";

            return await GetTokenAsync(instagramUrl, saveFullLog, farmaciaId, "instagram-long-lived-exchange");
        }

        var url =
            $"{Consts.MetaApi.BaseUrl.TrimEnd('/')}/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(appId)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";

        return await GetTokenAsync(url, saveFullLog, farmaciaId, "facebook-long-lived-exchange");
    }

    private async Task<MetaTokenResult> PostInstagramCodeForTokenAsync(string code, string appId, string appSecret, string redirectUri, bool saveFullLog, int? farmaciaId)
    {
        var client = _httpClientFactory.CreateClient(Consts.MetaApi.ClientName);
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = appId,
                ["client_secret"] = appSecret,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code"] = code
            });

            var response = await client.PostAsync("https://api.instagram.com/oauth/access_token", content);
            var body = await response.Content.ReadAsStringAsync();
            if (saveFullLog)
            {
                _logger.LogInformation(
                    "Meta OAuth [logCompleto] instagram-code-exchange | farmaciaId={FarmaciaId} | status={Status} | response={Response}",
                    farmaciaId,
                    (int)response.StatusCode,
                    body);
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram OAuth token request failed: {Status} {Body}", response.StatusCode, body);
                return MetaTokenResult.Fail("Falha ao trocar o código por token.", "token_exchange_failed");
            }

            var token = DeserializeTokenResponse(body);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
                return MetaTokenResult.Fail("Resposta inválida da Meta.", "invalid_token_response");

            return MetaTokenResult.Ok(token.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Instagram OAuth token");
            return MetaTokenResult.Fail("Erro ao comunicar com a Meta.", "token_exchange_exception");
        }
    }

    private async Task<MetaTokenResult> GetTokenAsync(string url, bool saveFullLog, int? farmaciaId, string operation)
    {
        var client = _httpClientFactory.CreateClient(Consts.MetaApi.ClientName);
        try
        {
            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (saveFullLog)
            {
                _logger.LogInformation(
                    "Meta OAuth [logCompleto] {Operation} | farmaciaId={FarmaciaId} | url={Url} | status={Status} | response={Response}",
                    operation,
                    farmaciaId,
                    SanitizeUrl(url),
                    (int)response.StatusCode,
                    body);
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta OAuth token request failed: {Status} {Body}", response.StatusCode, body);
                return MetaTokenResult.Fail("Falha ao trocar o código por token.", "token_exchange_failed");
            }

            var token = DeserializeTokenResponse(body);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
                return MetaTokenResult.Fail("Resposta inválida da Meta.", "invalid_token_response");

            return MetaTokenResult.Ok(token.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Meta OAuth token");
            return MetaTokenResult.Fail("Erro ao comunicar com a Meta.", "token_exchange_exception");
        }
    }

    private async Task<MetaOAuthConnectionResult> DiscoverLinkedAccountAsync(string accessToken, MetaOAuthChannelType channelType, bool saveFullLog, int? farmaciaId)
    {
        if (channelType == MetaOAuthChannelType.Instagram)
            return await DiscoverInstagramAccountAsync(accessToken, saveFullLog, farmaciaId);

        var client = _httpClientFactory.CreateClient(Consts.MetaApi.ClientName);
        var fields = "id,name,access_token,instagram_business_account{id,username},whatsapp_business_account{id,name,phone_numbers{id,display_phone_number,verified_name}}";
        var requestUrl = $"{Consts.MetaApi.BaseUrl.TrimEnd('/')}/{_config.ApiVersion}/me/accounts?fields={Uri.EscapeDataString(fields)}&access_token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var response = await client.GetAsync(requestUrl);
            var body = await response.Content.ReadAsStringAsync();
            if (saveFullLog)
            {
                _logger.LogInformation(
                    "Meta OAuth [logCompleto] discover-linked-account | farmaciaId={FarmaciaId} | url={Url} | status={Status} | response={Response}",
                    farmaciaId,
                    SanitizeUrl(requestUrl),
                    (int)response.StatusCode,
                    body);
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta OAuth /me/accounts failed: {Status} {Body}", response.StatusCode, body);
                return new MetaOAuthConnectionResult(
                    true,
                    "Login concluído, mas não foi possível consultar as contas vinculadas. Revise os campos antes de salvar.",
                    accessToken,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                return new MetaOAuthConnectionResult(
                    true,
                    "Login concluído, mas a Meta não retornou contas vinculadas. Revise os campos antes de salvar.",
                    accessToken,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            foreach (var page in data.EnumerateArray())
            {
                var pageId = GetString(page, "id");
                var pageName = GetString(page, "name");
                var pageAccessToken = GetString(page, "access_token");

                if (channelType == MetaOAuthChannelType.Instagram &&
                    page.TryGetProperty("instagram_business_account", out var instagramAccount) &&
                    instagramAccount.ValueKind == JsonValueKind.Object)
                {
                    var instagramBusinessId = GetString(instagramAccount, "id");
                    if (!string.IsNullOrWhiteSpace(instagramBusinessId))
                    {
                        return new MetaOAuthConnectionResult(
                            true,
                            "Instagram conectado com sucesso.",
                            !string.IsNullOrWhiteSpace(pageAccessToken) ? pageAccessToken : accessToken,
                            pageId,
                            pageName,
                            instagramBusinessId,
                            null,
                            null,
                            null);
                    }
                }

                if (channelType == MetaOAuthChannelType.WhatsApp &&
                    page.TryGetProperty("whatsapp_business_account", out var whatsappAccount) &&
                    whatsappAccount.ValueKind == JsonValueKind.Object)
                {
                    var wabaId = GetString(whatsappAccount, "id");
                    var phoneNumberId = GetFirstPhoneNumberId(whatsappAccount);
                    if (!string.IsNullOrWhiteSpace(wabaId) || !string.IsNullOrWhiteSpace(phoneNumberId))
                    {
                        return new MetaOAuthConnectionResult(
                            true,
                            "WhatsApp conectado com sucesso.",
                            !string.IsNullOrWhiteSpace(pageAccessToken) ? pageAccessToken : accessToken,
                            pageId,
                            pageName,
                            null,
                            wabaId,
                            phoneNumberId,
                            wabaId);
                    }
                }
            }

            return channelType == MetaOAuthChannelType.Instagram
                ? new MetaOAuthConnectionResult(true, "Login concluído. Nenhuma conta Instagram vinculada foi encontrada; preencha o Instagram Business ID manualmente.", accessToken, null, null, null, null, null, null)
                : new MetaOAuthConnectionResult(true, "Login concluído. Nenhuma conta WhatsApp vinculada foi encontrada; preencha os IDs manualmente.", accessToken, null, null, null, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering linked Meta account");
            return new MetaOAuthConnectionResult(
                true,
                "Login concluído, mas houve erro ao consultar as contas vinculadas. Revise os campos antes de salvar.",
                accessToken,
                null,
                null,
                null,
                null,
                null,
                null,
                "account_discovery_failed");
        }
    }

    private async Task<MetaOAuthConnectionResult> DiscoverInstagramAccountAsync(string accessToken, bool saveFullLog, int? farmaciaId)
    {
        var client = _httpClientFactory.CreateClient(Consts.MetaApi.ClientName);
        var requestUrl = $"https://graph.instagram.com/{_config.ApiVersion}/me?fields=user_id,username,name,account_type&access_token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var response = await client.GetAsync(requestUrl);
            var body = await response.Content.ReadAsStringAsync();
            if (saveFullLog)
            {
                _logger.LogInformation(
                    "Meta OAuth [logCompleto] instagram-discover-account | farmaciaId={FarmaciaId} | url={Url} | status={Status} | response={Response}",
                    farmaciaId,
                    SanitizeUrl(requestUrl),
                    (int)response.StatusCode,
                    body);
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta OAuth Instagram /me failed: {Status} {Body}", response.StatusCode, body);
                return new MetaOAuthConnectionResult(
                    true,
                    "Login concluído, mas não foi possível consultar a conta do Instagram vinculada. Revise os campos antes de salvar.",
                    accessToken,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            using var document = JsonDocument.Parse(body);
            var account = TryGetInstagramMePayload(document.RootElement);
            var instagramBusinessId = GetString(account, "user_id") ?? GetString(account, "id");
            var username = GetString(account, "username");
            var displayName = GetString(account, "name") ?? username;

            if (!string.IsNullOrWhiteSpace(instagramBusinessId))
            {
                return new MetaOAuthConnectionResult(
                    true,
                    "Instagram conectado com sucesso.",
                    accessToken,
                    null,
                    displayName,
                    instagramBusinessId,
                    null,
                    null,
                    null);
            }

            return new MetaOAuthConnectionResult(
                true,
                "Login concluído. Nenhuma conta Instagram vinculada foi encontrada; preencha o Instagram Business ID manualmente.",
                accessToken,
                null,
                displayName,
                null,
                null,
                null,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Instagram account via Meta OAuth");
            return new MetaOAuthConnectionResult(
                true,
                "Login concluído, mas houve erro ao consultar a conta do Instagram vinculada. Revise os campos antes de salvar.",
                accessToken,
                null,
                null,
                null,
                null,
                null,
                null,
                "instagram_account_discovery_failed");
        }
    }

    private static string GetScopes(MetaOAuthChannelType channelType)
    {
        return channelType switch
        {
            MetaOAuthChannelType.Instagram =>
                "instagram_business_basic,instagram_business_manage_messages",
            _ =>
                "whatsapp_business_messaging,whatsapp_business_management,pages_show_list,pages_manage_metadata,business_management"
        };
    }

    private string? ResolveAppId(MetaOAuthChannelType channelType, MetaConfigSource configSource, MetaAppMode appMode, string? appIdOverride)
    {
        if (configSource == MetaConfigSource.Own)
            return string.IsNullOrWhiteSpace(appIdOverride) ? null : appIdOverride.Trim();

        if (channelType == MetaOAuthChannelType.Instagram && !string.IsNullOrWhiteSpace(_config.InstagramAppId))
            return _config.InstagramAppId.Trim();

        return appMode switch
        {
            MetaAppMode.Test => Consts.MetaApi.TestAppId,
            MetaAppMode.Production => Consts.MetaApi.ProductionAppId,
            _ => null
        };
    }

    private string? ResolveAppSecret(MetaOAuthChannelType channelType, MetaConfigSource configSource, string? appSecretOverride)
    {
        if (configSource == MetaConfigSource.Own)
            return string.IsNullOrWhiteSpace(appSecretOverride) ? null : appSecretOverride.Trim();

        if (channelType == MetaOAuthChannelType.Instagram && !string.IsNullOrWhiteSpace(_config.InstagramAppSecret))
            return _config.InstagramAppSecret.Trim();

        return string.IsNullOrWhiteSpace(_config.AppSecret)
            ? null
            : _config.AppSecret.Trim();
    }

    private string NormalizeRedirectUri()
    {
        if (!string.IsNullOrWhiteSpace(_config.RedirectUri))
            return _config.RedirectUri.Trim();

        var baseUrl = _configuration["Application:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "https://localhost:5051/api/meta/oauth/callback";

        return $"{baseUrl.TrimEnd('/')}/api/meta/oauth/callback";
    }

    private static string CacheKey(string state) => $"{CachePrefix}{state}";

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    private static JsonElement TryGetInstagramMePayload(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array &&
            data.GetArrayLength() > 0)
        {
            return data[0];
        }

        return root;
    }

    private static string? GetFirstPhoneNumberId(JsonElement whatsappAccount)
    {
        if (!whatsappAccount.TryGetProperty("phone_numbers", out var phoneNumbers) || phoneNumbers.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var phoneNumber in phoneNumbers.EnumerateArray())
        {
            var id = GetString(phoneNumber, "id");
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return null;
    }

    private static MetaAccessTokenResponse? DeserializeTokenResponse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array &&
            data.GetArrayLength() > 0)
        {
            root = data[0];
        }

        var accessToken = GetString(root, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var tokenType = GetString(root, "token_type");
        int? expiresIn = null;
        if (root.TryGetProperty("expires_in", out var expiresProp) && expiresProp.TryGetInt32(out var expiresValue))
        {
            expiresIn = expiresValue;
        }

        return new MetaAccessTokenResponse(accessToken, tokenType, expiresIn);
    }

    private static string SanitizeUrl(string url)
    {
        return url
            .Replace("&client_secret=", "&client_secret=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("?client_secret=", "?client_secret=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("&access_token=", "&access_token=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("?access_token=", "?access_token=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("&fb_exchange_token=", "&fb_exchange_token=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("?fb_exchange_token=", "?fb_exchange_token=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("&code=", "&code=[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("?code=", "?code=[redacted]", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAppIdErrorMessage(MetaConfigSource configSource)
        => configSource == MetaConfigSource.Own
            ? "Meta App ID não informado."
            : "Modo do app Meta inválido.";

    private static string GetAppSecretErrorMessage(MetaConfigSource configSource)
        => configSource == MetaConfigSource.Own
            ? "Meta App Secret não configurado."
            : "Meta OAuth AppSecret não configurado.";

    private sealed record MetaTokenResult(bool Success, string? AccessToken, string? ErrorMessage = null, string? ErrorCode = null)
    {
        public static MetaTokenResult Ok(string accessToken) => new(true, accessToken);
        public static MetaTokenResult Fail(string message, string code) => new(false, null, message, code);
    }

    private sealed record MetaAccessTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string? TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int? ExpiresIn);
}
