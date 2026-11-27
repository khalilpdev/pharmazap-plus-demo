using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/motivos-encerramento")]
[Authorize(Roles = UserRoles.Administrador)]
public class MotivosEncerramentoController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<MotivosEncerramentoController> _logger;

    public MotivosEncerramentoController(PharmazapDbContext context, ILogger<MotivosEncerramentoController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lista os motivos de encerramento da farmácia. Acessível por Administrador e Atendente.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> GetMotivosEncerramento([FromQuery] bool? ativo = null)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            IQueryable<MotivoEncerramento> query = _context.MotivosEncerramento
                .Where(m => m.FarmaciaId == farmaciaId.Value);

            if (ativo.HasValue)
                query = query.Where(m => m.Ativo == ativo.Value);

            var motivos = await query
                .OrderBy(m => m.Ordem)
                .ThenBy(m => m.Nome)
                .Select(m => new
                {
                    m.Id,
                    m.Nome,
                    m.ExigeValorVenda,
                    m.ExigeComprovante,
                    m.Ativo,
                    m.Ordem,
                    m.FarmaciaId,
                    m.CreatedAt,
                    m.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { Success = true, Data = motivos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar motivos de encerramento");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar motivos de encerramento" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMotivoEncerramento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var motivo = await _context.MotivosEncerramento
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (motivo == null)
                return NotFound(new { Success = false, Message = "Motivo de encerramento não encontrado" });

            return Ok(new { Success = true, Data = motivo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar motivo de encerramento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar motivo de encerramento" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateMotivoEncerramento([FromBody] CreateMotivoEncerramentoRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
                return BadRequest(new { Success = false, Message = "Nome é obrigatório" });

            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var nomeExistente = await _context.MotivosEncerramento
                .AnyAsync(m => m.FarmaciaId == farmaciaId.Value && m.Nome == request.Nome.Trim());
            if (nomeExistente)
                return BadRequest(new { Success = false, Message = "Já existe um motivo de encerramento com este nome" });

            var motivo = new MotivoEncerramento
            {
                FarmaciaId = farmaciaId.Value,
                Farmacia = null!,
                Nome = request.Nome.Trim(),
                ExigeValorVenda = request.ExigeValorVenda ?? false,
                ExigeComprovante = request.ExigeComprovante ?? false,
                Ativo = request.Ativo ?? true,
                Ordem = request.Ordem ?? 0
            };

            _context.MotivosEncerramento.Add(motivo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Motivo de encerramento criado: {Nome} (ID: {Id}) na farmácia {FarmaciaId}",
                motivo.Nome, motivo.Id, motivo.FarmaciaId);

            return CreatedAtAction(nameof(GetMotivoEncerramento), new { id = motivo.Id }, new
            {
                Success = true,
                Data = new
                {
                    motivo.Id,
                    motivo.Nome,
                    motivo.ExigeValorVenda,
                    motivo.ExigeComprovante,
                    motivo.Ativo,
                    motivo.Ordem,
                    motivo.FarmaciaId
                },
                Message = "Motivo de encerramento criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar motivo de encerramento");
            return StatusCode(500, new { Success = false, Message = "Erro ao criar motivo de encerramento" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMotivoEncerramento(int id, [FromBody] UpdateMotivoEncerramentoRequest request)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var motivo = await _context.MotivosEncerramento
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (motivo == null)
                return NotFound(new { Success = false, Message = "Motivo de encerramento não encontrado" });

            if (!string.IsNullOrWhiteSpace(request.Nome) && request.Nome.Trim() != motivo.Nome)
            {
                var nomeExistente = await _context.MotivosEncerramento
                    .AnyAsync(m => m.FarmaciaId == farmaciaId.Value && m.Nome == request.Nome.Trim() && m.Id != id);
                if (nomeExistente)
                    return BadRequest(new { Success = false, Message = "Já existe um motivo de encerramento com este nome" });

                motivo.Nome = request.Nome.Trim();
            }

            if (request.ExigeValorVenda.HasValue)
                motivo.ExigeValorVenda = request.ExigeValorVenda.Value;

            if (request.ExigeComprovante.HasValue)
                motivo.ExigeComprovante = request.ExigeComprovante.Value;

            if (request.Ativo.HasValue)
                motivo.Ativo = request.Ativo.Value;

            if (request.Ordem.HasValue)
                motivo.Ordem = request.Ordem.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Motivo de encerramento atualizado: {Nome} (ID: {Id})", motivo.Nome, motivo.Id);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    motivo.Id,
                    motivo.Nome,
                    motivo.ExigeValorVenda,
                    motivo.ExigeComprovante,
                    motivo.Ativo,
                    motivo.Ordem,
                    motivo.FarmaciaId
                },
                Message = "Motivo de encerramento atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar motivo de encerramento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar motivo de encerramento" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> InativarMotivoEncerramento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var motivo = await _context.MotivosEncerramento
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (motivo == null)
                return NotFound(new { Success = false, Message = "Motivo de encerramento não encontrado" });

            motivo.Ativo = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Motivo de encerramento inativado: {Nome} (ID: {Id})", motivo.Nome, motivo.Id);

            return Ok(new { Success = true, Message = "Motivo de encerramento inativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inativar motivo de encerramento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao inativar motivo de encerramento" });
        }
    }

    [HttpPut("{id}/ativar")]
    public async Task<IActionResult> AtivarMotivoEncerramento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var motivo = await _context.MotivosEncerramento
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (motivo == null)
                return NotFound(new { Success = false, Message = "Motivo de encerramento não encontrado" });

            motivo.Ativo = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Motivo de encerramento ativado: {Nome} (ID: {Id})", motivo.Nome, motivo.Id);

            return Ok(new { Success = true, Message = "Motivo de encerramento ativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar motivo de encerramento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao ativar motivo de encerramento" });
        }
    }

    private int? GetFarmaciaId()
    {
        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out int farmaciaId) && farmaciaId > 0)
            return farmaciaId;
        return null;
    }
}

public record CreateMotivoEncerramentoRequest(string Nome, bool? ExigeValorVenda, bool? ExigeComprovante, bool? Ativo, int? Ordem);
public record UpdateMotivoEncerramentoRequest(string? Nome, bool? ExigeValorVenda, bool? ExigeComprovante, bool? Ativo, int? Ordem);
