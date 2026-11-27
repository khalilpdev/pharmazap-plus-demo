using System.Globalization;

namespace PharmazapPlus.API.Extensions;

public static class NLogDailyFileRetention
{
    private const int DefaultRetentionDays = 30;
    private const string LogDirectoryName = "logs";
    private const string DailyLogPrefix = "pharmazapplus-";
    private const string DailyLogPattern = $"{DailyLogPrefix}*.log";

    public static void EnsureRetention(string basePath, int retentionDays = DefaultRetentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        if (retentionDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), retentionDays, "A retenção de logs deve ser de pelo menos um dia.");
        }

        var logsDirectory = Path.Combine(basePath, LogDirectoryName);
        Directory.CreateDirectory(logsDirectory);

        var oldestDateToKeep = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(retentionDays - 1)));

        foreach (var logFilePath in Directory.EnumerateFiles(logsDirectory, DailyLogPattern, SearchOption.TopDirectoryOnly))
        {
            if (!TryGetLogDate(logFilePath, out var logDate) || logDate >= oldestDateToKeep)
            {
                continue;
            }

            File.Delete(logFilePath);
        }
    }

    private static bool TryGetLogDate(string logFilePath, out DateOnly logDate)
    {
        var fileName = Path.GetFileNameWithoutExtension(logFilePath);
        var datePart = fileName[DailyLogPrefix.Length..];

        return DateOnly.TryParseExact(
            datePart,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out logDate);
    }
}
