using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Shared.Extensions;

public static class StringExtension
{
    private const int DefaultWhatsAppDigitsLimit = 13;

    /// <summary>
    /// Remove acentos da string.
    /// </summary>
    public static string NoAccents(this string pValue)
    {
        if (string.IsNullOrWhiteSpace(pValue))
            return string.Empty;

        string normalizedString = pValue.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < normalizedString.Length; i++)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(normalizedString[i]);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(normalizedString[i]);
            }
        }
        return (sb.ToString().Normalize(NormalizationForm.FormC));
    }

    /// <summary>
    /// Valida se a string é um endereço de e-mail válido.
    /// </summary>
    public static bool IsValidEmail(this string inputEmail)
    {
        if (string.IsNullOrWhiteSpace(inputEmail))
            return false;

        string strRegex = @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}" +
              @"\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\" +
              @".)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$";

        Regex re = new Regex(strRegex);
        return re.IsMatch(inputEmail);
    }

    /// <summary>
    /// Formata o identificador de contato de acordo com o canal.
    /// WhatsApp/Telegram: número formatado via FormatTelefone.
    /// Instagram/Messenger: @username (sem formatação numérica).
    /// </summary>
    public static string FormatContactId(this string? input, CanalEnum canal)
    {
        if (string.IsNullOrEmpty(input)) return "";

        return canal switch
        {
            CanalEnum.Instagram => input.StartsWith("@") ? input : $"@{input}",
            CanalEnum.Messenger => input.StartsWith("@") ? input : $"@{input}",
            _ => input.FormatTelefone()
        };
    }

    /// <summary>
    /// Valida se a string é um número de telefone brasileiro válido.
    /// Aceita números com ou sem máscara. Mínimo 10 dígitos (fixo) e máximo 11 (celular).
    /// Remove automaticamente o prefixo do país (55) se presente.
    /// </summary>
    public static bool IsValidTelefone(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        // Remove prefixo do país (55) se presente (ex: 5511987654321 → 11987654321)
        if (digits.StartsWith("55") && digits.Length >= 12)
            digits = digits[2..];
        return digits.Length >= 10 && digits.Length <= 11;
    }

    /// <summary>
    /// Analisa uma string de moeda no formato brasileiro (e.g. "15", "15,00", "R$ 1.500,00")
    /// e retorna o valor decimal correspondente. Retorna null se a string estiver vazia ou
    /// não puder ser analisada.
    /// </summary>
    public static decimal? ParseBrCurrency(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var clean = value
            .Replace("R$", "")
            .Replace(" ", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(clean)) return null;

        var hasDot = clean.Contains('.');
        var hasComma = clean.Contains(',');

        if (!hasDot && !hasComma)
        {
            return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var intVal) && intVal > 0
                ? intVal
                : null;
        }

        if (hasDot && hasComma)
        {
            var lastDot = clean.LastIndexOf('.');
            var lastComma = clean.LastIndexOf(',');
            var decimalSeparator = lastDot > lastComma ? '.' : ',';
            var thousandsSeparator = decimalSeparator == '.' ? ',' : '.';

            clean = string.Concat(clean.Split(thousandsSeparator));
            clean = clean.Replace(decimalSeparator, '.');
        }
        else
        {
            var separator = hasComma ? ',' : '.';
            var parts = clean.Split(separator);
            var isDecimalInput = parts.Length == 2 && parts[1].Length is > 0 and <= 2;

            clean = isDecimalInput
                ? clean.Replace(separator, '.')
                : string.Concat(clean.Split(separator));
        }

        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) && result > 0
            ? result
            : null;
    }

    /// <summary>
    /// Formata um valor decimal como moeda no padrão brasileiro sem o prefixo "R$"
    /// (e.g. 15 → "15,00", 1500 → "1.500,00").
    /// </summary>
    public static string FormatBrCurrency(this decimal value)
        => value.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// Aplica máscara monetária brasileira em tempo real interpretando os dígitos digitados como centavos.
    /// Ex.: "1" → "R$ 0,01", "1000" → "R$ 10,00". Se a entrada já estiver formatada
    /// (ex.: "R$ 10,00"), reaproveita apenas os dígitos e mantém o valor monetário esperado.
    /// </summary>
    public static string FormatBrCurrencyInput(this string? value, int maxDigits = 12)
    {
        var digits = value.SanitizeDigits(maxDigits);
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var cents) && cents > 0
            ? (cents / 100m).ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))
            : string.Empty;
    }

    /// <summary>
    /// Formata um número de telefone brasileiro para exibição.
    /// Remove o prefixo do país (55) quando presente e aplica a máscara (XX) XXXXX-XXXX.
    /// </summary>
    public static string FormatTelefone(this string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        var digits = new string(input.Where(char.IsDigit).ToArray());

        // Strip Brazilian country code (55) when present (e.g. 5511987654321 = 13 digits → 11987654321)
        // Brazilian numbers: mobile = 11 digits, landline = 10 digits → with "55" = 13 or 12 digits
        if (digits.StartsWith("55") && digits.Length >= 12)
            digits = digits[2..];

        // Limit to 11 digits (mobile) or 10 (landline)
        digits = new string(digits.Take(11).ToArray());

        int len = digits.Length;
        if (len == 0) return "";
        if (len <= 2) return digits;

        var ddd = digits[..2];
        var num = digits[2..];

        if (num.Length == 0) return $"({ddd})";
        if (num.Length <= 5) return $"({ddd}) {num}";
        if (num.Length <= 8) return $"({ddd}) {num[..4]}-{num[4..]}";
        return $"({ddd}) {num[..5]}-{num[5..]}";
    }

    /// <summary>
    /// Remove todos os caracteres não numéricos e limita a quantidade máxima de dígitos.
    /// </summary>
    public static string SanitizeDigits(this string? input, int maxDigits)
    {
        return new string((input ?? "").Where(char.IsDigit).Take(maxDigits).ToArray());
    }

    /// <summary>
    /// Aplica máscara de WhatsApp no mesmo formato do telefone: "(11) 99999-9999".
    /// Remove o prefixo do país "55" quando presente para exibição.
    /// </summary>
    public static string MaskWhatsApp(this string? input, int maxDigits = DefaultWhatsAppDigitsLimit)
    {
        var digits = input.SanitizeDigits(maxDigits);
        // Strip country code "55" prefix so the display matches the phone mask format
        if (digits.StartsWith("55", StringComparison.Ordinal) && digits.Length >= 12)
            digits = digits[2..];
        return digits.FormatTelefone();
    }

    /// <summary>
    /// Aplica máscara de telefone brasileiro no formato "(11) 99999-9999".
    /// </summary>
    public static string MaskTelefoneInput(this string? input)
    {
        return input.SanitizeDigits(11).FormatTelefone();
    }

    /// <summary>
    /// Aplica máscara de CNPJ no formato "00.000.000/0000-00".
    /// </summary>
    public static string MaskCnpj(this string? input)
    {
        var digits = input.SanitizeDigits(14);
        int len = digits.Length;
        if (len == 0) return "";
        if (len <= 2) return digits;
        if (len <= 5) return $"{digits[..2]}.{digits[2..]}";
        if (len <= 8) return $"{digits[..2]}.{digits[2..5]}.{digits[5..]}";
        if (len <= 12) return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..]}";
        return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
    }

    /// <summary>
    /// Normaliza identificador WhatsApp removendo máscara, prefixo do país (55)
    /// e o 9º dígito opcional (quando aplicável).
    /// </summary>
    public static string NormalizeWhatsAppIdentifier(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("55", StringComparison.Ordinal) && digits.Length >= 12)
            digits = digits[2..];

        return RemoveOptionalNinthDigit(digits);
    }

    /// <summary>
    /// Verifica se dois identificadores WhatsApp representam o mesmo contato.
    /// </summary>
    public static bool IsEquivalentWhatsAppIdentifier(this string? left, string? right)
    {
        var normalizedLeft = left.NormalizeWhatsAppIdentifier();
        var normalizedRight = right.NormalizeWhatsAppIdentifier();

        if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
            return false;

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
            return true;

        var leftLastEightDigits = normalizedLeft.GetWhatsAppLastEightDigits();
        var rightLastEightDigits = normalizedRight.GetWhatsAppLastEightDigits();

        if (leftLastEightDigits.Length < 8 || rightLastEightDigits.Length < 8)
            return false;

        return string.Equals(leftLastEightDigits, rightLastEightDigits, StringComparison.Ordinal);
    }

    /// <summary>
    /// Retorna a chave de comparação WhatsApp baseada nos últimos 8 dígitos numéricos.
    /// Se houver 8 dígitos ou menos após a normalização, retorna o valor completo normalizado.
    /// </summary>
    public static string GetWhatsAppLastEightDigits(this string? input)
    {
        var normalized = input.NormalizeWhatsAppIdentifier();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        return normalized.Length <= 8 ? normalized : normalized[^8..];
    }

    /// <summary>
    /// Seleciona o identificador WhatsApp mais informativo entre o atual e o recebido.
    /// </summary>
    public static string SelectPreferredWhatsAppIdentifier(this string? current, string? incoming)
    {
        var normalizedCurrent = current.NormalizeWhatsAppIdentifier();
        var normalizedIncoming = incoming.NormalizeWhatsAppIdentifier();

        if (string.IsNullOrWhiteSpace(normalizedCurrent))
            return normalizedIncoming;

        if (string.IsNullOrWhiteSpace(normalizedIncoming))
            return normalizedCurrent;

        if (!normalizedCurrent.IsEquivalentWhatsAppIdentifier(normalizedIncoming))
            return normalizedCurrent;

        return normalizedIncoming.Length > normalizedCurrent.Length
            ? normalizedIncoming
            : normalizedCurrent;
    }

    private static string RemoveOptionalNinthDigit(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        if (digits.Length == 11 && digits[2] == '9')
            return digits.Remove(2, 1);

        if (digits.Length == 9 && digits[0] == '9')
            return digits[1..];

        return digits;
    }

    /// <summary>
    /// Aplica máscara de CPF no formato "000.000.000-00".
    /// </summary>
    public static string MaskCpf(this string? input)
    {
        var digits = input.SanitizeDigits(11);
        int len = digits.Length;
        if (len == 0) return "";
        if (len <= 3) return digits;
        if (len <= 6) return $"{digits[..3]}.{digits[3..]}";
        if (len <= 9) return $"{digits[..3]}.{digits[3..6]}.{digits[6..]}";
        return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
    }

    /// <summary>
    /// Aplica máscara de CEP no formato "00000-000".
    /// </summary>
    public static string MaskCep(this string? input)
    {
        var digits = input.SanitizeDigits(8);
        int len = digits.Length;
        if (len == 0) return "";
        if (len <= 5) return digits;
        return $"{digits[..5]}-{digits[5..]}";
    }

    /// <summary>
    /// Valida se a string representa um CNPJ válido (dígitos verificadores corretos).
    /// Aceita CNPJ com ou sem máscara.
    /// </summary>
    public static bool IsValidCnpj(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var cnpj = new string(input.Where(char.IsDigit).ToArray());

        if (cnpj.Length != 14) return false;

        // Rejeita CNPJs com todos os dígitos iguais (ex: 00000000000000)
        if (cnpj.Distinct().Count() == 1) return false;

        // Primeiro dígito verificador
        int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int soma = 0;
        for (int i = 0; i < 12; i++)
            soma += (cnpj[i] - '0') * mult1[i];
        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        if ((cnpj[12] - '0') != digito1) return false;

        // Segundo dígito verificador
        int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        soma = 0;
        for (int i = 0; i < 13; i++)
            soma += (cnpj[i] - '0') * mult2[i];
        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return (cnpj[13] - '0') == digito2;
    }
}
