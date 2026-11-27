using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.Campanhas;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Campanhas;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CampanhasController : ControllerBase
{
    private readonly ICampanhaService _campanhaService;
    private readonly PharmazapDbContext _context;
    private readonly ILogger<CampanhasController> _logger;

    public CampanhasController(ICampanhaService campanhaService, PharmazapDbContext context, ILogger<CampanhasController> logger)
    {
        _campanhaService = campanhaService;
        _context = context;
        _logger = logger;
    }

    private async Task<int> GetFarmaciaIdAsync()
    {
        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out var id) && id > 0)
            return id;

        // Fallback: busca a farmácia vinculada ao usuário autenticado
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdStr, out var userId))
        {
            var usuario = await _context.UsuariosFarmacia
                .Where(u => u.Id == userId && u.Ativo)
                .Select(u => new { u.FarmaciaId })
                .FirstOrDefaultAsync();
            if (usuario != null)
                return usuario.FarmaciaId;
        }

        return 0;
    }

    private string GetUserName() =>
        User.FindFirst(ClaimTypes.Name)?.Value ?? "Sistema";

    [HttpGet]
    public async Task<IActionResult> GetCampanhas([FromQuery] CampanhaStatusEnum? status = null)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var campanhas = await _campanhaService.GetCampanhasAsync(farmaciaId, status);
        return Ok(new { success = true, data = campanhas, total = campanhas.Count });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCampanha(int id)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        var campanha = await _campanhaService.GetCampanhaByIdAsync(id, farmaciaId);
        if (campanha == null)
            return NotFound(new { success = false, error = "Campanha não encontrada" });

        return Ok(new { success = true, data = campanha });
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> CreateCampanha([FromBody] CreateCampanhaRequest request)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(new { success = false, error = "Nome é obrigatório" });

        if (string.IsNullOrWhiteSpace(request.Mensagem))
            return BadRequest(new { success = false, error = "Mensagem é obrigatória" });

        try
        {
            var farmacia = await _context.Farmacias.FindAsync(farmaciaId);
            if (farmacia == null)
                return BadRequest(new { success = false, error = "Farmácia não encontrada" });

            var campanha = new Campanha
            {
                FarmaciaId = farmaciaId,
                Farmacia = farmacia,
                Nome = request.Nome.Trim(),
                Mensagem = request.Mensagem.Trim(),
                Tipo = request.Tipo,
                EnviarParaTodosAtivos = request.EnviarParaTodosAtivos,
                EnviarParaUsoContinuo = request.EnviarParaUsoContinuo,
                EnviarParaAceitaMarketing = request.EnviarParaAceitaMarketing,
                Observacoes = request.Observacoes?.Trim(),
                EtiquetaIds = request.EtiquetaIds ?? new List<int>(),
                CriadoPor = GetUserName()
            };

            var created = await _campanhaService.CreateCampanhaAsync(campanha);
            return CreatedAtAction(nameof(GetCampanha), new { id = created.Id },
                new { success = true, data = created });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar campanha para farmácia {FarmaciaId}: {Message}", farmaciaId, ex.Message);
            return StatusCode(500, new { success = false, error = "Erro interno ao salvar a campanha. Verifique os dados e tente novamente." });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> UpdateCampanha(int id, [FromBody] UpdateCampanhaRequest request)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var campanha = await _campanhaService.GetCampanhaByIdAsync(id, farmaciaId);
            if (campanha == null)
                return NotFound(new { success = false, error = "Campanha não encontrada" });

            if (campanha.Status != CampanhaStatusEnum.Rascunho)
                return BadRequest(new { success = false, error = "Somente campanhas em rascunho podem ser editadas" });

            campanha.Nome = request.Nome?.Trim() ?? campanha.Nome;
            campanha.Mensagem = request.Mensagem?.Trim() ?? campanha.Mensagem;
            campanha.Tipo = request.Tipo;
            campanha.EnviarParaTodosAtivos = request.EnviarParaTodosAtivos;
            campanha.EnviarParaUsoContinuo = request.EnviarParaUsoContinuo;
            campanha.EnviarParaAceitaMarketing = request.EnviarParaAceitaMarketing;
            campanha.Observacoes = request.Observacoes?.Trim();
            campanha.EtiquetaIds = request.EtiquetaIds ?? new List<int>();
            campanha.AtualizadoPor = GetUserName();

            var updated = await _campanhaService.UpdateCampanhaAsync(campanha);
            return Ok(new { success = true, data = updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar campanha {Id} para farmácia {FarmaciaId}: {Message}", id, farmaciaId, ex.Message);
            return StatusCode(500, new { success = false, error = "Erro interno ao salvar a campanha. Verifique os dados e tente novamente." });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> DeleteCampanha(int id)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var deleted = await _campanhaService.DeleteCampanhaAsync(id, farmaciaId);
            if (!deleted)
                return NotFound(new { success = false, error = "Campanha não encontrada" });

            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{id:int}/agendar")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> AgendarCampanha(int id, [FromBody] AgendarCampanhaRequest request)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var campanha = await _campanhaService.AgendarCampanhaAsync(id, farmaciaId, request.DataAgendamento);
            return Ok(new { success = true, data = campanha });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancelar")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> CancelarCampanha(int id)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var campanha = await _campanhaService.CancelarCampanhaAsync(id, farmaciaId);
            return Ok(new { success = true, data = campanha });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{id:int}/executar")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> ExecutarCampanha(int id)
    {
        var farmaciaId = await GetFarmaciaIdAsync();
        if (farmaciaId == 0)
            return BadRequest(new { success = false, error = "Farmácia não identificada" });

        try
        {
            var campanha = await _campanhaService.ExecutarCampanhaAsync(id, farmaciaId);
            return Ok(new { success = true, data = campanha });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

public record AgendarCampanhaRequest(DateTime? DataAgendamento);
