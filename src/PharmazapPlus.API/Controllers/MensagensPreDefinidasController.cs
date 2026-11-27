using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/mensagens-pre-definidas")]
[Authorize]
public class MensagensPreDefinidasController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<MensagensPreDefinidasController> _logger;

    public MensagensPreDefinidasController(PharmazapDbContext context, ILogger<MensagensPreDefinidasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lista as mensagens pré-definidas da farmácia. Acessível por todos os papéis autenticados.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMensagensPreDefinidas([FromQuery] bool? ativo = null)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            IQueryable<MensagemPreDefinida> query = _context.MensagensPreDefinidas
                .Where(m => m.FarmaciaId == farmaciaId.Value);

            if (ativo.HasValue)
                query = query.Where(m => m.Ativo == ativo.Value);

            var mensagens = await query
                .OrderBy(m => m.Ordem)
                .ThenBy(m => m.Titulo)
                .Select(m => new
                {
                    m.Id,
                    m.Titulo,
                    m.Texto,
                    m.Ativo,
                    m.Ordem,
                    m.FarmaciaId,
                    m.CreatedAt,
                    m.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { Success = true, Data = mensagens });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar mensagens pré-definidas");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar mensagens pré-definidas" });
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Gestor}")]
    public async Task<IActionResult> GetMensagemPreDefinida(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var mensagem = await _context.MensagensPreDefinidas
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (mensagem == null)
                return NotFound(new { Success = false, Message = "Mensagem pré-definida não encontrada" });

            return Ok(new { Success = true, Data = mensagem });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar mensagem pré-definida {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar mensagem pré-definida" });
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Gestor}")]
    public async Task<IActionResult> CreateMensagemPreDefinida([FromBody] CreateMensagemPreDefinidaRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Titulo))
                return BadRequest(new { Success = false, Message = "Título é obrigatório" });

            if (string.IsNullOrWhiteSpace(request.Texto))
                return BadRequest(new { Success = false, Message = "Texto é obrigatório" });

            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var tituloExistente = await _context.MensagensPreDefinidas
                .AnyAsync(m => m.FarmaciaId == farmaciaId.Value && m.Titulo == request.Titulo.Trim());
            if (tituloExistente)
                return BadRequest(new { Success = false, Message = "Já existe uma mensagem pré-definida com este título" });

            var mensagem = new MensagemPreDefinida
            {
                FarmaciaId = farmaciaId.Value,
                Titulo = request.Titulo.Trim(),
                Texto = request.Texto.Trim(),
                Ativo = request.Ativo ?? true,
                Ordem = request.Ordem ?? 0
            };

            _context.MensagensPreDefinidas.Add(mensagem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Mensagem pré-definida criada: {Titulo} (ID: {Id}) na farmácia {FarmaciaId}",
                mensagem.Titulo, mensagem.Id, mensagem.FarmaciaId);

            return CreatedAtAction(nameof(GetMensagemPreDefinida), new { id = mensagem.Id }, new
            {
                Success = true,
                Data = new
                {
                    mensagem.Id,
                    mensagem.Titulo,
                    mensagem.Texto,
                    mensagem.Ativo,
                    mensagem.Ordem,
                    mensagem.FarmaciaId
                },
                Message = "Mensagem pré-definida criada com sucesso"
            });
        }
        catch (Exception ex)
        {
            var msg = $"Erro ao criar mensagem pré-definida. {ex.Message}";
            _logger.LogError(ex, msg);
            return StatusCode(500, new { Success = false, Message = msg });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Gestor}")]
    public async Task<IActionResult> UpdateMensagemPreDefinida(int id, [FromBody] UpdateMensagemPreDefinidaRequest request)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var mensagem = await _context.MensagensPreDefinidas
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (mensagem == null)
                return NotFound(new { Success = false, Message = "Mensagem pré-definida não encontrada" });

            if (!string.IsNullOrWhiteSpace(request.Titulo) && request.Titulo.Trim() != mensagem.Titulo)
            {
                var tituloExistente = await _context.MensagensPreDefinidas
                    .AnyAsync(m => m.FarmaciaId == farmaciaId.Value && m.Titulo == request.Titulo.Trim() && m.Id != id);
                if (tituloExistente)
                    return BadRequest(new { Success = false, Message = "Já existe uma mensagem pré-definida com este título" });

                mensagem.Titulo = request.Titulo.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Texto))
                mensagem.Texto = request.Texto.Trim();

            if (request.Ativo.HasValue)
                mensagem.Ativo = request.Ativo.Value;

            if (request.Ordem.HasValue)
                mensagem.Ordem = request.Ordem.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Mensagem pré-definida atualizada: {Titulo} (ID: {Id})", mensagem.Titulo, mensagem.Id);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    mensagem.Id,
                    mensagem.Titulo,
                    mensagem.Texto,
                    mensagem.Ativo,
                    mensagem.Ordem,
                    mensagem.FarmaciaId
                },
                Message = "Mensagem pré-definida atualizada com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar mensagem pré-definida {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar mensagem pré-definida" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Gestor}")]
    public async Task<IActionResult> InativarMensagemPreDefinida(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var mensagem = await _context.MensagensPreDefinidas
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (mensagem == null)
                return NotFound(new { Success = false, Message = "Mensagem pré-definida não encontrada" });

            mensagem.Ativo = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Mensagem pré-definida inativada: {Titulo} (ID: {Id})", mensagem.Titulo, mensagem.Id);

            return Ok(new { Success = true, Message = "Mensagem pré-definida inativada com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inativar mensagem pré-definida {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao inativar mensagem pré-definida" });
        }
    }

    [HttpPut("{id}/ativar")]
    [Authorize(Roles = $"{UserRoles.Administrador},{UserRoles.Gestor}")]
    public async Task<IActionResult> AtivarMensagemPreDefinida(int id)
    {
        try
        {
            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var mensagem = await _context.MensagensPreDefinidas
                .FirstOrDefaultAsync(m => m.Id == id && m.FarmaciaId == farmaciaId.Value);

            if (mensagem == null)
                return NotFound(new { Success = false, Message = "Mensagem pré-definida não encontrada" });

            mensagem.Ativo = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Mensagem pré-definida ativada: {Titulo} (ID: {Id})", mensagem.Titulo, mensagem.Id);

            return Ok(new { Success = true, Message = "Mensagem pré-definida ativada com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar mensagem pré-definida {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao ativar mensagem pré-definida" });
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

public record CreateMensagemPreDefinidaRequest(string Titulo, string Texto, bool? Ativo, int? Ordem);
public record UpdateMensagemPreDefinidaRequest(string? Titulo, string? Texto, bool? Ativo, int? Ordem);
