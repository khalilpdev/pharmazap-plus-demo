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
public class UsuariosController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(PharmazapDbContext context, ILogger<UsuariosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsuarios([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? role = null)
    {
        try
        {
            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;

            IQueryable<UsuarioFarmacia> query = _context.UsuariosFarmacia
                .Include(u => u.Farmacia);

            // Administrador só vê usuários da própria farmácia
            if (!UserRoles.IsGestorLike(currentRole) && int.TryParse(farmaciaIdClaim, out int farmaciaId))
            {
                query = query.Where(u => u.FarmaciaId == farmaciaId);
            }

            // Filtro opcional por role
            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            var total = await query.CountAsync();
            var usuarios = await query
                .OrderByDescending(u => u.DataCadastro)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Role,
                    RoleDisplayName = u.NomeRole,
                    u.Ativo,
                    u.EmailConfirmado,
                    u.DataCadastro,
                    u.UltimoLogin,
                    u.Turno,
                    u.RecebeNotificacoes,
                    u.TotalAtendimentos,
                    u.AtendimentosHoje,
                    u.FarmaciaId,
                    FarmaciaNome = u.Farmacia.Nome
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = usuarios,
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
            _logger.LogError(ex, "Erro ao listar usuários");
            return StatusCode(500, new { Success = false, Message = "Erro ao listar usuários" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUsuario(int id)
    {
        try
        {
            var usuario = await _context.UsuariosFarmacia
                .Include(u => u.Farmacia)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound(new { Success = false, Message = "Usuário não encontrado" });

            if (!CanAccessUser(usuario))
                return Forbid();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Telefone,
                    usuario.Role,
                    RoleDisplayName = usuario.NomeRole,
                    usuario.Ativo,
                    usuario.EmailConfirmado,
                    usuario.DataCadastro,
                    usuario.UltimoLogin,
                    usuario.Turno,
                    usuario.RecebeNotificacoes,
                    usuario.TotalAtendimentos,
                    usuario.AtendimentosHoje,
                    usuario.FarmaciaId,
                    FarmaciaNome = usuario.Farmacia?.Nome
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuário {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao buscar usuário" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateUsuario([FromBody] CreateUsuarioRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nome) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new { Success = false, Message = "Nome, Email, Password e Role são obrigatórios" });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { Success = false, Message = "A senha deve ter no mínimo 6 caracteres" });
            }

            if (!UserRoles.AllRoles.Contains(request.Role))
            {
                return BadRequest(new { Success = false, Message = $"Role inválida. Valores aceitos: {string.Join(", ", UserRoles.AllRoles)}" });
            }

            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            if (!UserRoles.CanManageUsers(currentRole, request.Role))
            {
                return Forbid();
            }

            // Para Administrador, forçar a farmácia do usuário logado
            int targetFarmaciaId;
            if (UserRoles.IsGestorLike(currentRole) && request.FarmaciaId.HasValue)
            {
                targetFarmaciaId = request.FarmaciaId.Value;
            }
            else
            {
                var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
                if (!int.TryParse(farmaciaIdClaim, out targetFarmaciaId))
                {
                    return BadRequest(new { Success = false, Message = "FarmaciaId não identificada" });
                }
            }

            // Verificar se farmácia existe
            var farmacia = await _context.Farmacias.FindAsync(targetFarmaciaId);
            if (farmacia == null)
            {
                return BadRequest(new { Success = false, Message = "Farmácia não encontrada" });
            }

            // Verificar se email já existe nesta farmácia
            var emailExists = await _context.UsuariosFarmacia
                .AnyAsync(u => u.FarmaciaId == targetFarmaciaId && u.Email == request.Email);
            if (emailExists)
            {
                return BadRequest(new { Success = false, Message = "Email já cadastrado nesta farmácia" });
            }

            var (hash, salt) = GeneratePasswordHash(request.Password);

            var usuario = new UsuarioFarmacia
            {
                Nome = request.Nome,
                Email = request.Email,
                Telefone = request.Telefone ?? "",
                Role = request.Role,
                PasswordHash = hash,
                PasswordSalt = salt,
                Ativo = true,
                EmailConfirmado = true,
                FarmaciaId = targetFarmaciaId,
                Farmacia = farmacia,
                DataCadastro = DateTime.UtcNow,
                Turno = request.Turno ?? "Integral",
                RecebeNotificacoes = request.RecebeNotificacoes ?? true,
                TotalAtendimentos = 0,
                AtendimentosHoje = 0
            };

            _context.UsuariosFarmacia.Add(usuario);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuário criado: {Email} (ID: {Id}) na farmácia {FarmaciaId}",
                usuario.Email, usuario.Id, usuario.FarmaciaId);

            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, new
            {
                Success = true,
                Data = new
                {
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Telefone,
                    usuario.Role,
                    usuario.Ativo,
                    usuario.FarmaciaId,
                    usuario.DataCadastro
                },
                Message = "Usuário criado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
            return StatusCode(500, new { Success = false, Message = "Erro ao criar usuário" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UpdateUsuarioRequest request)
    {
        try
        {
            var usuario = await _context.UsuariosFarmacia.FindAsync(id);
            if (usuario == null)
                return NotFound(new { Success = false, Message = "Usuário não encontrado" });

            if (!CanAccessUser(usuario))
                return Forbid();

            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            // Se mudando a role, verificar permissão
            if (!string.IsNullOrWhiteSpace(request.Role) && request.Role != usuario.Role)
            {
                if (!UserRoles.AllRoles.Contains(request.Role))
                {
                    return BadRequest(new { Success = false, Message = $"Role inválida. Valores aceitos: {string.Join(", ", UserRoles.AllRoles)}" });
                }
                if (!UserRoles.CanManageUsers(currentRole, request.Role))
                {
                    return Forbid();
                }
                usuario.Role = request.Role;
            }

            if (!string.IsNullOrWhiteSpace(request.Nome))
                usuario.Nome = request.Nome;

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != usuario.Email)
            {
                var emailExists = await _context.UsuariosFarmacia
                    .AnyAsync(u => u.FarmaciaId == usuario.FarmaciaId && u.Email == request.Email && u.Id != id);
                if (emailExists)
                {
                    return BadRequest(new { Success = false, Message = "Email já cadastrado nesta farmácia" });
                }
                usuario.Email = request.Email;
            }

            if (request.Telefone != null)
                usuario.Telefone = request.Telefone;

            if (request.Turno != null)
                usuario.Turno = request.Turno;

            if (request.RecebeNotificacoes.HasValue)
                usuario.RecebeNotificacoes = request.RecebeNotificacoes.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuário atualizado: {Email} (ID: {Id})", usuario.Email, usuario.Id);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Telefone,
                    usuario.Role,
                    usuario.Ativo,
                    usuario.Turno,
                    usuario.RecebeNotificacoes,
                    usuario.FarmaciaId
                },
                Message = "Usuário atualizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar usuário {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao atualizar usuário" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUsuario(int id)
    {
        try
        {
            var usuario = await _context.UsuariosFarmacia.FindAsync(id);
            if (usuario == null)
                return NotFound(new { Success = false, Message = "Usuário não encontrado" });

            if (!CanAccessUser(usuario))
                return Forbid();

            // Não permitir desativar o próprio usuário
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
            {
                return BadRequest(new { Success = false, Message = "Não é possível desativar o próprio usuário" });
            }

            usuario.Ativo = false;
            usuario.DataInativacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuário desativado: {Email} (ID: {Id})", usuario.Email, usuario.Id);

            return Ok(new { Success = true, Message = "Usuário desativado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desativar usuário {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao desativar usuário" });
        }
    }

    [HttpPut("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { Success = false, Message = "Nova senha é obrigatória" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { Success = false, Message = "A senha deve ter no mínimo 6 caracteres" });
            }

            var usuario = await _context.UsuariosFarmacia.FindAsync(id);
            if (usuario == null)
                return NotFound(new { Success = false, Message = "Usuário não encontrado" });

            if (!CanAccessUser(usuario))
                return Forbid();

            var (hash, salt) = GeneratePasswordHash(request.NewPassword);
            usuario.PasswordHash = hash;
            usuario.PasswordSalt = salt;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Senha redefinida para usuário: {Email} (ID: {Id})", usuario.Email, usuario.Id);

            return Ok(new { Success = true, Message = "Senha redefinida com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao redefinir senha do usuário {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao redefinir senha" });
        }
    }

    private bool CanAccessUser(UsuarioFarmacia usuario)
    {
        var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (UserRoles.IsGestorLike(currentRole))
            return true;

        var farmaciaIdClaim = User.FindFirst("FarmaciaId")?.Value;
        if (int.TryParse(farmaciaIdClaim, out int farmaciaId))
        {
            return usuario.FarmaciaId == farmaciaId;
        }

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
