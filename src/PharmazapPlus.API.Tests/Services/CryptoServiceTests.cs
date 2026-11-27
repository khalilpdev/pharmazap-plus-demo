using FluentAssertions;
using PharmazapPlus.API.Services.Security;

namespace PharmazapPlus.API.Tests.Services;

public class CryptoServiceTests
{
    private static CryptoService CreateService()
    {
        var key = new byte[32];
        for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
        return new CryptoService(key);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidKey_CreatesInstance()
    {
        // Arrange
        var key = new byte[32];

        // Act
        var service = new CryptoService(key);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithShortKey_ThrowsArgumentException()
    {
        // Arrange
        var shortKey = new byte[16];

        // Act
        var act = () => new CryptoService(shortKey);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullKey_ThrowsArgumentException()
    {
        // Act
        var act = () => new CryptoService(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Encrypt
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_WithValidCpf_ReturnsBase64String()
    {
        // Arrange
        var service = CreateService();
        const string cpf = "12345678909";

        // Act
        var encrypted = service.Encrypt(cpf);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(encrypted);
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Encrypt_SamePlainText_ReturnsDifferentCipherTexts()
    {
        // Arrange
        var service = CreateService();
        const string cpf = "12345678909";

        // Act – IV é gerado aleatoriamente a cada chamada
        var encrypted1 = service.Encrypt(cpf);
        var encrypted2 = service.Encrypt(cpf);

        // Assert
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Encrypt_WithNullInput_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Encrypt(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encrypt_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Encrypt(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Decrypt
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_WithValidEncryptedCpf_ReturnsOriginalValue()
    {
        // Arrange
        var service = CreateService();
        const string cpf = "12345678909";

        // Act
        var encrypted = service.Encrypt(cpf);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(cpf);
    }

    [Fact]
    public void Decrypt_WithNullInput_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ThrowsFormatException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Decrypt("not-valid-base64!!!");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        var serviceA = CreateService();

        var differentKey = new byte[32];
        for (int i = 0; i < differentKey.Length; i++) differentKey[i] = (byte)(i + 10);
        var serviceB = new CryptoService(differentKey);

        const string cpf = "12345678909";
        var encrypted = serviceA.Encrypt(cpf);

        // Act
        var act = () => serviceB.Decrypt(encrypted);

        // Assert
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Round-trip
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("12345678909")]
    [InlineData("000.000.000-01")]
    [InlineData("98765432100")]
    [InlineData("João da Silva — CPF com caracteres especiais: áéíóú")]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginalValue(string plainText)
    {
        // Arrange
        var service = CreateService();

        // Act
        var decrypted = service.Decrypt(service.Encrypt(plainText));

        // Assert
        decrypted.Should().Be(plainText);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Factory helpers
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FromBase64Key_WithValidKey_CreatesInstance()
    {
        // Arrange
        var base64Key = CryptoService.GenerateBase64Key();

        // Act
        var service = CryptoService.FromBase64Key(base64Key);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void GenerateBase64Key_ReturnsValidBase64Of32Bytes()
    {
        // Act
        var base64Key = CryptoService.GenerateBase64Key();

        // Assert
        base64Key.Should().NotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(base64Key);
        bytes.Should().HaveCount(32);
    }

    [Fact]
    public void GenerateBase64Key_CalledTwice_ReturnsDifferentKeys()
    {
        // Act
        var key1 = CryptoService.GenerateBase64Key();
        var key2 = CryptoService.GenerateBase64Key();

        // Assert
        key1.Should().NotBe(key2);
    }
}
