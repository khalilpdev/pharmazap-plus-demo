using FluentAssertions;
using PharmazapPlus.Shared.Extensions;

namespace PharmazapPlus.API.Tests.Extensions;

public class StringExtensionTests
{
    [Fact]
    public void FormatTelefone_WithTenDigits_UsesFourDigitsAfterHyphen()
    {
        var result = "6195178597".FormatTelefone();

        result.Should().Be("(61) 9517-8597");
    }

    [Fact]
    public void FormatTelefone_WithElevenDigits_KeepsMobileMask()
    {
        var result = "61995178597".FormatTelefone();

        result.Should().Be("(61) 99517-8597");
    }

    [Fact]
    public void IsEquivalentWhatsAppIdentifier_WithSameLastEightDigits_ReturnsTrue()
    {
        var result = "5511912345678".IsEquivalentWhatsAppIdentifier("5531912345678");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsEquivalentWhatsAppIdentifier_WithDifferentLastEightDigits_ReturnsFalse()
    {
        var result = "5511912345678".IsEquivalentWhatsAppIdentifier("5531912349999");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetWhatsAppLastEightDigits_WithCountryCodeAndMask_ReturnsNormalizedSuffix()
    {
        var result = "+55 (11) 91234-5678".GetWhatsAppLastEightDigits();

        result.Should().Be("12345678");
    }
}
