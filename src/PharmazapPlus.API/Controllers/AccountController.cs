using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.Login;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
	private readonly IConfiguration _configuration;
	private readonly PharmazapDbContext _context;
	private readonly ILogger<AccountController> _logger;
	private readonly TokenProvider _tokenProvider;
	private readonly IWebHostEnvironment? _webHostEnvironment;

	public AccountController(
		IConfiguration configuration,
		PharmazapDbContext context,
		ILogger<AccountController> logger,
		TokenProvider tokenProvider,
		IWebHostEnvironment? webHostEnvironment = null)
	{
		_configuration = configuration;
		_context = context;
		_logger = logger;
		_tokenProvider = tokenProvider;
		_webHostEnvironment = webHostEnvironment;
	}

	[HttpPost("login")]
	[AllowAnonymous]
	public async Task<IActionResult> Login([FromBody] LoginRequest request)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
			{
				return BadRequest(new { Success = false, Message = "Email e senha são obrigatórios" });
			}

			var user = await _context.UsuariosFarmacia
				.Include(u => u.Farmacia)
				.FirstOrDefaultAsync(u => u.Email == request.Email && u.Ativo);

			if (user == null)
			{
				_logger.LogWarning("Login failed: user not found for email {Email}", request.Email);
				return Unauthorized(new { Success = false, Message = "Usuário não encontrado" });
			}

			if (user.Farmacia is { Ativa: false })
			{
				_logger.LogWarning("Login blocked for user {Email}: farmácia {FarmaciaId} desativada", request.Email, user.FarmaciaId);
				return Unauthorized(new { Success = false, Message = "Contacte seu administrador" });
			}

			if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
			{
				_logger.LogWarning("Login failed: invalid password for user {Email}", request.Email);
				return Unauthorized(new { Success = false, Message = "Senha incorreta" });
			}

			var token = _tokenProvider.GenerateToken(user);

			user.UltimoLogin = DateTime.UtcNow;
			await _context.SaveChangesAsync();

			_logger.LogInformation("Login successful for: {Email}", request.Email);

			// LGPD: check if Administrador needs to accept terms on first login
			if (user.Role == UserRoles.Administrador && user.Farmacia == null)
			{
				_logger.LogWarning("Administrador {UserId} has no associated Farmacia — cannot check TermoAceite", user.Id);
			}

			var termoAceiteRequired = user.Role == UserRoles.Administrador
				&& user.Farmacia != null
				&& !user.Farmacia.TermoAceite;

			return Ok(new
			{
				Success = true,
				Data = new
				{
					Id = user.Id.ToString(),
					user.Email,
					Name = user.Nome,
					user.Role,
					FarmaciaId = user.FarmaciaId.ToString(),
					FarmaciaNome = user.Farmacia?.Nome ?? "Sistema",
					FarmaciaLogoUrl = user.Farmacia?.LogoUrl,
					Token = token.AccessToken,
					Expiration = DateTime.UtcNow.AddHours(8),
					Theme = user.Theme ?? "Light",
					AccentColor = user.AccentColor,
					TermoAceiteRequired = termoAceiteRequired
				}
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during login for: {Email}", request.Email);
			return StatusCode(500, new { Success = false, Message = "Erro interno ao processar login" });
		}
	}

	[HttpGet("userinfo")]
	[Authorize]
	public async Task<IActionResult> GetUserInfo()
	{
		try
		{
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			UsuarioFarmacia? user = null;
			if (int.TryParse(userIdClaim, out var userId))
			{
				user = await _context.UsuariosFarmacia
					.Include(u => u.Farmacia)
					.FirstOrDefaultAsync(u => u.Id == userId && u.Ativo);
			}

			var userClaims = new
			{
				Id = user?.Id.ToString() ?? userIdClaim,
				Email = user?.Email ?? User.FindFirst(ClaimTypes.Email)?.Value,
				Name = user?.Nome ?? User.FindFirst(ClaimTypes.Name)?.Value,
				Role = user?.Role ?? User.FindFirst(ClaimTypes.Role)?.Value ?? UserRoles.Atendente,
				FarmaciaId = user?.FarmaciaId.ToString() ?? User.FindFirst("FarmaciaId")?.Value,
				FarmaciaNome = user?.Farmacia?.Nome ?? User.FindFirst("FarmaciaNome")?.Value,
				FarmaciaLogoUrl = user?.Farmacia?.LogoUrl
			};

			_logger.LogInformation("User info retrieved for: {Email}", userClaims.Email);

			return Ok(new
			{
				Success = true,
				Data = userClaims,
				Application = _configuration["Application:Name"],
				Timestamp = DateTime.UtcNow
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting user info");
			return StatusCode(500, new { Success = false, Message = "Erro interno" });
		}
	}

	[HttpPost("aceitar-termos")]
	[Authorize(Roles = UserRoles.Administrador)]
	public async Task<IActionResult> AceitarTermos()
	{
		try
		{
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!int.TryParse(userIdClaim, out var userId))
				return Unauthorized(new { Success = false, Message = "Usuário não identificado" });

			var user = await _context.UsuariosFarmacia
				.Include(u => u.Farmacia)
				.FirstOrDefaultAsync(u => u.Id == userId && u.Ativo);

			if (user == null)
				return NotFound(new { Success = false, Message = "Usuário não encontrado" });

			if (user.Farmacia == null)
				return BadRequest(new { Success = false, Message = "Farmácia não encontrada" });

			user.Farmacia.TermoAceite = true;
			user.Farmacia.TermoAceiteDataHora = DateTime.UtcNow;
			await _context.SaveChangesAsync();

			_logger.LogInformation("Termos de uso aceitos para farmácia {FarmaciaId} pelo usuário {UserId}", user.FarmaciaId, userId);

			return Ok(new { Success = true, Message = "Termos de uso aceitos com sucesso" });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao registrar aceite dos termos de uso");
			return StatusCode(500, new { Success = false, Message = "Erro interno ao registrar aceite" });
		}
	}

	[HttpPost("register-farmacia")]
	[Authorize(Roles = UserRoles.Gestor)]
	public async Task<IActionResult> RegisterFarmacia([FromBody] RegisterFarmaciaRequest request)
	{
		try
		{
			var cleanCnpj = NormalizeDigits(request.Cnpj, 14);

			// Validação
			if (await _context.Farmacias.AnyAsync(f => f.Cnpj == cleanCnpj))
			{
				return BadRequest(new { Success = false, Message = "CNPJ já cadastrado" });
			}

			var duplicateCheck = await HasDuplicateFarmaciaAsync(
				cleanCnpj,
				request.Email,
				request.Telefone);
			if (duplicateCheck)
			{
				return Conflict(new { Success = false, Message = "Já existe uma farmácia cadastrada com os mesmos dados de CNPJ, email ou telefone." });
			}

			// Cria farmácia
			var farmacia = new Farmacia
			{
				Nome = request.Nome,
				Cnpj = cleanCnpj,
				Telefone = request.Telefone ?? "",
				Email = NormalizeEmail(request.Email),
				Endereco = request.Endereco ?? "",
				LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
				Cidade = "",
				Estado = "",
				Cep = "",
				DataCadastro = DateTime.UtcNow,
				DataAtivacao = DateTime.UtcNow,
				Status = (Shared.Models.Core.FarmaciaStatus)FarmaciaStatus.Ativa,
				Ativa = true,
				MotivoBloqueio = null,
				Plano = request.Plano ?? "Basic",
				Integracoes = new FarmaciaIntegracoes
				{
					WhatsAppPhoneNumberId = "",
					WhatsAppAccessToken = "",
					WhatsAppBusinessAccountId = "",
					ChaveWhatsApp = Guid.NewGuid().ToString(),
					CreatedAt = DateTime.UtcNow
				}
			};

			_context.Farmacias.Add(farmacia);
			await _context.SaveChangesAsync();

			// Cria usuário administrador padrão sem senha — login bloqueado até configuração via painel.
			// VerifyPassword retorna false para PasswordHash/PasswordSalt vazios, impedindo acesso não autorizado.
			var adminUser = new UsuarioFarmacia
			{
				FarmaciaId = farmacia.Id,
				Farmacia = farmacia,
				Nome = request.NomeContato,
				Email = request.EmailContato,
				Telefone = request.TelefoneContato ?? "",
				Role = UserRoles.Administrador,
				DataCadastro = DateTime.UtcNow,
				Ativo = true,
				PasswordHash = "",
				PasswordSalt = "",
				Turno = "Integral"
			};

			_context.UsuariosFarmacia.Add(adminUser);
			await _context.SaveChangesAsync();

            await DatabaseSeeder.EnsureFarmaciaDefaultsAsync(_context, _logger, farmacia.Id);

			_logger.LogInformation("Farmácia registrada: {Nome} (ID: {Id})", farmacia.Nome, farmacia.Id);

			return Ok(new
			{
				Success = true,
				Data = new { farmacia.Id, farmacia.Nome, ChaveWhatsApp = farmacia.Integracoes!.ChaveWhatsApp },
				Message = "Farmácia registrada com sucesso. Chave WhatsApp gerada."
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error registering farmácia");
			return StatusCode(500, new { Success = false, Message = $"Erro ao registrar farmácia [{ex.Message}]" });
		}
	}

	[HttpGet("farmacias")]
	[Authorize(Roles = UserRoles.Gestor)]
	public async Task<IActionResult> GetFarmacias([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
	{
		try
		{
			var query = _context.Farmacias
				.OrderByDescending(f => f.DataCadastro);

			var total = await query.CountAsync();
			var farmacias = await query
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(f => new
				{
					f.Id,
					f.Nome,
					f.Cnpj,
					f.Telefone,
					f.Email,
					f.Endereco,
					f.LogoUrl,
					f.DataCadastro,
					f.Plano,
					f.Ativa,
					f.MotivoBloqueio,
					TotalAtendentes = _context.UsuariosFarmacia
						.Count(u => u.FarmaciaId == f.Id && u.Ativo && u.Role == UserRoles.Atendente),
					TotalAdministradores = _context.UsuariosFarmacia
						.Count(u => u.FarmaciaId == f.Id && u.Ativo && u.Role == UserRoles.Administrador)
				})
				.ToListAsync();

			return Ok(new
			{
				Success = true,
				Data = farmacias,
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
			_logger.LogError(ex, "Error getting farmácias");
			return StatusCode(500, new { Success = false, Message = "Erro ao listar farmácias" });
		}
	}

	[HttpPut("update-farmacia/{id}")]
	[Authorize(Roles = UserRoles.Gestor)]
	public async Task<IActionResult> UpdateFarmacia(int id, [FromBody] UpdateFarmaciaRequest request)
	{
		try
		{
			var farmacia = await _context.Farmacias.FindAsync(id);
			if (farmacia == null)
				return NotFound(new { Success = false, Message = "Farmácia não encontrada" });

			if (!string.IsNullOrWhiteSpace(request.Cnpj))
			{
				var cleanCnpj = NormalizeDigits(request.Cnpj, 14);
				if (await _context.Farmacias.AnyAsync(f => f.Cnpj == cleanCnpj && f.Id != id))
					return BadRequest(new { Success = false, Message = "CNPJ já cadastrado em outra farmácia" });
				farmacia.Cnpj = cleanCnpj;
			}

			var currentCnpj = NormalizeDigits(farmacia.Cnpj, 14);
			var currentEmail = NormalizeEmail(farmacia.Email);
			var currentTelefone = NormalizeDigits(farmacia.Telefone, 11);
			var proposedCnpj = request.Cnpj is null ? currentCnpj : NormalizeDigits(request.Cnpj, 14);
			var proposedEmail = request.Email is null ? currentEmail : NormalizeEmail(request.Email);
			var proposedTelefone = request.Telefone is null ? currentTelefone : NormalizeDigits(request.Telefone, 11);
			var identityChanged = proposedCnpj != currentCnpj ||
				proposedEmail != currentEmail ||
				proposedTelefone != currentTelefone;

			if (identityChanged &&
				await HasDuplicateFarmaciaAsync(proposedCnpj, proposedEmail, proposedTelefone, id))
			{
				return Conflict(new { Success = false, Message = "Já existe uma farmácia cadastrada com os mesmos dados de CNPJ, email ou telefone." });
			}

			if (!string.IsNullOrWhiteSpace(request.Nome))
				farmacia.Nome = request.Nome.Trim();
			if (request.Telefone != null)
				farmacia.Telefone = request.Telefone;
			if (request.Email != null)
				farmacia.Email = NormalizeEmail(request.Email);
			if (request.Endereco != null)
				farmacia.Endereco = request.Endereco.Trim();
			if (request.LogoUrl != null)
				farmacia.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
			if (!string.IsNullOrWhiteSpace(request.Plano))
				farmacia.Plano = request.Plano;

			if (request.Ativa == false && string.IsNullOrWhiteSpace(request.MotivoBloqueio))
				return BadRequest(new { Success = false, Message = "Informe o motivo do bloqueio da farmácia." });

			if (request.Ativa.HasValue)
			{
				farmacia.Ativa = request.Ativa.Value;
				farmacia.Status = request.Ativa.Value
					? Shared.Models.Core.FarmaciaStatus.Ativa
					: Shared.Models.Core.FarmaciaStatus.Suspensa;
				farmacia.DataAtivacao = request.Ativa.Value ? DateTime.UtcNow : farmacia.DataAtivacao;
				farmacia.DataSuspensao = request.Ativa.Value ? null : DateTime.UtcNow;

				if (request.Ativa.Value)
					farmacia.MotivoBloqueio = null;
			}

			if (request.MotivoBloqueio != null)
				farmacia.MotivoBloqueio = string.IsNullOrWhiteSpace(request.MotivoBloqueio) ? null : request.MotivoBloqueio.Trim();

			await _context.SaveChangesAsync();
			_logger.LogInformation("Farmácia atualizada: {Nome} (ID: {Id})", farmacia.Nome, farmacia.Id);

			return Ok(new { Success = true, Message = "Farmácia atualizada com sucesso." });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating farmácia {Id}", id);
			return StatusCode(500, new { Success = false, Message = $"Erro ao atualizar farmácia [{ex.Message}]" });
		}
	}

    [HttpGet("farmacia/{id}/departamentos")]
    [Authorize(Roles = UserRoles.Gestor)]
    public async Task<IActionResult> GetFarmaciaDepartamentos(int id, [FromQuery] bool? ativo = null)
    {
        try
        {
            var farmaciaExists = await _context.Farmacias.AnyAsync(f => f.Id == id);
            if (!farmaciaExists)
                return NotFound(new { Success = false, Message = "Farmácia não encontrada" });

            IQueryable<Departamento> query = _context.Departamentos
                .Where(d => d.FarmaciaId == id);

            if (ativo.HasValue)
                query = query.Where(d => d.Ativo == ativo.Value);

            var departamentos = await query
                .OrderBy(d => d.Nome)
                .Select(d => new
                {
                    d.Id,
                    d.Nome,
                    d.Ativo,
                    d.FarmaciaId
                })
                .ToListAsync();

            return Ok(new { Success = true, Data = departamentos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting departamentos for farmácia {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Erro ao carregar departamentos da farmácia" });
        }
    }

	[HttpPost("upload-farmacia-logo")]
	[Authorize(Roles = UserRoles.Gestor)]
	public async Task<IActionResult> UploadFarmaciaLogo(IFormFile file)
	{
		try
		{
			if (file == null || file.Length == 0)
				return BadRequest(new { Success = false, Message = "Arquivo não enviado" });

			var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
			if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
				return BadRequest(new { Success = false, Message = "Tipo de arquivo não permitido. Envie uma imagem (JPEG, PNG, GIF ou WebP)" });

			if (file.Length > 5 * 1024 * 1024)
				return BadRequest(new { Success = false, Message = "Arquivo muito grande. Tamanho máximo: 5MB" });

			var allowedExtensions = Consts.FarmaciaLogo.AllowedExtensions;
			var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
			if (!allowedExtensions.Contains(extension))
				return BadRequest(new { Success = false, Message = "Extensão de arquivo não permitida." });

			using var headerStream = file.OpenReadStream();
			var headerBytes = new byte[8];
			var bytesRead = await headerStream.ReadAsync(headerBytes, 0, headerBytes.Length);
			if (!headerStream.CanSeek)
				return BadRequest(new { Success = false, Message = "Não foi possível processar o arquivo enviado." });

			headerStream.Position = 0;

			if (!IsValidImageMagicBytes(headerBytes, bytesRead))
				return BadRequest(new { Success = false, Message = "O conteúdo do arquivo não corresponde a uma imagem válida." });

			var contentRoot = _webHostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();
			var uploadsDir = Path.Combine(contentRoot, Consts.FarmaciaLogo.UploadsRelativePath);
			Directory.CreateDirectory(uploadsDir);

			var fileName = $"{Guid.NewGuid()}{extension}";
			var filePath = Path.Combine(uploadsDir, fileName);

			var resolvedPath = Path.GetFullPath(filePath);
			var resolvedDir = Path.GetFullPath(uploadsDir);
			if (!resolvedPath.StartsWith(resolvedDir + Path.DirectorySeparatorChar) && resolvedPath != resolvedDir)
				return BadRequest(new { Success = false, Message = "Caminho de arquivo inválido." });

			await using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await headerStream.CopyToAsync(stream);
			}

			var logoUrl = string.Format(Consts.FarmaciaLogo.UrlFormat, fileName, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			return Ok(new { Success = true, LogoUrl = logoUrl, Message = "Logo enviada com sucesso." });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error uploading farmacia logo");
			return StatusCode(500, new { Success = false, Message = "Erro ao fazer upload da logo" });
		}
	}

	/// <summary>
	/// Serves a previously-uploaded farmácia logo image.
	/// AllowAnonymous so browsers can load the image in img tags without authentication headers.
	/// </summary>
	[HttpGet(Consts.FarmaciaLogo.ServeEndpointRoute)]
	[AllowAnonymous]
	public IActionResult GetFarmaciaLogo(string fileName)
	{
		// Prevent path traversal: use only the base file name, ignoring any directory components
		var cleanName = Path.GetFileName(fileName);
		if (string.IsNullOrWhiteSpace(cleanName))
			return NotFound();

		var extension = Path.GetExtension(cleanName).ToLowerInvariant();
		if (!Consts.FarmaciaLogo.AllowedExtensions.Contains(extension))
			return NotFound();

		var contentRoot = _webHostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();
		var uploadsDir = Path.GetFullPath(Path.Combine(contentRoot, Consts.FarmaciaLogo.UploadsRelativePath));
		var filePath = Path.GetFullPath(Path.Combine(uploadsDir, cleanName));

		// Double-check the resolved path is inside the uploads directory
		if (!filePath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			return NotFound();

		if (!System.IO.File.Exists(filePath))
			return NotFound();

		// Cache for 1 year; cache-busting is done via the ?v= query parameter
		Response.Headers["Cache-Control"] = Consts.FarmaciaLogo.CacheControlValue;
		return PhysicalFile(filePath, Consts.FarmaciaLogo.GetContentType(extension));
	}

	private static bool IsValidImageMagicBytes(byte[] bytes, int bytesRead)
	{
		if (bytesRead >= 3 &&
			bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
			return true;

		if (bytesRead >= 8 &&
			bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
			bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
			return true;

		if (bytesRead >= 6 &&
			bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
			bytes[3] == 0x38 && (bytes[4] == 0x39 || bytes[4] == 0x37) && bytes[5] == 0x61)
			return true;

		if (bytesRead >= 4 &&
			bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
			return true;

		return false;
	}

	[HttpGet("farmacia/{id}/dashboard")]
	[Authorize(Roles = "Administrador,Gestor")]
	public async Task<IActionResult> GetFarmaciaDashboard(int id)
	{
		try
		{
			var userFarmaciaId = User.FindFirst("FarmaciaId")?.Value;

			// Verifica permissão
			if (!User.IsInRole(UserRoles.Gestor) &&
				(!int.TryParse(userFarmaciaId, out int userFarmId) || userFarmId != id))
			{
				return Forbid();
			}

			var farmacia = await _context.Farmacias
				.Include(f => f.Usuarios)
				.Include(f => f.Clientes)
				.FirstOrDefaultAsync(f => f.Id == id);

			if (farmacia == null)
			{
				return NotFound(new { Success = false, Message = "Farmácia não encontrada" });
			}

			var dashboard = new
			{
				farmacia.Id,
				farmacia.Nome,
				farmacia.Plano,
				TotalAtendentes = farmacia.Usuarios.Count(u => u.Ativo),
				TotalClientes = farmacia.Clientes.Count(c => c.Ativo),
				TotalAtendimentos = await _context.Atendimentos
					.Where(a => a.FarmaciaId == id && a.DataFim.HasValue)
					.CountAsync(),
				AtendimentosHoje = await _context.Atendimentos
					.Where(a => a.FarmaciaId == id &&
							   a.DataInicio.Date == DateTime.UtcNow.Date)
					.CountAsync(),
				AtendimentosAndamento = await _context.Atendimentos
					.Where(a => a.FarmaciaId == id &&
                               a.StatusEnum == AtendimentoStatusEnum.EmAndamento)
					.CountAsync()
			};

			return Ok(new { Success = true, Data = dashboard });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting farmácia dashboard");
			return StatusCode(500, new { Success = false, Message = "Erro ao carregar dashboard" });
		}
	}

	/// <summary>
	/// Retorna estatísticas do dashboard de acordo com o papel do usuário autenticado.
	/// Gestor: estatísticas globais (farmacias, usuários, atendimentos).
	/// Administrador: estatísticas da sua farmácia.
	/// Atendente: estatísticas pessoais (pendentes, em andamento, finalizados hoje).
	/// </summary>
	[HttpGet("dashboard-stats")]
	[Authorize]
	public async Task<IActionResult> GetDashboardStats()
	{
		try
		{
			var today = DateTime.UtcNow.Date;

			if (User.IsInRole(UserRoles.Gestor))
			{
				var totalFarmacias = await _context.Farmacias
					.Where(f => (FarmaciaStatus)f.Status == FarmaciaStatus.Ativa)
					.CountAsync();
				var totalAdministradores = await _context.UsuariosFarmacia
					.Where(u => u.Ativo && u.Role == UserRoles.Administrador)
					.CountAsync();
				var atendimentosHoje = await _context.Atendimentos
					.Where(a => a.DataInicio.Date == today)
					.CountAsync();
				var atendimentosEmAndamento = await _context.Atendimentos
                   .Where(a => a.StatusEnum == AtendimentoStatusEnum.EmAndamento)
					.CountAsync();

				return Ok(new
				{
					Success = true,
					Data = new
					{
						TotalFarmacias = totalFarmacias,
						TotalAdministradores = totalAdministradores,
						AtendimentosHoje = atendimentosHoje,
						AtendimentosEmAndamento = atendimentosEmAndamento
					}
				});
			}

			var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
			if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
				return BadRequest(new { Success = false, Message = "Farmácia não identificada" });

			if (User.IsInRole(UserRoles.Administrador))
			{
				var totalAtendentes = await _context.UsuariosFarmacia
					.Where(u => u.FarmaciaId == farmaciaId && u.Ativo && u.Role == UserRoles.Atendente)
					.CountAsync();
				var totalClientes = await _context.Clientes
					.Where(c => c.FarmaciaId == farmaciaId && c.Ativo)
					.CountAsync();
				var atendimentosHoje = await _context.Atendimentos
					.Where(a => a.FarmaciaId == farmaciaId && a.DataInicio.Date == today)
					.CountAsync();
				var atendimentosEmAndamento = await _context.Atendimentos
                 .Where(a => a.FarmaciaId == farmaciaId && a.StatusEnum == AtendimentoStatusEnum.EmAndamento)
					.CountAsync();

				return Ok(new
				{
					Success = true,
					Data = new
					{
						TotalAtendentes = totalAtendentes,
						TotalClientes = totalClientes,
						AtendimentosHoje = atendimentosHoje,
						AtendimentosEmAndamento = atendimentosEmAndamento
					}
				});
			}

			// Atendente
			var atendenteIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
			int.TryParse(atendenteIdStr, out var atendenteId);

			var pendentes = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmaciaId && a.StatusEnum == AtendimentoStatusEnum.Pendente)
				.CountAsync();
			var emAndamento = await _context.Atendimentos
             .Where(a => a.FarmaciaId == farmaciaId && a.AtendenteId == atendenteId && a.StatusEnum == AtendimentoStatusEnum.EmAndamento)
				.CountAsync();
			var finalizadosHoje = await _context.Atendimentos
				.Where(a => a.FarmaciaId == farmaciaId && a.AtendenteId == atendenteId &&
                          a.StatusEnum == AtendimentoStatusEnum.Finalizado &&
							a.DataFim.HasValue && a.DataFim.Value.Date == today)
				.CountAsync();

			return Ok(new
			{
				Success = true,
				Data = new
				{
					AtendimentosPendentes = pendentes,
					AtendimentosEmAndamento = emAndamento,
					AtendimentosFinalizadosHoje = finalizadosHoje
				}
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting dashboard stats");
			return StatusCode(500, new { Success = false, Message = "Erro ao carregar estatísticas" });
		}
	}

	[HttpGet("recent-activity")]
	[Authorize]
	public async Task<IActionResult> GetRecentActivity([FromQuery] int limit = 20)
	{
		try
		{
			var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
			var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!int.TryParse(userIdStr, out var userId) || userId <= 0)
				return BadRequest(new { Success = false, Message = "Usuário não identificado" });

			IQueryable<AtividadeRecente> query = _context.AtividadesRecentes
				.Where(a => a.UsuarioId == userId);

			if (int.TryParse(farmaciaIdStr, out var farmaciaId))
			{
				query = query.Where(a => a.FarmaciaId == farmaciaId);
			}
			else if (!User.IsInRole(UserRoles.Gestor))
			{
				return BadRequest(new { Success = false, Message = "Farmácia não identificada" });
			}

			query = query.OrderByDescending(a => a.DataHora);

			var activities = await query
				.Take(limit)
				.Select(a => new
				{
					a.Id,
					a.UsuarioId,
					a.UsuarioNome,
					TipoAtividade = a.TipoAtividade.ToString(),
					a.Descricao,
					a.AtendimentoId,
					a.ClienteNome,
					a.DataHora
				})
				.ToListAsync();

			return Ok(new { Success = true, Data = activities });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting recent activity");
			return StatusCode(500, new { Success = false, Message = "Erro ao carregar atividades recentes" });
		}
	}

	private async Task<bool> HasDuplicateFarmaciaAsync(string? cnpj, string? email, string? telefone, int? currentId = null)
	{
		var normalizedCnpj = NormalizeDigits(cnpj, 14);
		var normalizedEmail = NormalizeEmail(email);
		var normalizedTelefone = NormalizeDigits(telefone, 11);

		var farmacias = await _context.Farmacias
			.AsNoTracking()
			.Where(f => !currentId.HasValue || f.Id != currentId.Value)
			.Select(f => new { f.Id, f.Cnpj, f.Email, f.Telefone })
			.ToListAsync();

		return farmacias.Any(f =>
			(!string.IsNullOrWhiteSpace(normalizedCnpj) && NormalizeDigits(f.Cnpj, 14) == normalizedCnpj) ||
			(!string.IsNullOrWhiteSpace(normalizedEmail) && NormalizeEmail(f.Email) == normalizedEmail) ||
			(!string.IsNullOrWhiteSpace(normalizedTelefone) && NormalizeDigits(f.Telefone, 11) == normalizedTelefone));
	}

	private static string NormalizeDigits(string? value, int maxDigits)
		=> value.SanitizeDigits(maxDigits);

	private static string NormalizeEmail(string? value)
		=> string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

	private bool VerifyPassword(string password, string storedHash, string storedSalt)
	{
		if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
			return false;

		using var sha256 = SHA256.Create();
		var passwordBytes = Encoding.UTF8.GetBytes(password + storedSalt);
		var hashBytes = sha256.ComputeHash(passwordBytes);
		var computedHash = Convert.ToBase64String(hashBytes);

		return computedHash == storedHash;
	}

	[HttpPut("theme")]
	[Authorize]
	public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest request)
	{
		try
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!int.TryParse(userId, out int id))
				return BadRequest(new { Success = false, Message = "ID de usuário inválido" });

			var user = await _context.UsuariosFarmacia.FindAsync(id);
			if (user == null)
				return NotFound(new { Success = false, Message = "Usuário não encontrado" });

			user.Theme = request.Theme == "Dark" ? "Dark" : "Light";
			await _context.SaveChangesAsync();

			return Ok(new { Success = true });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating theme for user");
			return StatusCode(500, new { Success = false, Message = "Erro ao atualizar tema" });
		}
	}

	[HttpPut("accent-color")]
	[Authorize]
	public async Task<IActionResult> UpdateAccentColor([FromBody] UpdateAccentColorRequest request)
	{
		try
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (!int.TryParse(userId, out int id))
				return BadRequest(new { Success = false, Message = "ID de usuário inválido" });

			var user = await _context.UsuariosFarmacia.FindAsync(id);
			if (user == null)
				return NotFound(new { Success = false, Message = "Usuário não encontrado" });

			user.AccentColor = request.AccentColor;
			await _context.SaveChangesAsync();

			return Ok(new { Success = true });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating accent color for user");
			return StatusCode(500, new { Success = false, Message = "Erro ao atualizar cor de destaque" });
		}
	}
}
