using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PharmazapPlus.Shared.Models;
using System.Security.Claims;

namespace PharmazapPlus.Web.Services;

public class PharmazapAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _protectedLocalStorage;
    private readonly ILogger<PharmazapAuthStateProvider> _logger;
    private UserInfo? _currentUser;
    private bool _initialized;

    public PharmazapAuthStateProvider(
        ProtectedLocalStorage protectedLocalStorage,
        ILogger<PharmazapAuthStateProvider> logger)
    {
        _protectedLocalStorage = protectedLocalStorage;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentUser is null && !_initialized)
        {
            try
            {
                // Try to restore auth state from persistent storage (only works in interactive mode)
                var tokenResult = await _protectedLocalStorage.GetAsync<string>("pharmazap_token");
                var userResult = await _protectedLocalStorage.GetAsync<UserInfo>("pharmazap_user");

                if (tokenResult.Success && userResult.Success && userResult.Value is { } storedUser
                    && !string.IsNullOrEmpty(tokenResult.Value)
                    && storedUser.Expiration > DateTime.UtcNow)
                {
                    storedUser.Token = tokenResult.Value;
                    _currentUser = storedUser;
                }

                _initialized = true;
            }
            catch (InvalidOperationException)
            {
                // JS interop is not available during prerendering - return anonymous
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error restoring auth state from storage [{ex.Message}]");
            }
        }

        if (_currentUser is { } user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id ?? ""),
                new(ClaimTypes.Name, user.Name ?? ""),
                new(ClaimTypes.Email, user.Email ?? ""),
                new(ClaimTypes.Role, user.Role ?? ""),
            };

            if (!string.IsNullOrEmpty(user.FarmaciaId))
                claims.Add(new Claim("FarmaciaId", user.FarmaciaId));

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void SetUser(UserInfo? user)
    {
        _currentUser = user;
        _initialized = true;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Gets the current user info stored in the auth state provider.
    /// </summary>
    public UserInfo? GetCurrentUser() => _currentUser;
}
