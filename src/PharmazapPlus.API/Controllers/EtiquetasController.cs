using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
public class EtiquetasController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<EtiquetasController> _logger;

    public EtiquetasController(PharmazapDbContext context, ILogger<EtiquetasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Acessível por Administrador e Atendente (para preencher combos)
    [HttpGet]
    public async Task<IActionResult> GetEtiquetas([FromQuery] bool? ativo = null)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            IQueryable<Etiqueta> query = _context.Etiquetas
                .Where(e => e.FarmaciaId == farmaciaId.Value);

            if (ativo.HasValue)
                query = query.Where(e => e.Ativo == ativo.Value);

            var etiquetas = await query
                .OrderBy(e => e.Nome)
                .Select(e => new
                {
                    e.Id,
                    e.Nome,
                    e.Cor,
                    e.Ativo,
                    e.FarmaciaId,
                    e.CreatedAt,
                    e.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { Success = true, Data = etiquetas });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar etiquetas");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar etiquetas" });
        }
    }

    [HttpGet("permissoes")]
    public async Task<IActionResult> GetPermissoes()
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var podeGerenciar = await PodeGerenciarEtiquetasAsync(farmaciaId.Value);
            return Ok(new
            {
                Success = true,
                Data = new
                {
                    PodeGerenciarEtiquetas = podeGerenciar,
                    SomenteLeitura = !podeGerenciar
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar permissões de etiquetas");
            return StatusCode(500, new { Success = false, Message = "Erro ao carregar permissões de etiquetas" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEtiqueta(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var etiqueta = await _context.Etiquetas
                .FirstOrDefaultAsync(e => e.Id == id && e.FarmaciaId == farmaciaId.Value);

            if (etiqueta == null)
                return NotFound(new { Success = false, Message = "Etiqueta não encontrada" });

            return Ok(new { Success = true, Data = etiqueta });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar etiqueta {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar etiqueta" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateEtiqueta([FromBody] CreateEtiquetaRequest request)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            if (!await PodeGerenciarEtiquetasAsync(farmaciaId.Value))
                return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "Você não tem permissão para criar etiquetas" });

            if (string.IsNullOrWhiteSpace(request.Nome))
                return BadRequest(new { Success = false, Message = "Nome é obrigatório" });

            var nomeExistente = await _context.Etiquetas
                .AnyAsync(e => e.FarmaciaId == farmaciaId.Value && e.Nome == request.Nome.Trim());
            if (nomeExistente)
                return BadRequest(new { Success = false, Message = "Já existe uma etiqueta com este nome" });

            var etiqueta = new Etiqueta
            {
                FarmaciaId = farmaciaId.Value,
                Nome = request.Nome.Trim(),
                Cor = string.IsNullOrWhiteSpace(request.Cor) ? Consts.EtiquetaCores.Padrao : request.Cor.Trim(),
                Ativo = request.Ativo ?? true,
                Farmacia = null!
            };

            _context.Etiquetas.Add(etiqueta);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Etiqueta criada: {Nome} (ID: {Id}) na farmácia {FarmaciaId}",
                etiqueta.Nome, etiqueta.Id, etiqueta.FarmaciaId);

            return CreatedAtAction(nameof(GetEtiqueta), new { id = etiqueta.Id }, new
            {
                Success = true,
                Data = new { etiqueta.Id, etiqueta.Nome, etiqueta.Cor, etiqueta.Ativo, etiqueta.FarmaciaId },
                Message = "Etiqueta criada com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar etiqueta");
            return StatusCode(500, new { Success = false, Message = "Erro ao criar etiqueta" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEtiqueta(int id, [FromBody] UpdateEtiquetaRequest request)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            if (!await PodeGerenciarEtiquetasAsync(farmaciaId.Value))
                return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "Você não tem permissão para atualizar etiquetas" });

            var etiqueta = await _context.Etiquetas
                .FirstOrDefaultAsync(e => e.Id == id && e.FarmaciaId == farmaciaId.Value);

            if (etiqueta == null)
                return NotFound(new { Success = false, Message = "Etiqueta não encontrada" });

            if (!string.IsNullOrWhiteSpace(request.Nome) && request.Nome.Trim() != etiqueta.Nome)
            {
                var nomeExistente = await _context.Etiquetas
                    .AnyAsync(e => e.FarmaciaId == farmaciaId.Value && e.Nome == request.Nome.Trim() && e.Id != id);
                if (nomeExistente)
                    return BadRequest(new { Success = false, Message = "Já existe uma etiqueta com este nome" });

                etiqueta.Nome = request.Nome.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Cor))
                etiqueta.Cor = request.Cor.Trim();

            if (request.Ativo.HasValue)
                etiqueta.Ativo = request.Ativo.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Etiqueta atualizada: {Nome} (ID: {Id})", etiqueta.Nome, etiqueta.Id);

            return Ok(new
            {
                Success = true,
                Data = new { etiqueta.Id, etiqueta.Nome, etiqueta.Cor, etiqueta.Ativo, etiqueta.FarmaciaId },
                Message = "Etiqueta atualizada com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar etiqueta {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar etiqueta" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> InativarEtiqueta(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            if (!await PodeGerenciarEtiquetasAsync(farmaciaId.Value))
                return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "Você não tem permissão para inativar etiquetas" });

            var etiqueta = await _context.Etiquetas
                .FirstOrDefaultAsync(e => e.Id == id && e.FarmaciaId == farmaciaId.Value);

            if (etiqueta == null)
                return NotFound(new { Success = false, Message = "Etiqueta não encontrada" });

            etiqueta.Ativo = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Etiqueta inativada: {Nome} (ID: {Id})", etiqueta.Nome, etiqueta.Id);

            return Ok(new { Success = true, Message = "Etiqueta inativada com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inativar etiqueta {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao inativar etiqueta" });
        }
    }

    [HttpPut("{id}/ativar")]
    public async Task<IActionResult> AtivarEtiqueta(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            if (!await PodeGerenciarEtiquetasAsync(farmaciaId.Value))
                return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "Você não tem permissão para ativar etiquetas" });

            var etiqueta = await _context.Etiquetas
                .FirstOrDefaultAsync(e => e.Id == id && e.FarmaciaId == farmaciaId.Value);

            if (etiqueta == null)
                return NotFound(new { Success = false, Message = "Etiqueta não encontrada" });

            etiqueta.Ativo = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Etiqueta ativada: {Nome} (ID: {Id})", etiqueta.Nome, etiqueta.Id);

            return Ok(new { Success = true, Message = "Etiqueta ativada com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar etiqueta {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao ativar etiqueta" });
        }
    }

    private int? GetFarmaciaId()
    {
        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out int farmaciaId) && farmaciaId > 0)
            return farmaciaId;
        return null;
    }

    private async Task<bool> PodeGerenciarEtiquetasAsync(int farmaciaId)
    {
        if (User.IsInRole(UserRoles.Administrador))
            return true;

        if (!User.IsInRole(UserRoles.Atendente))
            return false;

        return await _context.FarmaciaIntegracoes
            .AsNoTracking()
            .Where(i => i.FarmaciaId == farmaciaId)
            .Select(i => i.PermitirGerenciarEtiquetasAtendente)
            .FirstOrDefaultAsync();
    }
}

public record CreateEtiquetaRequest(string Nome, string? Cor, bool? Ativo);
public record UpdateEtiquetaRequest(string? Nome, string? Cor, bool? Ativo);
