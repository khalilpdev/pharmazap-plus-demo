using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace PharmazapPlus.Web.Services
{
    public class LocalStorageService : ILocalStorageService
    {
        private readonly ProtectedLocalStorage _protectedLocalStorage;

        public LocalStorageService(ProtectedLocalStorage protectedLocalStorage)
        {
            _protectedLocalStorage = protectedLocalStorage;
        }

        public async Task<T?> GetItemAsync<T>(string key)
        {
            try
            {
                var result = await _protectedLocalStorage.GetAsync<T>(key);
                return result.Success ? result.Value : default;
            }
            catch
            {
                return default;
            }
        }

        public async Task SetItemAsync<T>(string key, T? value)
        {
            if (value is not null)
                await _protectedLocalStorage.SetAsync(key, value);
        }

        public async Task RemoveItemAsync(string key)
        {
            await _protectedLocalStorage.DeleteAsync(key);
        }
    }
}