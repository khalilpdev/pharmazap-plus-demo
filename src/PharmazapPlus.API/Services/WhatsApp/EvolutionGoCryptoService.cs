using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Services.WhatsApp;

public class EvolutionGoCryptoService : IEvolutionGoCryptoService
{
    private const int AesBlockSizeBytes = 16;
    private const int TruncatedHmacSizeBytes = 10;

    private static readonly HashSet<string> ImageTypes = ["image", "sticker"];
    private static readonly HashSet<string> VideoTypes = ["video"];
    private static readonly HashSet<string> AudioTypes = ["audio"];
    private static readonly HashSet<string> DocumentTypes = ["document"];

    public string? ExtractMediaKey(JsonElement mediaPayload)
    {
        if (mediaPayload.ValueKind != JsonValueKind.Object)
            return null;

        if (TryGetString(mediaPayload, "mediaKey", out var key) && !string.IsNullOrWhiteSpace(key))
            return key;

        return null;
    }

    public byte[]? DecryptMedia(byte[] encryptedData, string? mediaKeyBase64, string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaKeyBase64) || encryptedData is null || encryptedData.Length == 0)
            return null;

        try
        {
            var mediaKey = Convert.FromBase64String(mediaKeyBase64);
            var context = ResolveMediaContext(mediaType);
            if (string.IsNullOrWhiteSpace(context))
                return null;

            var salt = Encoding.UTF8.GetBytes(context);
            var info = Encoding.UTF8.GetBytes(context);

            byte[] derived;
            try
            {
                derived = HKDF.DeriveKey(HashAlgorithmName.SHA256, mediaKey, 80, salt, info);
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            var cipherKey = derived.AsSpan(0, 32).ToArray();
            var iv = derived.AsSpan(64, 16).ToArray();
            var ciphertext = PrepareCiphertext(encryptedData);
            if (ciphertext is null)
                return null;

            using var aes = Aes.Create();
            aes.Key = cipherKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return decrypted;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string ResolveMediaContext(string mediaType)
    {
        var normalized = mediaType?.Trim().ToLowerInvariant() ?? string.Empty;

        if (ImageTypes.Contains(normalized))
            return "WhatsApp Image Keys";
        if (VideoTypes.Contains(normalized))
            return "WhatsApp Video Keys";
        if (AudioTypes.Contains(normalized))
            return "WhatsApp Audio Keys";
        if (DocumentTypes.Contains(normalized))
            return "WhatsApp Document Keys";

        return "WhatsApp Image Keys";
    }

    /// <summary>
    /// Prepares encrypted media payload for AES-CBC decryption.
    /// Removes the trailing 10-byte truncated HMAC when present and
    /// validates that the remaining ciphertext is block-aligned.
    /// </summary>
    /// <param name="encryptedData">Encrypted payload from CDN/Evolution.</param>
    /// <returns>
    /// Ciphertext ready for decryption, or null when payload format is invalid.
    /// </returns>
    private static byte[]? PrepareCiphertext(byte[] encryptedData)
    {
        if (encryptedData.Length % AesBlockSizeBytes == 0)
            return encryptedData;

        if (encryptedData.Length <= TruncatedHmacSizeBytes)
            return null;

        if (encryptedData.Length % AesBlockSizeBytes != TruncatedHmacSizeBytes)
            return null;

        var ciphertextLength = encryptedData.Length - TruncatedHmacSizeBytes;
        if (ciphertextLength <= 0 || ciphertextLength % AesBlockSizeBytes != 0)
            return null;

        return encryptedData.AsSpan(0, ciphertextLength).ToArray();
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        var camelName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(camelName, out prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        return false;
    }
}
