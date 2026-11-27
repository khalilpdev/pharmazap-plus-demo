using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Services.WhatsApp;

/// <summary>
/// Serviço para gerenciar bloqueio de remetentes (lista negra de spam).
/// Suporta bloqueios locais (por farmácia) e globais (todas as farmácias).
/// </summary>
public interface IRemetenteBloqueadoService
{
    /// <summary>
    /// Verifica se um remetente está bloqueado (considera bloqueios local e global).
    /// </summary>
    /// <param name="numeroDDD">Número de telefone normalizado (ex: "12998803214")</param>
    /// <param name="farmaciaId">ID da farmácia para verificação local</param>
    /// <returns>True se bloqueado localmente OU globalmente</returns>
    Task<bool> IsRemetentoBloqueadoAsync(string numeroDDD, int farmaciaId);

    /// <summary>
    /// Bloqueia um remetente com escopo definido (local ou global).
    /// </summary>
    /// <param name="numeroDDD">Número de telefone a normalizar</param>
    /// <param name="farmaciaId">ID da farmácia (obrigatório)</param>
    /// <param name="escopo">Local ou Global</param>
    /// <param name="usuarioBloqueadorId">ID do usuário que está bloqueando</param>
    /// <param name="nomeUsuarioBloqueador">Nome do usuário que está bloqueando</param>
    /// <param name="motivo">Motivo do bloqueio (opcional)</param>
    /// <returns>O RemetenteBloqueado criado/atualizado</returns>
    Task<RemetenteBloqueado> BloqueRemetenteAsync(
        string numeroDDD,
        int farmaciaId,
        RemetentoBloqueioEscopoEnum escopo,
        int usuarioBloqueadorId,
        string nomeUsuarioBloqueador,
        string? motivo = null);

    /// <summary>
    /// Desbloqueia um remetente.
    /// </summary>
    /// <param name="numeroDDD">Número de telefone normalizado</param>
    /// <param name="farmaciaId">ID da farmácia</param>
    /// <param name="escopo">Local ou Global</param>
    /// <param name="usuarioDesbloqueadorId">ID do usuário que está desbloqueando</param>
    /// <param name="nomeUsuarioDesbloqueador">Nome do usuário que está desbloqueando</param>
    Task DesbloqueRemetenteAsync(
        string numeroDDD,
        int farmaciaId,
        RemetentoBloqueioEscopoEnum escopo,
        int usuarioDesbloqueadorId,
        string nomeUsuarioDesbloqueador);

    /// <summary>
    /// Lista remetentes bloqueados da farmácia (local + global).
    /// </summary>
    /// <param name="farmaciaId">ID da farmácia</param>
    /// <param name="apenasAtivos">Se true, retorna apenas bloqueios ativos</param>
    Task<IList<RemetenteBloqueado>> ListarBloqueadosAsync(int farmaciaId, bool apenasAtivos = true);

    /// <summary>
    /// Lista remetentes bloqueados globalmente.
    /// </summary>
    /// <param name="apenasAtivos">Se true, retorna apenas bloqueios ativos</param>
    Task<IList<RemetenteBloqueado>> ListarBloqueadosGlobaisAsync(bool apenasAtivos = true);
}

