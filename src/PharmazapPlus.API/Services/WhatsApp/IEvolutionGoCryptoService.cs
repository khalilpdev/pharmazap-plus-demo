using System.Text.Json;

namespace PharmazapPlus.API.Services.WhatsApp;

public interface IEvolutionGoCryptoService
{
    string? ExtractMediaKey(JsonElement mediaPayload);
    byte[]? DecryptMedia(byte[] encryptedData, string? mediaKeyBase64, string mediaType);
}
