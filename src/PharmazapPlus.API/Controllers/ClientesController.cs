#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.Shared.Models.CRM;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly ICrmService _crmService;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(
        ICrmService crmService,
        ILogger<ClientesController> logger)
    {
        _crmService = crmService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetClientes(
        [FromQuery] string? query = null,
        [FromQuery] ClienteStatus? status = null,
        [FromQuery] ClienteTipo? tipo = null,
        [FromQuery] string? segmento = null,
        [FromQuery] bool? usoContinuo = null,
        [FromQuery] string? tag = null,
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? orderBy = "Nome",
        [FromQuery] bool orderDesc = false)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;
            _logger.LogInformation("🟡 GetClientes called - FarmaciaId: {FarmaciaId}, User: {User}, Page: {Page}, PageSize: {PageSize}", 
                farmaciaId, User?.Identity?.Name, page, pageSize);

            if (string.IsNullOrEmpty(farmaciaId))
            {
                _logger.LogWarning("🔴 GetClientes - FarmaciaId not found in claims for user: {User}", User?.Identity?.Name);
                return BadRequest(new { success = false, error = "Farmácia não identificada" });
            }

            var filter = new ClienteSearchFilter
            {
                Query = query ?? string.Empty,
                Status = status,
                Tipo = tipo,
                Segmento = segmento ?? string.Empty,
                UsoContinuo = usoContinuo,
                Tag = tag ?? string.Empty,
                DataInicio = dataInicio,
                DataFim = dataFim,
                Page = page,
                PageSize = pageSize,
                OrderBy = orderBy ?? "Nome",
                OrderDesc = orderDesc
            };

            _logger.LogInformation("🟡 Calling _crmService.SearchClientes with FarmaciaId: {FarmaciaId}", farmaciaId);
            var clientes = await _crmService.SearchClientes(farmaciaId, filter);
            _logger.LogInformation("🟢 GetClientes - Returned {Count} clientes for FarmaciaId: {FarmaciaId}", clientes.Count(), farmaciaId);

            return Ok(new
            {
                success = true,
                data = clientes,
                pagination = new
                {
                    page,
                    pageSize,
                    total = clientes.Count()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔴 Error getting clientes");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro interno do servidor"
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCliente(int id)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var cliente = await _crmService.GetCliente(id, farmaciaId);

            return Ok(new { success = true, data = cliente });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateCliente([FromBody] Cliente cliente)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            if (string.IsNullOrWhiteSpace(cliente.Nome))
                return BadRequest(new { success = false, error = "Nome é obrigatório" });

            if (string.IsNullOrWhiteSpace(cliente.WhatsAppId))
                return BadRequest(new { success = false, error = "WhatsApp é obrigatório" });

            // Verificar duplicata por WhatsAppId na mesma farmácia
            var filter = new ClienteSearchFilter { Query = cliente.WhatsAppId, PageSize = 1 };
            var existing = await _crmService.SearchClientes(farmaciaId, filter);
            if (existing.Any(c => c.WhatsAppId == cliente.WhatsAppId))
                return BadRequest(new { success = false, error = "Já existe um cliente com este WhatsApp nesta farmácia" });

            // Definir farmácia do cliente
            cliente.FarmaciaId = int.Parse(farmaciaId);
            cliente.CriadoPor = userId;

            var createdCliente = await _crmService.CreateCliente(cliente);

            return CreatedAtAction(nameof(GetCliente), new { id = createdCliente.Id },
                new { success = true, data = createdCliente });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cliente");
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCliente(int id, [FromBody] ClienteUpdateDto updateDto)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            // Verificar se o cliente pertence à farmácia
            var cliente = await _crmService.GetCliente(id, farmaciaId);
            if (cliente == null)
                return NotFound(new { success = false, error = "Cliente não encontrado" });

            updateDto.Nome ??= cliente.Nome;
            updateDto.Sobrenome ??= cliente.Sobrenome;

            if (string.IsNullOrWhiteSpace(updateDto.Nome))
                return BadRequest(new { success = false, error = "Nome não pode ser vazio" });

            var updatedCliente = await _crmService.UpdateCliente(id, updateDto);

            return Ok(new { success = true, data = updatedCliente });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Gestor")]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var (success, message) = await _crmService.DeleteCliente(id, farmaciaId);

            if (!success)
                return BadRequest(new { success = false, error = message });

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpPost("{id}/reativar")]
    [Authorize(Roles = "Administrador,Gestor,Atendente")]
    public async Task<IActionResult> ReativarCliente(int id)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var (success, message) = await _crmService.ReativarCliente(id, farmaciaId);

            if (!success)
                return BadRequest(new { success = false, error = message });

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpGet("{id}/estatisticas")]
    public async Task<IActionResult> GetEstatisticasCliente(int id)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var estatisticas = await _crmService.GetEstatisticasCliente(id, farmaciaId);

            return Ok(new { success = true, data = estatisticas });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting estatísticas for cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpGet("{id}/atendimentos")]
    public async Task<IActionResult> GetAtendimentosCliente(int id)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            var atendimentos = await _crmService.GetAtendimentosByCliente(id, farmaciaId);

            return Ok(new { success = true, data = atendimentos });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting atendimentos for cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> AddTagToCliente(int id, [FromBody] AddTagRequest request)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            // Verificar se o cliente pertence à farmácia
            var cliente = await _crmService.GetCliente(id, farmaciaId);
            if (cliente == null)
                return NotFound(new { success = false, error = "Cliente não encontrado" });

            var tag = await _crmService.AddTagToCliente(id, request.Tag, request.Cor, userId);

            return Ok(new { success = true, data = tag });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tag to cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }

    [HttpPost("{id}/observacoes")]
    public async Task<IActionResult> AddObservacaoToCliente(int id, [FromBody] ClienteObservacaoDto observacaoDto)
    {
        try
        {
            var farmaciaId = User.FindFirst("FarmaciaId")?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(farmaciaId))
                return BadRequest(new { success = false, error = "Farmácia não identificada" });

            // Verificar se o cliente pertence à farmácia
            var cliente = await _crmService.GetCliente(id, farmaciaId);
            if (cliente == null)
                return NotFound(new { success = false, error = "Cliente não encontrado" });

            if (string.IsNullOrWhiteSpace(observacaoDto.Responsavel))
                observacaoDto.Responsavel = !string.IsNullOrWhiteSpace(userId) ? userId : "Sistema";

            var observacao = await _crmService.AddObservacaoToCliente(id, observacaoDto);

            return Ok(new { success = true, data = observacao });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding observação to cliente {ClienteId}", id);
            return StatusCode(500, new { success = false, error = "Erro interno do servidor" });
        }
    }
}
