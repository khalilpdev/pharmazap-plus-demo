using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.MetaOAuth;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/meta/oauth")]
[Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Desenvolvedor}")]
public class MetaOAuthController : ControllerBase
{
    private readonly IMetaOAuthService _metaOAuthService;
    private readonly PharmazapDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetaOAuthController> _logger;

    public MetaOAuthController(
        IMetaOAuthService metaOAuthService,
        PharmazapDbContext context,
        IConfiguration configuration,
        ILogger<MetaOAuthController> logger)
    {
        _metaOAuthService = metaOAuthService;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] MetaOAuthStartHttpRequest request)
    {
        if (!IsValidMetaConfigSource(request.ConfigSource))
        {
            return BadRequest(new
            {
                success = false,
                error = "Origem da configuração Meta inválida."
            });
        }

        if (request.ConfigSource == MetaConfigSource.PharmazapCentral && !Enum.IsDefined(request.AppMode))
        {
            return BadRequest(new
            {
                success = false,
                error = "Ambiente Meta inválido."
            });
        }

        var farmaciaClaim = HttpContext?.User?.FindFirst("FarmaciaId")?.Value;
        var farmaciaId = int.TryParse(farmaciaClaim, out var parsedFarmaciaId) && parsedFarmaciaId > 0
            ? parsedFarmaciaId
            : (int?)null;
        var saveFullLog = farmaciaId.HasValue && await GetSalvarLogCompletoAsync(farmaciaId.Value);
        var appId = request.AppId?.Trim();
        var appSecret = request.AppSecret?.Trim();

        if (request.ConfigSource == MetaConfigSource.Own)
        {
            if (farmaciaId.HasValue && request.ChannelId.HasValue && request.ChannelId.Value > 0)
            {
                var storedCredentials = await GetStoredMetaCredentialsAsync(farmaciaId.Value, request.ChannelId.Value);
                if (string.IsNullOrWhiteSpace(appId))
                    appId = storedCredentials.AppId;
                if (string.IsNullOrWhiteSpace(appSecret))
                    appSecret = storedCredentials.AppSecret;
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Meta App ID é obrigatório para usar suas próprias configurações."
                });
            }

            if (string.IsNullOrWhiteSpace(appSecret))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Meta App Secret é obrigatório para usar suas próprias configurações."
                });
            }
        }

        var result = await _metaOAuthService.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            request.ChannelType,
            request.ConfigSource,
            request.AppMode,
            appId,
            appSecret,
            farmaciaId,
            saveFullLog));
        if (!result.Success || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
        {
            return BadRequest(new
            {
                success = false,
                error = result.Error ?? result.Message ?? "Não foi possível iniciar a conexão com a Meta."
            });
        }

        return Ok(new
        {
            success = true,
            authorizationUrl = result.AuthorizationUrl,
            state = result.State,
            message = result.Message
        });
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription)
    {
        MetaOAuthConnectionResult result;

        if (!string.IsNullOrWhiteSpace(error))
        {
            result = new MetaOAuthConnectionResult(false, errorDescription ?? error, null, null, null, null, null, null, null, error);
        }
        else
        {
            result = await _metaOAuthService.CompleteAuthorizationAsync(code ?? string.Empty, state ?? string.Empty);
        }

        var webOrigin = GetWebOrigin();
        var payload = JsonSerializer.Serialize(new
        {
            type = "meta-oauth-result",
            payload = result
        });

        var shouldCloseAutomatically = result.Success;
        var html = $$"""
<!doctype html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Meta OAuth</title>
    <style>
        body{font-family:Arial,sans-serif;padding:2rem;line-height:1.5;background:#f7f7f8;color:#222;}
        .card{max-width:720px;margin:0 auto;background:#fff;border:1px solid #d9d9dd;border-radius:12px;padding:1.5rem;box-shadow:0 8px 24px rgba(0,0,0,.08);}
        .title{margin:0 0 .75rem 0;}
        .message{white-space:pre-wrap;margin:0 0 1rem 0;}
        .actions{margin-top:1.25rem;}
        button{border:0;border-radius:8px;padding:.75rem 1rem;background:#0f6cbd;color:#fff;cursor:pointer;font:inherit;}
    </style>
</head>
<body>
    <div class="card">
        <h3 class="title">{{(result.Success ? "Conexão concluída" : "Falha na conexão")}}</h3>
        <p class="message">{{HtmlEncode(result.Message)}}</p>
        <p>{{(result.Success ? "A janela será fechada automaticamente." : "Revise o erro acima e tente novamente.")}}</p>
        <div class="actions">
            <button type="button" onclick="window.close();">Fechar janela</button>
        </div>
    </div>
    <script>
        (function() {
            const message = {{payload}};
            try {
                if (window.opener) {
                    window.opener.postMessage(message, {{JsonSerializer.Serialize(webOrigin)}});
                    if ({{shouldCloseAutomatically.ToString().ToLowerInvariant()}}) {
                        window.close();
                    }
                }
            } catch (e) {
                console.error(e);
            }
        })();
    </script>
</body>
</html>
""";

        return Content(html, "text/html", Encoding.UTF8);
    }

    private string GetWebOrigin()
    {
        var webUrl = _configuration["Application:WebUrl"];
        if (string.IsNullOrWhiteSpace(webUrl))
            return "https://localhost:7160";

        return new Uri(webUrl).GetLeftPart(UriPartial.Authority);
    }

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private async Task<bool> GetSalvarLogCompletoAsync(int farmaciaId)
    {
        var integracoes = await _context.FarmaciaIntegracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FarmaciaId == farmaciaId);

        return integracoes?.SalvarLogCompletoMidiaApiGo ?? false;
    }

    private async Task<(string? AppId, string? AppSecret)> GetStoredMetaCredentialsAsync(int farmaciaId, int channelId)
    {
        var stored = await _context.Canais
            .AsNoTracking()
            .Where(c => c.Id == channelId && c.FarmaciaIntegracoes != null && c.FarmaciaIntegracoes.FarmaciaId == farmaciaId)
            .Select(c => new
            {
                c.MetaAppId,
                c.MetaAppSecret
            })
            .FirstOrDefaultAsync();

        return stored == null
            ? (null, null)
            : (stored.MetaAppId?.Trim(), stored.MetaAppSecret?.Trim());
    }

    private static bool IsValidMetaConfigSource(MetaConfigSource configSource)
        => configSource is MetaConfigSource.Own or MetaConfigSource.PharmazapCentral;
}
