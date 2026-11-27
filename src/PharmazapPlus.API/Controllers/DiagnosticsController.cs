using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/diagnostics")]
[Authorize(Roles = UserRoles.Administrador)]
public class DiagnosticsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IWebHostEnvironment environment, ILogger<DiagnosticsController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("channel-logs")]
    public IActionResult GetChannelLogs([FromQuery] string source = "all", [FromQuery] int limit = 200)
    {
        var normalizedSource = (source ?? "all").Trim().ToLowerInvariant();
        if (limit < 1)
            limit = 1;
        if (limit > 500)
            limit = 500;

        var logDirectory = ResolveLogDirectory();
        if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
        {
            return Ok(new
            {
                success = false,
                source = normalizedSource,
                error = "Diretório de logs não encontrado.",
                lines = Array.Empty<string>()
            });
        }

        var logFile = Directory.EnumerateFiles(logDirectory, "pharmazapplus-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(logFile) || !System.IO.File.Exists(logFile))
        {
            return Ok(new
            {
                success = false,
                source = normalizedSource,
                error = "Nenhum arquivo de log diário foi encontrado.",
                lines = Array.Empty<string>()
            });
        }

        var filters = GetFilters(normalizedSource);
        var matches = new Queue<string>();
        var totalMatches = 0;

        foreach (var line in System.IO.File.ReadLines(logFile))
        {
            if (filters.Length > 0 && !filters.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                continue;

            totalMatches++;
            matches.Enqueue(line);
            while (matches.Count > limit)
                matches.Dequeue();
        }

        var lines = matches.ToArray();
        _logger.LogInformation(
            "Channel logs requested | source={Source} | file={File} | matches={Matches} | returned={Returned}",
            normalizedSource,
            Path.GetFileName(logFile),
            totalMatches,
            lines.Length);

        return Ok(new
        {
            success = true,
            source = normalizedSource,
            fileName = Path.GetFileName(logFile),
            logDirectory,
            totalMatches,
            returned = lines.Length,
            lines
        });
    }

    private string ResolveLogDirectory()
    {
        var gdcLogDirectory = GlobalDiagnosticsContext.Get("LogDirectory");
        if (!string.IsNullOrWhiteSpace(gdcLogDirectory))
            return gdcLogDirectory;

        var contentRoot = _environment.ContentRootPath;
        return Path.Combine(contentRoot, "logs");
    }

    private static string[] GetFilters(string source)
    {
        return source switch
        {
            "instagram-meta" => new[] { "Instagram", "InstagramService", "MetaOAuth", "Graph API", "instagram" },
            "whatsapp-meta" => new[] { "WhatsApp Oficial", "MetaOAuth", "ChannelsController", "PhoneNumberId", "Business Account", "whatsapp" },
            "meta" => new[] { "MetaOAuth", "MetaApp", "MetaConfigSource", "OAuth", "Facebook", "Graph API", "Meta " },
            "evolution" => new[] { "Evolution", "Evolution Go", "GenerateQrCode", "GeneratePairingCode", "EnsureWhatsAppInstance", "qrcode", "pairing" },
            _ => Array.Empty<string>()
        };
    }
}
