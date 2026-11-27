using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/configuracoes")]
[Authorize(Roles = $"{UserRoles.Gestor},{UserRoles.Administrador},{UserRoles.Desenvolvedor}")]
public class ConfiguracoesController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<ConfiguracoesController> _logger;
    private readonly IRemetenteBloqueadoService _remetenteBloqueadoService;

    public ConfiguracoesController(
        PharmazapDbContext context,
        ILogger<ConfiguracoesController> logger,
        IRemetenteBloqueadoService remetenteBloqueadoService)
    {
        _context = context;
        _logger = logger;
        _remetenteBloqueadoService = remetenteBloqueadoService;
    }

    [HttpGet("midia-api-go-log-completo")]
    public async Task<IActionResult> GetSalvarLogCompletoMidiaApiGo()
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        return Ok(new
        {
            success = true,
            data = new
            {
                salvarLogCompletoMidiaApiGo = farmacia.Integracoes?.SalvarLogCompletoMidiaApiGo ?? false
            }
        });
    }

    [HttpPut("midia-api-go-log-completo")]
    public async Task<IActionResult> UpdateSalvarLogCompletoMidiaApiGo([FromBody] UpdateSalvarLogCompletoMidiaApiGoRequest request)
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        if (farmacia.Integracoes == null)
        {
            farmacia.Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = farmacia.Id,
                ChaveWhatsApp = Guid.NewGuid().ToString("N")[..12].ToUpper()
            };
            _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
        }

        farmacia.Integracoes.SalvarLogCompletoMidiaApiGo = request.Enabled;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Configuração de log completo de mídia API GO atualizada para farmácia {FarmaciaId}: {Enabled}",
            farmaciaId,
            request.Enabled);

        return Ok(new
        {
            success = true,
            message = "Configuração atualizada com sucesso"
        });
    }

    [HttpGet("habilitar-bloqueio-remetente")]
    public async Task<IActionResult> GetHabilitarBloqueioRemetente()
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        return Ok(new
        {
            success = true,
            habilitarBloqueioRemetente = farmacia.Integracoes?.HabilitarBloqueioRemetente ?? false
        });
    }

    [HttpPut("habilitar-bloqueio-remetente")]
    [Authorize(Roles = $"{UserRoles.Gestor},{UserRoles.Administrador}")]
    public async Task<IActionResult> UpdateHabilitarBloqueioRemetente([FromBody] UpdateHabilitarBloqueioRemetenteRequest request)
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        if (farmacia.Integracoes == null)
        {
            farmacia.Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = farmacia.Id,
                ChaveWhatsApp = Guid.NewGuid().ToString("N")[..12].ToUpper()
            };
            _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
        }

        farmacia.Integracoes.HabilitarBloqueioRemetente = request.Enabled;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Configuração de bloqueio de remetentes atualizada para farmácia {FarmaciaId}: {Enabled}",
            farmaciaId,
            request.Enabled);

        return Ok(new
        {
            success = true,
            message = "Configuração atualizada com sucesso"
        });
    }

    [HttpGet("permitir-gerenciar-etiquetas-atendente")]
    public async Task<IActionResult> GetPermitirGerenciarEtiquetasAtendente()
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        return Ok(new
        {
            success = true,
            permitirGerenciarEtiquetasAtendente = farmacia.Integracoes?.PermitirGerenciarEtiquetasAtendente ?? false
        });
    }

    [HttpPut("permitir-gerenciar-etiquetas-atendente")]
    [Authorize(Roles = UserRoles.Administrador)]
    public async Task<IActionResult> UpdatePermitirGerenciarEtiquetasAtendente([FromBody] UpdatePermitirGerenciarEtiquetasAtendenteRequest request)
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId) || farmaciaId <= 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var farmacia = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstOrDefaultAsync(f => f.Id == farmaciaId);

        if (farmacia == null)
            return NotFound(new { success = false, error = "Farmácia não encontrada" });

        if (farmacia.Integracoes == null)
        {
            farmacia.Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = farmacia.Id,
                ChaveWhatsApp = Guid.NewGuid().ToString("N")[..12].ToUpper()
            };
            _context.FarmaciaIntegracoes.Add(farmacia.Integracoes);
        }

        farmacia.Integracoes.PermitirGerenciarEtiquetasAtendente = request.Enabled;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Configuração de gestão de etiquetas por atendente atualizada para farmácia {FarmaciaId}: {Enabled}",
            farmaciaId,
            request.Enabled);

        return Ok(new
        {
            success = true,
            message = "Configuração atualizada com sucesso"
        });
    }

    /// <summary>
    /// Lista remetentes bloqueados da farmácia (locais + globais).
    /// Atendentes veem apenas bloqueios locais; Admin/Gestor veem ambos.
    /// </summary>
    [HttpGet("remetentes-bloqueados")]
    public async Task<IActionResult> ListarRemetentesBlockeados([FromQuery] string? escopo = null)
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId))
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            IList<RemetenteBloqueado> bloqueados;

            if (userRole == UserRoles.Atendente)
            {
                // Atendente vê apenas bloqueios locais da sua farmácia
                bloqueados = await _remetenteBloqueadoService.ListarBloqueadosAsync(farmaciaId, apenasAtivos: false);
                bloqueados = bloqueados.Where(r => r.EhLocal).ToList();
            }
            else if (string.IsNullOrEmpty(escopo) || escopo == "Local")
            {
                // Admin/Gestor: vê bloqueios locais por padrão
                bloqueados = await _remetenteBloqueadoService.ListarBloqueadosAsync(farmaciaId, apenasAtivos: false);
            }
            else if (escopo == "Global")
            {
                // Admin/Gestor: pode filtrar por globais
                bloqueados = await _remetenteBloqueadoService.ListarBloqueadosGlobaisAsync(apenasAtivos: false);
            }
            else
            {
                return BadRequest(new { success = false, error = "Escopo inválido. Use 'Local' ou 'Global'" });
            }

            var resposta = bloqueados.Select(r => new
            {
                r.Id,
                r.NumeroNormalizado,
                r.Escopo,
                r.MotivoBloqueia,
                DataBloqueio = r.DataBloqueio.ToString("yyyy-MM-dd HH:mm:ss"),
                r.NomeBloqueadoPor,
                DataDesbloqueio = r.DataDesbloqueio?.ToString("yyyy-MM-dd HH:mm:ss"),
                r.NomeDesbloqueadoPor,
                r.Ativo
            }).ToList();

            return Ok(new
            {
                success = true,
                data = resposta,
                total = resposta.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar remetentes bloqueados");
            return StatusCode(500, new { success = false, error = "Erro ao listar remetentes bloqueados" });
        }
    }

    /// <summary>
    /// Desbloqueia um remetente.
    /// </summary>
    [HttpDelete("remetentes-bloqueados/{id}")]
    public async Task<IActionResult> DesbloqueRemetente(int id)
    {
        var farmaciaClaim = User.FindFirst("FarmaciaId")?.Value;
        if (!int.TryParse(farmaciaClaim, out var farmaciaId))
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(usuarioIdStr, out var usuarioId))
            return BadRequest(new { success = false, error = "Usuário não identificado" });

        try
        {
            var remetenteBloqueado = await _context.RemetentesBlockeados
                .FirstOrDefaultAsync(r => r.Id == id && r.Ativo);

            if (remetenteBloqueado == null)
                return NotFound(new { success = false, error = "Remetente bloqueado não encontrado" });

            // Verificar permissão: atendente só pode desbloquear seus bloqueios locais
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == UserRoles.Atendente && remetenteBloqueado.FarmaciaId != farmaciaId)
                return Forbid();

            var usuario = await _context.UsuariosFarmacia.FirstOrDefaultAsync(u => u.Id == usuarioId);
            var nomeUsuario = usuario?.Nome ?? "Desconhecido";

            await _remetenteBloqueadoService.DesbloqueRemetenteAsync(
                remetenteBloqueado.NumeroNormalizado,
                remetenteBloqueado.FarmaciaId ?? farmaciaId,
                remetenteBloqueado.Escopo,
                usuarioId,
                nomeUsuario);

            return Ok(new
            {
                success = true,
                message = "Remetente desbloqueado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desbloquear remetente {id}", id);
            return StatusCode(500, new { success = false, error = "Erro ao desbloquear remetente" });
        }
    }
}

public sealed record UpdateSalvarLogCompletoMidiaApiGoRequest(bool Enabled);
public sealed record UpdateHabilitarBloqueioRemetenteRequest(bool Enabled);
public sealed record UpdatePermitirGerenciarEtiquetasAtendenteRequest(bool Enabled);
