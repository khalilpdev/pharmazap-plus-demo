using System.Security.Cryptography;
using System.Text;

namespace PharmazapPlus.API.Services.Security;

/// <summary>
/// Serviço de criptografia AES-256-CBC para dados confidenciais (ex: CPF).
/// A chave deve ser fornecida via variável de ambiente PHARMAZAP_ENCRYPTION_KEY
/// (32 bytes codificados em Base64) ou pela configuração Encryption:Key.
/// </summary>
public class CryptoService : ICryptoService
{
    private const int IvSizeBytes = 16;
    private const int KeySizeBytes = 32; // 256 bits

    private readonly byte[] _key;

    public CryptoService(byte[] key)
    {
        if (key is null || key.Length != KeySizeBytes)
            throw new ArgumentException($"A chave AES deve ter exatamente {KeySizeBytes} bytes (256 bits).", nameof(key));

        _key = key;
    }

    /// <inheritdoc/>
    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
        aes.GenerateIV();
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, iv);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prefixar IV ao texto cifrado e codificar em Base64
        var result = new byte[IvSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, IvSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, result, IvSizeBytes, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc/>
    public string Decrypt(string encryptedText)
    {
        ArgumentException.ThrowIfNullOrEmpty(encryptedText);

        var fullCipher = Convert.FromBase64String(encryptedText);

        if (fullCipher.Length <= IvSizeBytes)
            throw new ArgumentException("Dados criptografados inválidos.", nameof(encryptedText));

        var iv = new byte[IvSizeBytes];
        var cipherBytes = new byte[fullCipher.Length - IvSizeBytes];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, IvSizeBytes);
        Buffer.BlockCopy(fullCipher, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Cria uma instância de <see cref="CryptoService"/> a partir de uma chave codificada em Base64.
    /// </summary>
    public static CryptoService FromBase64Key(string base64Key)
    {
        ArgumentException.ThrowIfNullOrEmpty(base64Key);
        var key = Convert.FromBase64String(base64Key);
        return new CryptoService(key);
    }

    /// <summary>
    /// Gera uma nova chave AES-256 aleatória e retorna sua representação em Base64.
    /// Útil para gerar a chave inicial em ambiente de desenvolvimento.
    /// </summary>
    public static string GenerateBase64Key()
    {
        var key = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
