namespace PharmazapPlus.API.Services.Security;

public interface ICryptoService
{
    /// <summary>
    /// Criptografa um texto simples usando AES-256.
    /// Retorna o IV concatenado com o texto cifrado, codificados em Base64.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Descriptografa um texto cifrado previamente gerado por <see cref="Encrypt"/>.
    /// </summary>
    string Decrypt(string encryptedText);
}
