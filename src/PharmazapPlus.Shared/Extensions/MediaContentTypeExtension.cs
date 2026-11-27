namespace PharmazapPlus.Shared.Extensions;

public static class MediaContentTypeExtension
{
    public static string? NormalizeAllowedMediaContentType(this string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        var normalized = contentType.Trim().ToLowerInvariant();
        var semicolon = normalized.IndexOf(';');
        if (semicolon >= 0)
            normalized = normalized[..semicolon].Trim();

        return normalized.StartsWith("image/", StringComparison.Ordinal) ||
               normalized.StartsWith("video/", StringComparison.Ordinal) ||
               normalized.StartsWith("audio/", StringComparison.Ordinal) ||
               normalized is "application/pdf"
                or "application/msword"
                or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.ms-excel"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "text/plain"
            ? normalized
            : null;
    }
}
