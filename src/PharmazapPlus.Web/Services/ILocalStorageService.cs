namespace PharmazapPlus.Web.Services;

/// <summary>
/// Service for managing protected local storage in Blazor using built-in capabilities
/// </summary>
public interface ILocalStorageService
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}
