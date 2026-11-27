using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Administrador)]
public class DepartamentosController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<DepartamentosController> _logger;

    public DepartamentosController(PharmazapDbContext context, ILogger<DepartamentosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Atendente}")]
    public async Task<IActionResult> GetDepartamentos([FromQuery] bool? ativo = null)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            IQueryable<Departamento> query = _context.Departamentos
                .Where(d => d.FarmaciaId == farmaciaId.Value);

            if (ativo.HasValue)
                query = query.Where(d => d.Ativo == ativo.Value);

            var departamentos = await query
                .OrderBy(d => d.Nome)
                .Select(d => new
                {
                    d.Id,
                    d.Nome,
                    d.Ativo,
                    d.FarmaciaId,
                    d.CreatedAt,
                    d.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { Success = true, Data = departamentos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar departamentos");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar departamentos" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDepartamento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var departamento = await _context.Departamentos
                .FirstOrDefaultAsync(d => d.Id == id && d.FarmaciaId == farmaciaId.Value);

            if (departamento == null)
                return NotFound(new { Success = false, Message = "Departamento não encontrado" });

            return Ok(new { Success = true, Data = departamento });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar departamento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar departamento" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateDepartamento([FromBody] CreateDepartamentoRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
                return BadRequest(new { Success = false, Message = "Nome é obrigatório" });

            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var nomeExistente = await _context.Departamentos
                .AnyAsync(d => d.FarmaciaId == farmaciaId.Value && d.Nome == request.Nome.Trim());
            if (nomeExistente)
                return BadRequest(new { Success = false, Message = "Já existe um departamento com este nome" });

            var departamento = new Departamento
            {
                FarmaciaId = farmaciaId.Value,
                Nome = request.Nome.Trim(),
                Ativo = request.Ativo ?? true,
                Farmacia = null
            };

            _context.Departamentos.Add(departamento);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Departamento criado: {Nome} (ID: {Id}) na farmácia {FarmaciaId}",
                departamento.Nome, departamento.Id, departamento.FarmaciaId);

            return CreatedAtAction(nameof(GetDepartamento), new { id = departamento.Id }, new
            {
                Success = true,
                Data = new { departamento.Id, departamento.Nome, departamento.Ativo, departamento.FarmaciaId },
                Message = "Departamento criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar departamento");
            return StatusCode(500, new { Success = false, Message = "Erro ao criar departamento" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDepartamento(int id, [FromBody] UpdateDepartamentoRequest request)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var departamento = await _context.Departamentos
                .FirstOrDefaultAsync(d => d.Id == id && d.FarmaciaId == farmaciaId.Value);

            if (departamento == null)
                return NotFound(new { Success = false, Message = "Departamento não encontrado" });

            if (!string.IsNullOrWhiteSpace(request.Nome) && request.Nome.Trim() != departamento.Nome)
            {
                var nomeExistente = await _context.Departamentos
                    .AnyAsync(d => d.FarmaciaId == farmaciaId.Value && d.Nome == request.Nome.Trim() && d.Id != id);
                if (nomeExistente)
                    return BadRequest(new { Success = false, Message = "Já existe um departamento com este nome" });

                departamento.Nome = request.Nome.Trim();
            }

            if (request.Ativo.HasValue)
                departamento.Ativo = request.Ativo.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Departamento atualizado: {Nome} (ID: {Id})", departamento.Nome, departamento.Id);

            return Ok(new
            {
                Success = true,
                Data = new { departamento.Id, departamento.Nome, departamento.Ativo, departamento.FarmaciaId },
                Message = "Departamento atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar departamento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar departamento" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> InativarDepartamento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var departamento = await _context.Departamentos
                .FirstOrDefaultAsync(d => d.Id == id && d.FarmaciaId == farmaciaId.Value);

            if (departamento == null)
                return NotFound(new { Success = false, Message = "Departamento não encontrado" });

            departamento.Ativo = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Departamento inativado: {Nome} (ID: {Id})", departamento.Nome, departamento.Id);

            return Ok(new { Success = true, Message = "Departamento inativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inativar departamento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao inativar departamento" });
        }
    }

    [HttpPut("{id}/ativar")]
    public async Task<IActionResult> AtivarDepartamento(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var departamento = await _context.Departamentos
                .FirstOrDefaultAsync(d => d.Id == id && d.FarmaciaId == farmaciaId.Value);

            if (departamento == null)
                return NotFound(new { Success = false, Message = "Departamento não encontrado" });

            departamento.Ativo = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Departamento ativado: {Nome} (ID: {Id})", departamento.Nome, departamento.Id);

            return Ok(new { Success = true, Message = "Departamento ativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar departamento {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao ativar departamento" });
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

public record CreateDepartamentoRequest(string Nome, bool? Ativo);
public record UpdateDepartamentoRequest(string? Nome, bool? Ativo);
