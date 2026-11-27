using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.Core;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Gestor},{UserRoles.Administrador}")]
public class AtendentesController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<AtendentesController> _logger;

    public AtendentesController(PharmazapDbContext context, ILogger<AtendentesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAtendentes([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var farmaciaId = GetFarmaciaId();
            if (!UserRoles.IsGestorLike(currentRole) && farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            IQueryable<UsuarioFarmacia> query = _context.UsuariosFarmacia
                .Where(u => u.Role == UserRoles.Atendente);

            if (!UserRoles.IsGestorLike(currentRole) && farmaciaId.HasValue)
                query = query.Where(u => u.FarmaciaId == farmaciaId.Value);

            var total = await query.CountAsync();
            var atendentes = await query
                .OrderByDescending(u => u.DataCadastro)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Ativo,
                    u.DataCadastro,
                    u.UltimoLogin,
                    u.Turno,
                    u.RecebeNotificacoes,
                    u.TotalAtendimentos,
                    u.AtendimentosHoje,
                    u.FarmaciaId,
                    u.DepartamentoId,
                    DepartamentoNome = u.Departamento != null ? u.Departamento.Nome : null
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = atendentes,
                Pagination = new
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar atendentes");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar atendentes" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAtendente(int id)
    {
        try
        {
            var atendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRoles.Atendente);

            if (atendente == null)
                return NotFound(new { Success = false, Message = "Atendente não encontrado" });

            if (!CanAccess(atendente))
                return Forbid();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    atendente.Id,
                    atendente.Nome,
                    atendente.Email,
                    atendente.Telefone,
                    atendente.Ativo,
                    atendente.DataCadastro,
                    atendente.UltimoLogin,
                    atendente.Turno,
                    atendente.RecebeNotificacoes,
                    atendente.TotalAtendimentos,
                    atendente.AtendimentosHoje,
                    atendente.FarmaciaId,
                    atendente.DepartamentoId,
                    DepartamentoNome = atendente.Departamento != null ? atendente.Departamento.Nome : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar atendente {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar atendente" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAtendente([FromBody] CreateAtendenteRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nome) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { Success = false, Message = "Nome, Email e Senha são obrigatórios" });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { Success = false, Message = "A senha deve ter no mínimo 6 caracteres" });
            }

            var farmaciaId = GetFarmaciaId();
            if (farmaciaId == null)
                return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });

            var farmaciaExists = await _context.Farmacias.AnyAsync(f => f.Id == farmaciaId.Value);
            if (!farmaciaExists)
                return BadRequest(new { Success = false, Message = "Farmácia não encontrada" });

            var emailExists = await _context.UsuariosFarmacia
                .AnyAsync(u => u.FarmaciaId == farmaciaId.Value && u.Email == request.Email);
            if (emailExists)
                return BadRequest(new { Success = false, Message = "Email já cadastrado nesta farmácia" });

            if (request.DepartamentoId.HasValue)
            {
                var departamentoValido = await _context.Departamentos
                    .AnyAsync(d => d.Id == request.DepartamentoId.Value && d.FarmaciaId == farmaciaId.Value && d.Ativo);
                if (!departamentoValido)
                    return BadRequest(new { Success = false, Message = "Departamento inválido para esta farmácia" });
            }

            var (hash, salt) = GeneratePasswordHash(request.Password);

            var atendente = new UsuarioFarmacia
            {
                Nome = request.Nome,
                Email = request.Email,
                Telefone = request.Telefone ?? "",
                Role = UserRoles.Atendente,
                PasswordHash = hash,
                PasswordSalt = salt,
                Ativo = true,
                EmailConfirmado = true,
                FarmaciaId = farmaciaId.Value,
                Farmacia = null!,
                DataCadastro = DateTime.UtcNow,
                Turno = request.Turno ?? "Integral",
                RecebeNotificacoes = request.RecebeNotificacoes ?? true,
                DepartamentoId = request.DepartamentoId,
                TotalAtendimentos = 0,
                AtendimentosHoje = 0
            };

            _context.UsuariosFarmacia.Add(atendente);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Atendente criado: {Email} (ID: {Id}) na farmácia {FarmaciaId}",
                atendente.Email, atendente.Id, atendente.FarmaciaId);

            return CreatedAtAction(nameof(GetAtendente), new { id = atendente.Id }, new
            {
                Success = true,
                Data = new
                {
                    atendente.Id,
                    atendente.Nome,
                    atendente.Email,
                    atendente.Telefone,
                    atendente.Ativo,
                    atendente.FarmaciaId,
                    atendente.DataCadastro,
                    atendente.Turno,
                    atendente.RecebeNotificacoes,
                    atendente.DepartamentoId
                },
                Message = "Atendente criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar atendente");
            return StatusCode(500, new { Success = false, Message = "Erro ao criar atendente" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAtendente(int id, [FromBody] UpdateAtendenteRequest request)
    {
        try
        {
            var atendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRoles.Atendente);

            if (atendente == null)
                return NotFound(new { Success = false, Message = "Atendente não encontrado" });

            if (!CanAccess(atendente))
                return Forbid();

            if (!string.IsNullOrWhiteSpace(request.Nome))
                atendente.Nome = request.Nome;

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != atendente.Email)
            {
                var emailExists = await _context.UsuariosFarmacia
                    .AnyAsync(u => u.FarmaciaId == atendente.FarmaciaId && u.Email == request.Email && u.Id != id);
                if (emailExists)
                    return BadRequest(new { Success = false, Message = "Email já cadastrado nesta farmácia" });

                atendente.Email = request.Email;
            }

            if (request.Telefone != null)
                atendente.Telefone = request.Telefone;

            if (request.Turno != null)
                atendente.Turno = request.Turno;

            if (request.RecebeNotificacoes.HasValue)
                atendente.RecebeNotificacoes = request.RecebeNotificacoes.Value;

            if (request.DepartamentoIdInformado)
            {
                if (request.DepartamentoId.HasValue)
                {
                    var departamentoValido = await _context.Departamentos
                        .AnyAsync(d => d.Id == request.DepartamentoId.Value && d.FarmaciaId == atendente.FarmaciaId && d.Ativo);
                    if (!departamentoValido)
                        return BadRequest(new { Success = false, Message = "Departamento inválido para esta farmácia" });
                }

                atendente.DepartamentoId = request.DepartamentoId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Atendente atualizado: {Email} (ID: {Id})", atendente.Email, atendente.Id);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    atendente.Id,
                    atendente.Nome,
                    atendente.Email,
                    atendente.Telefone,
                    atendente.Ativo,
                    atendente.Turno,
                    atendente.RecebeNotificacoes,
                    atendente.FarmaciaId,
                    atendente.DepartamentoId
                },
                Message = "Atendente atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar atendente {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar atendente" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> InativarAtendente(int id)
    {
        try
        {
            var atendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRoles.Atendente);

            if (atendente == null)
                return NotFound(new { Success = false, Message = "Atendente não encontrado" });

            if (!CanAccess(atendente))
                return Forbid();

            atendente.Ativo = false;
            atendente.DataInativacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Atendente inativado: {Email} (ID: {Id})", atendente.Email, atendente.Id);

            return Ok(new { Success = true, Message = "Atendente inativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inativar atendente {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao inativar atendente" });
        }
    }

    [HttpPut("{id}/ativar")]
    public async Task<IActionResult> AtivarAtendente(int id)
    {
        try
        {
            var atendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRoles.Atendente);

            if (atendente == null)
                return NotFound(new { Success = false, Message = "Atendente não encontrado" });

            if (!CanAccess(atendente))
                return Forbid();

            atendente.Ativo = true;
            atendente.DataInativacao = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Atendente ativado: {Email} (ID: {Id})", atendente.Email, atendente.Id);

            return Ok(new { Success = true, Message = "Atendente ativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar atendente {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao ativar atendente" });
        }
    }

    [HttpPut("{id}/reset-senha")]
    public async Task<IActionResult> ResetSenhaAtendente(int id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { Success = false, Message = "A nova senha é obrigatória" });

            if (request.NewPassword.Length < 6)
                return BadRequest(new { Success = false, Message = "A senha deve ter no mínimo 6 caracteres" });

            var atendente = await _context.UsuariosFarmacia
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRoles.Atendente);

            if (atendente == null)
                return NotFound(new { Success = false, Message = "Atendente não encontrado" });

            if (!CanAccess(atendente))
                return Forbid();

            var (hash, salt) = GeneratePasswordHash(request.NewPassword);
            atendente.PasswordHash = hash;
            atendente.PasswordSalt = salt;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Senha redefinida para atendente {Email} (ID: {Id}) por {AdminUser}",
                atendente.Email, atendente.Id, User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown");

            return Ok(new { Success = true, Message = "Senha redefinida com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao redefinir senha do atendente {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao redefinir senha" });
        }
    }

    private int? GetFarmaciaId()
    {
        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out int farmaciaId) && farmaciaId > 0)
            return farmaciaId;
        return null;
    }

    private bool CanAccess(UsuarioFarmacia atendente)
    {
        var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (UserRoles.IsGestorLike(currentRole))
            return true;

        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out int farmaciaId))
            return atendente.FarmaciaId == farmaciaId;

        return false;
    }

    private static (string hash, string salt) GeneratePasswordHash(string password)
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        string salt = Convert.ToBase64String(saltBytes);

        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }
}
