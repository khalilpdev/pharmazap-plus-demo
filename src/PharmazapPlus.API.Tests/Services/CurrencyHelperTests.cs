using FluentAssertions;
using PharmazapPlus.Shared.Extensions;

namespace PharmazapPlus.API.Tests.Services;

/// <summary>
/// Testes para os helpers de moeda brasileira em StringExtension.
/// Cobre os casos do campo "Valor da venda" no diálogo de encerramento de atendimento.
/// </summary>
public class CurrencyHelperTests
{
    // ── ParseBrCurrency ───────────────────────────────────────────────

    [Fact]
    public void ParseBrCurrency_PlainInteger_ReturnsCorrectDecimal()
    {
        // Usuário digita "15" sem formatar — esperado: 15,00
        "15".ParseBrCurrency().Should().Be(15m);
    }

    [Fact]
    public void ParseBrCurrency_PlainLargeInteger_ReturnsCorrectDecimal()
    {
        "1500".ParseBrCurrency().Should().Be(1500m);
    }

    [Fact]
    public void ParseBrCurrency_WithCommaDecimal_ReturnsCorrectDecimal()
    {
        "15,00".ParseBrCurrency().Should().Be(15m);
    }

    [Fact]
    public void ParseBrCurrency_WithDotDecimal_ReturnsCorrectDecimal()
    {
        "15.50".ParseBrCurrency().Should().Be(15.50m);
    }

    [Fact]
    public void ParseBrCurrency_WithThousandsSeparator_ReturnsCorrectDecimal()
    {
        "1.500,00".ParseBrCurrency().Should().Be(1500m);
    }

    [Fact]
    public void ParseBrCurrency_WithRPrefix_ReturnsCorrectDecimal()
    {
        "R$ 99,90".ParseBrCurrency().Should().Be(99.90m);
    }

    [Fact]
    public void ParseBrCurrency_WithRPrefixAndThousands_ReturnsCorrectDecimal()
    {
        "R$ 1.200,50".ParseBrCurrency().Should().Be(1200.50m);
    }

    [Fact]
    public void ParseBrCurrency_EmptyString_ReturnsNull()
    {
        "".ParseBrCurrency().Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ParseBrCurrency_NullOrWhitespace_ReturnsNull(string? input)
    {
        input.ParseBrCurrency().Should().BeNull();
    }

    [Fact]
    public void ParseBrCurrency_ZeroValue_ReturnsNull()
    {
        // Valor zero não é aceito como venda válida
        "0".ParseBrCurrency().Should().BeNull();
        "0,00".ParseBrCurrency().Should().BeNull();
    }

    [Fact]
    public void ParseBrCurrency_InvalidString_ReturnsNull()
    {
        "abc".ParseBrCurrency().Should().BeNull();
    }

    // ── FormatBrCurrency ──────────────────────────────────────────────

    [Fact]
    public void FormatBrCurrency_WholeNumber_ReturnsTwoDecimalPlaces()
    {
        15m.FormatBrCurrency().Should().Be("15,00");
    }

    [Fact]
    public void FormatBrCurrency_LargeValue_UsesThousandsSeparator()
    {
        1500m.FormatBrCurrency().Should().Be("1.500,00");
    }

    [Fact]
    public void FormatBrCurrency_ValueWithCents_FormatsCorrectly()
    {
        99.90m.FormatBrCurrency().Should().Be("99,90");
    }

    [Fact]
    public void FormatBrCurrencyInput_SingleDigit_FormatsAsCents()
    {
        "1".FormatBrCurrencyInput().Should().Be("R$ 0,01");
    }

    [Fact]
    public void FormatBrCurrencyInput_IntegerDigits_FormatsImmediately()
    {
        "1500".FormatBrCurrencyInput().Should().Be("R$ 15,00");
    }

    [Fact]
    public void FormatBrCurrencyInput_WithExistingCurrencyText_KeepsExpectedValue()
    {
        "R$ 10,00".FormatBrCurrencyInput().Should().Be("R$ 10,00");
    }

    [Fact]
    public void FormatBrCurrencyInput_EmptyString_ReturnsEmpty()
    {
        "".FormatBrCurrencyInput().Should().BeEmpty();
    }

    // ── Round-trip: parse then format ────────────────────────────────

    [Theory]
    [InlineData("15", "15,00")]
    [InlineData("15,00", "15,00")]
    [InlineData("1.500,00", "1.500,00")]
    [InlineData("99,90", "99,90")]
    [InlineData("R$ 250,00", "250,00")]
    public void ParseThenFormat_RoundTrip_ProducesExpectedOutput(string input, string expected)
    {
        var parsed = input.ParseBrCurrency();
        parsed.Should().NotBeNull();
        parsed!.Value.FormatBrCurrency().Should().Be(expected);
    }
}
