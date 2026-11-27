using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;
        private const string AUTH_COOKIE_NAME = "PharmazapAuth";
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILocalStorageService _localStorage;
        private readonly ThemeService _themeService;

        public UserInfo? CurrentUser { get; private set; }

        public AuthService(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger,
            AuthenticationStateProvider authenticationStateProvider,
            ILocalStorageService localStorage,
            ThemeService themeService)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _authStateProvider = authenticationStateProvider;
            _localStorage = localStorage;
            _themeService = themeService;
        }

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        public async Task Initialize()
        {
            // Restore user from stored data (no API call needed here)
            var storedUser = await _localStorage.GetItemAsync<UserInfo>(Consts.Web.UserStorageKey);
            if (storedUser != null && storedUser.Expiration > DateTime.UtcNow)
            {
                var token = await _localStorage.GetItemAsync<string>(Consts.Web.TokenStorageKey);
                if (!string.IsNullOrEmpty(token))
                {
                    storedUser.Token = token;
                    storedUser.IsAuthenticated = true;
                    CurrentUser = storedUser;
                    _themeService.SetThemeFromString(CurrentUser.Theme);
                    _themeService.SetAccentColor(CurrentUser.AccentColor);
                }
                else
                {
                    _logger.LogWarning("Auth initialization skipped: token not found in storage.");
                }
            }
        }

        public bool HasRole(params string[] roles)
        {
            return CurrentUser != null && roles.Contains(CurrentUser.Role);
        }

        public async Task<bool> Login(string email, string password)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);

                var loginRequest = new { email, password };
                var response = await client.PostAsJsonAsync(Consts.Endpoints.Login, loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    // Check for server errors first
                    if ((int)response.StatusCode >= 500)
                    {   
                        _logger.LogError("Server error during login for {Email}: {StatusCode}", email, response.StatusCode);
                        throw new HttpRequestException($"Erro no servidor ({(int)response.StatusCode}). Tente novamente mais tarde.");
                    }

                    // Try to read the error message from the API
                    string errorMessage = "Credenciais inválidas";
                    try
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var errorDoc = JsonDocument.Parse(errorContent);
                        if (errorDoc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            var message = messageElement.GetString();
                            if (!string.IsNullOrEmpty(message))
                            {
                                errorMessage = message;
                            }
                        }
                    }
                    catch
                    {
                        // If parsing fails, keep the default message
                    }

                    _logger.LogWarning("Login failed for {Email}: {ErrorMessage}", email, errorMessage);
                    throw new UnauthorizedAccessException(errorMessage);
                }

                var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (loginResult?.Success == true && loginResult.Data is { } data)
                {
                    // ✅ Criar o CurrentUser
                    CurrentUser = new UserInfo
                    {
                        Id = data.Id,
                        Email = data.Email,
                        Name = data.Name,
                        Role = data.Role,
                        FarmaciaId = data.FarmaciaId,
                        FarmaciaNome = data.FarmaciaNome,
                        FarmaciaLogoUrl = data.FarmaciaLogoUrl,
                        Token = data.Token,
                        Expiration = data.Expiration,
                        Theme = data.Theme,
                        AccentColor = data.AccentColor,
                        AvatarUrl = "",
                        IsAuthenticated = true,
                        TermoAceiteRequired = data.TermoAceiteRequired
                    };

                    // ✅ Persistir token e usuário no storage (sobrevive a page reload)
                    await _localStorage.SetItemAsync(Consts.Web.TokenStorageKey, data.Token);
                    await _localStorage.SetItemAsync(Consts.Web.UserStorageKey, CurrentUser);

                    // ✅ Aplicar tema do usuário
                    _themeService.SetThemeFromString(CurrentUser.Theme);
                    _themeService.SetAccentColor(CurrentUser.AccentColor);

                    // ✅ Configurar o token no HttpClient padrão
                    var defaultClient = _httpClientFactory.CreateClient();
                    defaultClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", data.Token);

                    // ✅ Configurar o token no HttpClient da API
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", data.Token);

                    // ✅ Notificar o AuthenticationStateProvider
                    if (_authStateProvider is PharmazapAuthStateProvider provider)
                    {
                        provider.SetUser(CurrentUser);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for {email} [{ex.Message}]");
                throw;
            }
        }

        public async Task Logout()
        {
            // ✅ Remover token e usuário do LocalStorage
            await _localStorage.RemoveItemAsync(Consts.Web.TokenStorageKey);
            await _localStorage.RemoveItemAsync(Consts.Web.UserStorageKey);

            // ✅ Limpar o header Authorization
            var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);
            client.DefaultRequestHeaders.Authorization = null;

            var defaultClient = _httpClientFactory.CreateClient();
            defaultClient.DefaultRequestHeaders.Authorization = null;

            // ✅ Limpar o CurrentUser
            CurrentUser = null;

            // ✅ Notificar o AuthenticationStateProvider
            if (_authStateProvider is PharmazapAuthStateProvider provider)
            {
                provider.SetUser(null);
            }

            _logger.LogInformation("User logged out");
        }


        public async Task<UserInfo?> GetUserInfo()
        {
            if (CurrentUser?.IsAuthenticated == true)
                return CurrentUser;

            // ✅ Tentar restaurar o usuário do storage (salvo durante o login)
            try
            {
                var storedUser = await _localStorage.GetItemAsync<UserInfo>(Consts.Web.UserStorageKey);
                if (storedUser != null)
                {
                    var token = await _localStorage.GetItemAsync<string>(Consts.Web.TokenStorageKey);
                    if (!string.IsNullOrEmpty(token) && storedUser.Expiration > DateTime.UtcNow)
                    {
                        storedUser.Token = token;
                        storedUser.IsAuthenticated = true;
                        CurrentUser = storedUser;
                        return CurrentUser;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error restoring user from storage [{ex.Message}]");
            }

            // Fallback: buscar do endpoint da API
            var fallbackToken = await _localStorage.GetItemAsync<string>(Consts.Web.TokenStorageKey);
            if (string.IsNullOrEmpty(fallbackToken))
                return null;

            try
            {
                var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", fallbackToken);

                var response = await client.GetAsync(Consts.Endpoints.UserInfo);
                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<UserInfoResponse>();
                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        CurrentUser = new UserInfo
                        {
                            Id = apiResponse.Data.Id ?? "",
                            Email = apiResponse.Data.Email ?? "",
                            Name = apiResponse.Data.Name ?? "",
                            Role = apiResponse.Data.Role ?? "",
                            FarmaciaId = apiResponse.Data.FarmaciaId ?? "",
                            FarmaciaNome = apiResponse.Data.FarmaciaNome ?? "",
                            FarmaciaLogoUrl = apiResponse.Data.FarmaciaLogoUrl,
                            Token = fallbackToken,
                            AvatarUrl = "",
                            IsAuthenticated = true
                        };
                        return CurrentUser;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user info from API [{ex.Message}]");
            }

            return null;
        }

        public async Task UpdateTheme(string theme)
        {
            if (CurrentUser == null) return;

            try
            {
                var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", CurrentUser.Token);

                var response = await client.PutAsJsonAsync(Consts.Endpoints.UpdateTheme, new { theme });
                if (response.IsSuccessStatusCode)
                {
                    // Re-check CurrentUser after async operation to prevent race condition
                    if (CurrentUser == null)
                    {
                        _logger.LogWarning("CurrentUser became null during UpdateTheme operation");
                        return;
                    }

                    CurrentUser.Theme = theme;
                    await _localStorage.SetItemAsync(Consts.Web.UserStorageKey, CurrentUser);
                }
                else
                {
                    _logger.LogWarning("Failed to update theme on server. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating theme [{ex.Message}]");
            }
        }

        public async Task UpdateAccentColor(string? accentColor)
        {
            if (CurrentUser == null) return;

            try
            {
                var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", CurrentUser.Token);

                var response = await client.PutAsJsonAsync(Consts.Endpoints.UpdateAccentColor, new { accentColor });
                if (response.IsSuccessStatusCode)
                {
                    if (CurrentUser == null)
                    {
                        _logger.LogWarning("CurrentUser became null during UpdateAccentColor operation");
                        return;
                    }

                    CurrentUser.AccentColor = accentColor;
                    await _localStorage.SetItemAsync(Consts.Web.UserStorageKey, CurrentUser);
                }
                else
                {
                    _logger.LogWarning("Failed to update accent color on server. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating accent color [{ex.Message}]");
            }
        }

        public async Task UpdateFarmaciaLogoUrl(string? logoUrl)        {
            if (CurrentUser == null) return;
            CurrentUser.FarmaciaLogoUrl = logoUrl;
            await _localStorage.SetItemAsync(Consts.Web.UserStorageKey, CurrentUser);
            OnFarmaciaLogoUrlChanged?.Invoke();
        }

        public async Task<bool> AceitarTermos()
        {
            if (CurrentUser == null) return false;

            try
            {
                var client = _httpClientFactory.CreateClient(Consts.Web.ApiClientName);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", CurrentUser.Token);

                var response = await client.PostAsync(Consts.Endpoints.AceitarTermos, null);
                if (response.IsSuccessStatusCode)
                {
                    CurrentUser.TermoAceiteRequired = false;
                    await _localStorage.SetItemAsync(Consts.Web.UserStorageKey, CurrentUser);
                    return true;
                }

                _logger.LogWarning("Failed to accept terms. Status: {Status}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting terms [{Message}]", ex.Message);
                return false;
            }
        }

        public event Action? OnFarmaciaLogoUrlChanged;
    }
}
