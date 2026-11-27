using FluentAssertions;
using PharmazapPlus.API.Services.WhatsApp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Tests.Services;

public class EvolutionGoCryptoServiceTests
{
    private static readonly EvolutionGoCryptoService Service = new();

    // ──────────────────────────────────────────────────────────────────────────
    // ExtractMediaKey
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractMediaKey_WithPascalCaseProperty_ReturnsKey()
    {
        var json = JsonDocument.Parse("""{"mediaKey":"ABC123def456=="}""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().Be("ABC123def456==");
    }

    [Fact]
    public void ExtractMediaKey_WithCamelCaseProperty_ReturnsKey()
    {
        var json = JsonDocument.Parse("""{"mediaKey":"ABC123def456=="}""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().Be("ABC123def456==");
    }

    [Fact]
    public void ExtractMediaKey_WithNullProperty_ReturnsNull()
    {
        var json = JsonDocument.Parse("""{"mediaKey":null}""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMediaKey_WithMissingProperty_ReturnsNull()
    {
        var json = JsonDocument.Parse("""{"otherKey":"value"}""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMediaKey_WithEmptyString_ReturnsNull()
    {
        var json = JsonDocument.Parse("""{"mediaKey":""}""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMediaKey_WithNonObjectJson_ReturnsNull()
    {
        var json = JsonDocument.Parse("""["item"]""").RootElement;
        var result = Service.ExtractMediaKey(json);
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DecryptMedia — round-trip
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image", "WhatsApp Image Keys")]
    [InlineData("sticker", "WhatsApp Image Keys")]
    [InlineData("video", "WhatsApp Video Keys")]
    [InlineData("audio", "WhatsApp Audio Keys")]
    [InlineData("document", "WhatsApp Document Keys")]
    public void DecryptMedia_WithValidKey_DecryptsCorrectly(string mediaType, string expectedContext)
    {
        // Arrange
        var original = "Hello MMS3 World!"u8.ToArray();
        var (mediaKeyBase64, encrypted) = EncryptRoundTrip(original, expectedContext);

        // Act
        var decrypted = Service.DecryptMedia(encrypted, mediaKeyBase64, mediaType);

        // Assert
        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DecryptMedia_WithUnknownMediaType_FallsBackToImageKeys()
    {
        var original = "fallback test"u8.ToArray();
        var (mediaKeyBase64, encrypted) = EncryptRoundTrip(original, "WhatsApp Image Keys");

        var decrypted = Service.DecryptMedia(encrypted, mediaKeyBase64, "application/pdf");

        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DecryptMedia_WithTruncatedHmacSuffix_DecryptsCorrectly()
    {
        var original = "payload with hmac"u8.ToArray();
        var (mediaKeyBase64, encrypted) = EncryptRoundTrip(original, "WhatsApp Image Keys");
        var payloadWithHmac = new byte[encrypted.Length + 10];
        Buffer.BlockCopy(encrypted, 0, payloadWithHmac, 0, encrypted.Length);
        var hmacSuffix = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        hmacSuffix.AsSpan().CopyTo(payloadWithHmac.AsSpan(encrypted.Length));

        var decrypted = Service.DecryptMedia(payloadWithHmac, mediaKeyBase64, "image");

        decrypted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DecryptMedia_WithNullMediaKey_ReturnsNull()
    {
        var result = Service.DecryptMedia([1, 2, 3], null, "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithEmptyMediaKey_ReturnsNull()
    {
        var result = Service.DecryptMedia([1, 2, 3], "", "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithWhitespaceMediaKey_ReturnsNull()
    {
        var result = Service.DecryptMedia([1, 2, 3], "   ", "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithNullEncryptedData_ReturnsNull()
    {
        var result = Service.DecryptMedia(null!, "ABC123", "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithEmptyEncryptedData_ReturnsNull()
    {
        var result = Service.DecryptMedia([], "ABC123", "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithInvalidBase64MediaKey_ReturnsNull()
    {
        var result = Service.DecryptMedia([1, 2, 3], "!!!invalid-base64!!!", "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithTamperedCiphertext_ReturnsNull()
    {
        var original = "tamper test"u8.ToArray();
        var (mediaKeyBase64, encrypted) = EncryptRoundTrip(original, "WhatsApp Image Keys");

        encrypted[^1] ^= 0xFF;

        var result = Service.DecryptMedia(encrypted, mediaKeyBase64, "image");
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptMedia_WithNonBlockPayloadAndInvalidRemainder_ReturnsNull()
    {
        var original = "invalid remainder test"u8.ToArray();
        var (mediaKeyBase64, encrypted) = EncryptRoundTrip(original, "WhatsApp Image Keys");
        var invalidPayload = new byte[encrypted.Length + 1];
        Buffer.BlockCopy(encrypted, 0, invalidPayload, 0, encrypted.Length);
        invalidPayload[^1] = 1;

        var result = Service.DecryptMedia(invalidPayload, mediaKeyBase64, "image");

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static (string MediaKeyBase64, byte[] Encrypted) EncryptRoundTrip(
        byte[] plaintext, string context)
    {
        var mediaKey = RandomNumberGenerator.GetBytes(32);
        var mediaKeyBase64 = Convert.ToBase64String(mediaKey);

        var salt = Encoding.UTF8.GetBytes(context);
        var info = Encoding.UTF8.GetBytes(context);
        var derived = HKDF.DeriveKey(HashAlgorithmName.SHA256, mediaKey, 80, salt, info);

        var cipherKey = derived.AsSpan(0, 32).ToArray();
        var iv = derived.AsSpan(64, 16).ToArray();

        using var aes = Aes.Create();
        aes.Key = cipherKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        return (mediaKeyBase64, encrypted);
    }
}