/// <summary>
/// Implementação do serviço de bloqueio de remetentes.
/// </summary>
public class RemetenteBloqueadoService : IRemetenteBloqueadoService
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<RemetenteBloqueadoService> _logger;

    public RemetenteBloqueadoService(PharmazapDbContext context, ILogger<RemetenteBloqueadoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Normaliza um número de telefone para armazenamento/comparação.
    /// Remove máscara, pontuação, e prefixo "55".
    /// </summary>
    private static string NormalizarNumero(string numeroDDD)
    {
        var sanitizado = numeroDDD.SanitizeDigits(11);
        
        // Remove prefixo "55" se presente no início
        if (sanitizado.StartsWith("55") && sanitizado.Length > 11)
            sanitizado = sanitizado[2..];
        
        // Garante 11 dígitos (DDD + 9 + 8 dígitos)
        return sanitizado.Length >= 11 ? sanitizado[^11..] : sanitizado;
    }

    /// <inheritdoc />
    public async Task<bool> IsRemetentoBloqueadoAsync(string numeroDDD, int farmaciaId)
    {
        var numeroNormalizado = NormalizarNumero(numeroDDD);
        
        try
        {
            // Verifica bloqueio local (específico da farmácia) OU global
            var estaBloqueado = await _context.RemetentesBlockeados
                .Where(r => r.NumeroNormalizado == numeroNormalizado && r.Ativo)
                .Where(r => r.FarmaciaId == farmaciaId || r.FarmaciaId == null)
                .AnyAsync();

            if (estaBloqueado)
                _logger.LogInformation("Remetente bloqueado detectado: {numero} para farmácia {farmaciaId}", numeroNormalizado, farmaciaId);

            return estaBloqueado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar remetente bloqueado: {numero}", numeroNormalizado);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<RemetenteBloqueado> BloqueRemetenteAsync(
        string numeroDDD,
        int farmaciaId,
        RemetentoBloqueioEscopoEnum escopo,
        int usuarioBloqueadorId,
        string nomeUsuarioBloqueador,
        string? motivo = null)
    {
        var numeroNormalizado = NormalizarNumero(numeroDDD);

        try
        {
            // Verifica se já existe um bloqueio (local ou global)
            var jaBloqueado = await _context.RemetentesBlockeados
                .FirstOrDefaultAsync(r =>
                    r.NumeroNormalizado == numeroNormalizado &&
                    r.FarmaciaId == (escopo == RemetentoBloqueioEscopoEnum.Local ? farmaciaId : null));

            RemetenteBloqueado remetenteBloqueado;

            if (jaBloqueado != null)
            {
                // Reativa um bloqueio existente
                jaBloqueado.Ativo = true;
                jaBloqueado.DataBloqueio = DateTime.UtcNow;
                jaBloqueado.BloqueadoPorId = usuarioBloqueadorId;
                jaBloqueado.NomeBloqueadoPor = nomeUsuarioBloqueador;
                jaBloqueado.DataDesbloqueio = null;
                jaBloqueado.DesbloqueadoPorId = null;
                jaBloqueado.NomeDesbloqueadoPor = null;
                jaBloqueado.MotivoBloqueia = motivo;

                remetenteBloqueado = jaBloqueado;
            }
            else
            {
                // Cria novo bloqueio
                remetenteBloqueado = new RemetenteBloqueado
                {
                    NumeroNormalizado = numeroNormalizado,
                    FarmaciaId = escopo == RemetentoBloqueioEscopoEnum.Local ? farmaciaId : null,
                    Escopo = escopo,
                    MotivoBloqueia = motivo,
                    DataBloqueio = DateTime.UtcNow,
                    BloqueadoPorId = usuarioBloqueadorId,
                    NomeBloqueadoPor = nomeUsuarioBloqueador,
                    Ativo = true
                };

                _context.RemetentesBlockeados.Add(remetenteBloqueado);
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Remetente bloqueado: {numero} (Escopo: {escopo}, Farmácia: {farmaciaId}, Bloqueador: {bloqueador})",
                numeroNormalizado,
                escopo,
                escopo == RemetentoBloqueioEscopoEnum.Local ? farmaciaId : "Global",
                nomeUsuarioBloqueador);

            return remetenteBloqueado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao bloquear remetente: {numero}", numeroNormalizado);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DesbloqueRemetenteAsync(
        string numeroDDD,
        int farmaciaId,
        RemetentoBloqueioEscopoEnum escopo,
        int usuarioDesbloqueadorId,
        string nomeUsuarioDesbloqueador)
    {
        var numeroNormalizado = NormalizarNumero(numeroDDD);

        try
        {
            var remetenteBloqueado = await _context.RemetentesBlockeados
                .FirstOrDefaultAsync(r =>
                    r.NumeroNormalizado == numeroNormalizado &&
                    r.FarmaciaId == (escopo == RemetentoBloqueioEscopoEnum.Local ? farmaciaId : null) &&
                    r.Ativo);

            if (remetenteBloqueado == null)
            {
                _logger.LogWarning("Tentativa de desbloquear remetente não encontrado: {numero}", numeroNormalizado);
                return;
            }

            remetenteBloqueado.Ativo = false;
            remetenteBloqueado.DataDesbloqueio = DateTime.UtcNow;
            remetenteBloqueado.DesbloqueadoPorId = usuarioDesbloqueadorId;
            remetenteBloqueado.NomeDesbloqueadoPor = nomeUsuarioDesbloqueador;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Remetente desbloqueado: {numero} (Escopo: {escopo}, Desbloqueador: {desbloqueador})",
                numeroNormalizado,
                escopo,
                nomeUsuarioDesbloqueador);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desbloquear remetente: {numero}", numeroNormalizado);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<RemetenteBloqueado>> ListarBloqueadosAsync(int farmaciaId, bool apenasAtivos = true)
    {
        var query = _context.RemetentesBlockeados
            .Where(r => r.FarmaciaId == farmaciaId || r.FarmaciaId == null);

        if (apenasAtivos)
            query = query.Where(r => r.Ativo);

        return await query
            .OrderByDescending(r => r.DataBloqueio)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IList<RemetenteBloqueado>> ListarBloqueadosGlobaisAsync(bool apenasAtivos = true)
    {
        var query = _context.RemetentesBlockeados
            .Where(r => r.FarmaciaId == null);

        if (apenasAtivos)
            query = query.Where(r => r.Ativo);

        return await query
            .OrderByDescending(r => r.DataBloqueio)
            .ToListAsync();
    }
}
