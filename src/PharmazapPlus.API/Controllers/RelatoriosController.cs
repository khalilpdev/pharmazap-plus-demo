using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using System.Drawing;
using System.Security.Claims;

namespace PharmazapPlus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RelatoriosController : ControllerBase
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<RelatoriosController> _logger;

    private static readonly Color HeaderBackgroundColor = Color.FromArgb(98, 100, 167);

    public RelatoriosController(PharmazapDbContext context, ILogger<RelatoriosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Relatório consolidado para o Gestor — visão de todas as farmácias.
    /// </summary>
    [HttpGet("gestor")]
    [Authorize(Roles = UserRoles.Gestor)]
    public async Task<IActionResult> GetRelatorioGestor(
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        [FromQuery] int? farmaciaId = null)
    {
        try
        {
            var inicio = DateTime.SpecifyKind((dataInicio ?? DateTime.UtcNow.AddDays(-30)).Date, DateTimeKind.Utc);
            var fim = DateTime.SpecifyKind((dataFim ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var farmaciaQuery = _context.Farmacias
                .Where(f => (FarmaciaStatus)f.Status == FarmaciaStatus.Ativa);

            if (farmaciaId.HasValue)
                farmaciaQuery = farmaciaQuery.Where(f => f.Id == farmaciaId.Value);

            var farmacias = await farmaciaQuery
                .Select(f => new { f.Id, f.Nome })
                .ToListAsync();

            var farmaciaIds = farmacias.Select(f => f.Id).ToList();

            var atendimentosNoPeriodo = await _context.Atendimentos
                .Where(a => farmaciaIds.Contains(a.FarmaciaId) &&
                            a.DataInicio >= inicio && a.DataInicio <= fim)
                .Select(a => new { a.FarmaciaId, a.StatusEnum, a.DataInicio })
                .ToListAsync();

            var pedidosNoPeriodo = await _context.Pedidos
                .Where(p => farmaciaIds.Contains(p.FarmaciaId) &&
                            p.DataPedido >= inicio && p.DataPedido <= fim)
                .Select(p => new { p.FarmaciaId, ValorFinal = p.ValorTotal - p.Desconto, p.Status })
                .ToListAsync();

            var clientesPorFarmacia = await _context.Clientes
                .Where(c => farmaciaIds.Contains(c.FarmaciaId) && c.Ativo)
                .GroupBy(c => c.FarmaciaId)
                .Select(g => new { FarmaciaId = g.Key, Total = g.Count() })
                .ToListAsync();

            var farmaciasData = farmacias.Select(f =>
            {
                var atendimentos = atendimentosNoPeriodo.Where(a => a.FarmaciaId == f.Id).ToList();
                var pedidos = pedidosNoPeriodo.Where(p => p.FarmaciaId == f.Id).ToList();
                var totalClientes = clientesPorFarmacia.FirstOrDefault(c => c.FarmaciaId == f.Id)?.Total ?? 0;
                var receita = pedidos.Where(p => p.Status != PedidoStatusEnum.Cancelado).Sum(p => p.ValorFinal);
                var totalPedidos = pedidos.Count(p => p.Status != PedidoStatusEnum.Cancelado);

                return new
                {
                    f.Id,
                    f.Nome,
                    TotalAtendimentos = atendimentos.Count,
                    AtendimentosFinalizados = atendimentos.Count(a => a.StatusEnum == AtendimentoStatusEnum.Finalizado),
                    Receita = receita,
                    TotalPedidos = totalPedidos,
                    TicketMedio = totalPedidos > 0 ? receita / totalPedidos : 0m,
                    TotalClientes = totalClientes
                };
            }).ToList();

            var tendencia = atendimentosNoPeriodo
                .GroupBy(a => a.DataInicio.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Data = g.Key.ToString("yyyy-MM-dd"), Quantidade = g.Count() })
                .ToList();

            var totalReceita = farmaciasData.Sum(f => f.Receita);
            var totalPedidosGeral = farmaciasData.Sum(f => f.TotalPedidos);

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    Periodo = new { Inicio = inicio, Fim = dataFim ?? DateTime.UtcNow },
                    Resumo = new
                    {
                        TotalFarmacias = farmacias.Count,
                        TotalAtendimentos = atendimentosNoPeriodo.Count,
                        TotalReceita = totalReceita,
                        TotalPedidos = totalPedidosGeral,
                        TicketMedio = totalPedidosGeral > 0 ? totalReceita / totalPedidosGeral : 0m
                    },
                    Farmacias = farmaciasData,
                    TendenciaAtendimentos = tendencia
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gestor report");
            return StatusCode(500, new { Success = false, Message = "Erro ao gerar relatório" });
        }
    }

    /// <summary>
    /// Relatório detalhado para o Administrador — visão da farmácia e desempenho dos atendentes.
    /// </summary>
    [HttpGet("administrador")]
    [Authorize(Roles = UserRoles.GestorOuAdministrador)]
    public async Task<IActionResult> GetRelatorioAdministrador(
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null)
    {
        try
        {
            var inicio = DateTime.SpecifyKind((dataInicio ?? DateTime.UtcNow.AddDays(-30)).Date, DateTimeKind.Utc);
            var fim = DateTime.SpecifyKind((dataFim ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            int farmaciaId;
            if (User.IsInRole(UserRoles.Gestor))
            {
                // Gestor pode acessar via query parameter opcional
                var farmaciaIdParam = Request.Query["farmaciaId"].FirstOrDefault();
                if (!int.TryParse(farmaciaIdParam, out farmaciaId))
                {
                    var primeiraFarmacia = await _context.Farmacias
                        .Where(f => (FarmaciaStatus)f.Status == FarmaciaStatus.Ativa)
                        .OrderBy(f => f.Id)
                        .Select(f => f.Id)
                        .FirstOrDefaultAsync();
                    farmaciaId = primeiraFarmacia;
                }
            }
            else
            {
                var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
                if (!int.TryParse(farmaciaIdStr, out farmaciaId))
                    return BadRequest(new { Success = false, Message = "Farmácia não identificada" });
            }

            var farmacia = await _context.Farmacias
                .Where(f => f.Id == farmaciaId)
                .Select(f => new { f.Id, f.Nome })
                .FirstOrDefaultAsync();

            if (farmacia == null)
                return NotFound(new { Success = false, Message = "Farmácia não encontrada" });

            var atendimentos = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmaciaId &&
                            a.DataInicio >= inicio && a.DataInicio <= fim)
                .Select(a => new
                {
                    a.Id,
                    a.AtendenteId,
                    a.StatusEnum,
                    a.DataInicio,
                    a.DataFim,
                    a.DuracaoSegundos
                })
                .ToListAsync();

            var atendentes = await _context.UsuariosFarmacia
                .Where(u => u.FarmaciaId == farmaciaId && u.Role == UserRoles.Atendente)
                .Select(u => new { u.Id, u.Nome, u.Ativo })
                .ToListAsync();

            var atendenteStats = atendentes.Select(a =>
            {
                var atendsDoAtendente = atendimentos.Where(at => at.AtendenteId == a.Id).ToList();
                var finalizados = atendsDoAtendente.Count(at => at.StatusEnum == AtendimentoStatusEnum.Finalizado);
                var emAndamento = atendsDoAtendente.Count(at => at.StatusEnum == AtendimentoStatusEnum.EmAndamento);
                var total = atendsDoAtendente.Count;
                var tempoMedio = atendsDoAtendente
                    .Where(at => at.DuracaoSegundos.HasValue)
                    .Select(at => at.DuracaoSegundos!.Value)
                    .DefaultIfEmpty(0)
                    .Average();

                return new
                {
                    a.Id,
                    a.Nome,
                    a.Ativo,
                    TotalAtendimentos = total,
                    AtendimentosFinalizados = finalizados,
                    AtendimentosEmAndamento = emAndamento,
                    TaxaConversao = total > 0 ? Math.Round((double)finalizados / total * 100, 1) : 0.0,
                    TempoMedioSegundos = (int)tempoMedio
                };
            }).OrderByDescending(a => a.AtendimentosFinalizados).ToList();

            var totalFinalizados = atendimentos.Count(a => a.StatusEnum == AtendimentoStatusEnum.Finalizado);
            var totalAtendimentos = atendimentos.Count;
            var tempoMedioGeral = atendimentos
                .Where(a => a.DuracaoSegundos.HasValue)
                .Select(a => a.DuracaoSegundos!.Value)
                .DefaultIfEmpty(0)
                .Average();

            var tendencia = atendimentos
                .GroupBy(a => a.DataInicio.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Data = g.Key.ToString("yyyy-MM-dd"), Quantidade = g.Count() })
                .ToList();

            var totalClientes = await _context.Clientes
                .Where(c => c.FarmaciaId == farmaciaId && c.Ativo)
                .CountAsync();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    Periodo = new { Inicio = inicio, Fim = dataFim ?? DateTime.UtcNow },
                    Farmacia = farmacia,
                    Resumo = new
                    {
                        TotalAtendimentos = totalAtendimentos,
                        AtendimentosFinalizados = totalFinalizados,
                        TaxaConversao = totalAtendimentos > 0 ? Math.Round((double)totalFinalizados / totalAtendimentos * 100, 1) : 0.0,
                        TempoMedioSegundos = (int)tempoMedioGeral,
                        TotalClientes = totalClientes,
                        TotalAtendentes = atendentes.Count(a => a.Ativo)
                    },
                    Atendentes = atendenteStats,
                    TendenciaAtendimentos = tendencia
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting administrador report");
            return StatusCode(500, new { Success = false, Message = "Erro ao gerar relatório" });
        }
    }

    /// <summary>
    /// Relatório individual do atendente — histórico e métricas pessoais.
    /// </summary>
    [HttpGet("atendente")]
    [Authorize]
    public async Task<IActionResult> GetRelatorioAtendente(
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null)
    {
        try
        {
            var inicio = DateTime.SpecifyKind((dataInicio ?? DateTime.UtcNow.AddDays(-30)).Date, DateTimeKind.Utc);
            var fim = DateTime.SpecifyKind((dataFim ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
            if (!int.TryParse(farmaciaIdStr, out var farmaciaId))
                return BadRequest(new { Success = false, Message = "Farmácia não identificada" });

            var atendenteIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(atendenteIdStr, out var atendenteId))
                return BadRequest(new { Success = false, Message = "Usuário não identificado" });

            var atendimentosDb = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmaciaId &&
                            a.AtendenteId == atendenteId &&
                            a.DataInicio >= inicio && a.DataInicio <= fim)
                .Include(a => a.Cliente)
                .OrderByDescending(a => a.DataInicio)
                .ToListAsync();

            var atendimentos = atendimentosDb
                .Select(a => new
                {
                    a.Id,
                    ClienteNome = a.Cliente != null ? a.Cliente.Nome : a.WhatsAppId,
                    Status = a.Status,
                    a.StatusEnum,
                    a.DataInicio,
                    a.DataFim,
                    a.DuracaoSegundos,
                    a.Canal
                })
                .ToList();

            var finalizados = atendimentos.Count(a => a.StatusEnum == AtendimentoStatusEnum.Finalizado);
            var emAndamento = atendimentos.Count(a => a.StatusEnum == AtendimentoStatusEnum.EmAndamento);
            var pendentes = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmaciaId && a.StatusEnum == AtendimentoStatusEnum.Pendente)
                .CountAsync();

            var tempoMedio = atendimentos
                .Where(a => a.DuracaoSegundos.HasValue)
                .Select(a => a.DuracaoSegundos!.Value)
                .DefaultIfEmpty(0)
                .Average();

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    Periodo = new { Inicio = inicio, Fim = dataFim ?? DateTime.UtcNow },
                    Resumo = new
                    {
                        TotalAtendimentos = atendimentos.Count,
                        AtendimentosFinalizados = finalizados,
                        AtendimentosEmAndamento = emAndamento,
                        AtendimentosPendentes = pendentes,
                        TempoMedioSegundos = (int)tempoMedio
                    },
                    Historico = atendimentos
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting atendente report");
            return StatusCode(500, new { Success = false, Message = "Erro ao gerar relatório" });
        }
    }

    /// <summary>
    /// Relatório gerencial de faturamento — disponível apenas para Gestor e Administrador.
    /// </summary>
    [HttpGet("faturamento")]
    [Authorize(Roles = UserRoles.GestorOuAdministrador)]
    public async Task<IActionResult> GetRelatorioFaturamento(
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        [FromQuery] int? atendenteId = null,
        [FromQuery] int? farmaciaId = null)
    {
        try
        {
            var inicio = DateTime.SpecifyKind((dataInicio ?? DateTime.UtcNow.AddDays(-30)).Date, DateTimeKind.Utc);
            var fim = DateTime.SpecifyKind((dataFim ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            int resolvedFarmaciaId;
            if (User.IsInRole(UserRoles.Gestor))
            {
                if (!farmaciaId.HasValue || farmaciaId == 0)
                {
                    var primeiraFarmacia = await _context.Farmacias
                        .Where(f => (FarmaciaStatus)f.Status == FarmaciaStatus.Ativa)
                        .OrderBy(f => f.Id)
                        .Select(f => f.Id)
                        .FirstOrDefaultAsync();
                    resolvedFarmaciaId = primeiraFarmacia;
                }
                else
                {
                    resolvedFarmaciaId = farmaciaId.Value;
                }
            }
            else
            {
                var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
                if (!int.TryParse(farmaciaIdStr, out resolvedFarmaciaId))
                    return BadRequest(new { Success = false, Message = "Farmácia não identificada" });
            }

            var farmacia = await _context.Farmacias
                .Where(f => f.Id == resolvedFarmaciaId)
                .Select(f => new { f.Id, f.Nome })
                .FirstOrDefaultAsync();

            if (farmacia == null)
                return NotFound(new { Success = false, Message = "Farmácia não encontrada" });

            var atendimentosQuery = _context.Atendimentos
                .Where(a => a.FarmaciaId == resolvedFarmaciaId &&
                            a.StatusEnum == AtendimentoStatusEnum.Finalizado &&
                            a.DataInicio >= inicio && a.DataInicio <= fim);

            if (atendenteId.HasValue)
                atendimentosQuery = atendimentosQuery.Where(a => a.AtendenteId == atendenteId.Value);

            var atendimentos = await atendimentosQuery
                .Include(a => a.Cliente)
                .Include(a => a.Atendente)
                .OrderByDescending(a => a.DataFim)
                .ToListAsync();

            var vendas = atendimentos
                .Where(a => a.ValorVenda.HasValue && a.ValorVenda.Value > 0)
                .Select(a => new
                {
                    AtendimentoId = a.Id,
                    ClienteNome = a.Cliente?.Nome ?? a.WhatsAppId,
                    AtendenteNome = a.Atendente?.Nome ?? "—",
                    DataFim = a.DataFim ?? a.DataInicio,
                    ValorVenda = a.ValorVenda!.Value,
                    Canal = a.Canal
                })
                .ToList();

            var atendentes = await _context.UsuariosFarmacia
                .Where(u => u.FarmaciaId == resolvedFarmaciaId)
                .Select(u => new { u.Id, u.Nome })
                .ToListAsync();

            var porAtendente = atendentes
                .Select(u =>
                {
                    var atendsDoAtendente = atendimentos.Where(a => a.AtendenteId == u.Id).ToList();
                    var comVenda = atendsDoAtendente.Where(a => a.ValorVenda.HasValue && a.ValorVenda.Value > 0).ToList();
                    var fat = comVenda.Sum(a => a.ValorVenda!.Value);
                    var totalFinalizados = atendsDoAtendente.Count;
                    return new
                    {
                        u.Id,
                        u.Nome,
                        TotalAtendimentos = totalFinalizados,
                        AtendimentosComVenda = comVenda.Count,
                        Faturamento = fat,
                        TicketMedio = comVenda.Count > 0 ? fat / comVenda.Count : 0m,
                        TaxaConversaoVenda = totalFinalizados > 0
                            ? Math.Round((double)comVenda.Count / totalFinalizados * 100, 1)
                            : 0.0
                    };
                })
                .Where(a => a.TotalAtendimentos > 0)
                .OrderByDescending(a => a.Faturamento)
                .ToList();

            var totalFaturado = vendas.Sum(v => v.ValorVenda);
            var totalComVenda = vendas.Count;
            var totalFinalizados = atendimentos.Count;

            return Ok(new
            {
                Success = true,
                Data = new
                {
                    Periodo = new { Inicio = inicio, Fim = fim },
                    Farmacia = new { farmacia.Id, farmacia.Nome },
                    Resumo = new
                    {
                        TotalFaturado = totalFaturado,
                        TotalAtendimentosComVenda = totalComVenda,
                        TotalAtendimentosFinalizados = totalFinalizados,
                        TicketMedio = totalComVenda > 0 ? totalFaturado / totalComVenda : 0m,
                        TaxaConversaoVenda = totalFinalizados > 0
                            ? Math.Round((double)totalComVenda / totalFinalizados * 100, 1)
                            : 0.0
                    },
                    PorAtendente = porAtendente,
                    Vendas = vendas
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting faturamento report");
            return StatusCode(500, new { Success = false, Message = "Erro ao gerar relatório de faturamento" });
        }
    }

    /// <summary>
    /// Exporta relatório em formato Excel (.xlsx).
    /// tipo: "gestor" | "administrador" | "atendente"
    /// </summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> ExportarRelatorio(
        [FromQuery] string tipo = "atendente",
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null)
    {
        try
        {
            var inicio = DateTime.SpecifyKind((dataInicio ?? DateTime.UtcNow.AddDays(-30)).Date, DateTimeKind.Utc);
            var fim = DateTime.SpecifyKind((dataFim ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            ExcelPackage.License.SetNonCommercialPersonal("PharmazapPlus");
            using var package = new ExcelPackage();

            if (tipo == "gestor" && User.IsInRole(UserRoles.Gestor))
            {
                await AddGestorSheetAsync(package, inicio, fim);
            }
            else if (tipo == "administrador" && User.IsInRole(UserRoles.GestorOuAdministrador))
            {
                var farmaciaIdStr = User.IsInRole(UserRoles.Gestor)
                    ? Request.Query["farmaciaId"].FirstOrDefault()
                    : User.FindFirst("FarmaciaId")?.Value;
                int.TryParse(farmaciaIdStr, out var farmaciaId);
                await AddAdministradorSheetAsync(package, farmaciaId, inicio, fim);
            }
            else if (tipo == "faturamento" && User.IsInRole(UserRoles.GestorOuAdministrador))
            {
                var farmaciaIdStr = User.IsInRole(UserRoles.Gestor)
                    ? Request.Query["farmaciaId"].FirstOrDefault()
                    : User.FindFirst("FarmaciaId")?.Value;
                int.TryParse(farmaciaIdStr, out var farmaciaId);
                int? atendenteIdParam = int.TryParse(Request.Query["atendenteId"].FirstOrDefault(), out var aid) ? aid : null;
                await AddFaturamentoSheetAsync(package, farmaciaId, atendenteIdParam, inicio, fim);
            }
            else
            {
                var farmaciaIdStr = User.FindFirst("FarmaciaId")?.Value;
                var atendenteIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(farmaciaIdStr, out var farmaciaId);
                int.TryParse(atendenteIdStr, out var atendenteId);
                await AddAtendenteSheetAsync(package, farmaciaId, atendenteId, inicio, fim);
            }

            var bytes = package.GetAsByteArray();
            var fileName = $"relatorio_{tipo}_{inicio:yyyyMMdd}_{fim:yyyyMMdd}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report");
            return StatusCode(500, new { Success = false, Message = "Erro ao exportar relatório" });
        }
    }

    private async Task AddGestorSheetAsync(ExcelPackage package, DateTime inicio, DateTime fim)
    {
        var sheet = package.Workbook.Worksheets.Add("Relatório Gestor");

        // Header
        sheet.Cells[1, 1].Value = "Relatório Gerencial — Todas as Farmácias";
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[2, 1].Value = $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";

        var headers = new[] { "Farmácia", "Atendimentos", "Finalizados", "Receita (R$)", "Total Pedidos", "Ticket Médio (R$)", "Clientes Ativos" };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cells[4, i + 1].Value = headers[i];
            sheet.Cells[4, i + 1].Style.Font.Bold = true;
            sheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(HeaderBackgroundColor);
            sheet.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
        }

        var farmacias = await _context.Farmacias
            .Where(f => (FarmaciaStatus)f.Status == FarmaciaStatus.Ativa)
            .Select(f => new { f.Id, f.Nome })
            .ToListAsync();

        var row = 5;
        foreach (var farmacia in farmacias)
        {
            var atendimentos = await _context.Atendimentos
                .Where(a => a.FarmaciaId == farmacia.Id && a.DataInicio >= inicio && a.DataInicio <= fim)
                .Select(a => new { a.StatusEnum })
                .ToListAsync();

            var pedidos = await _context.Pedidos
                .Where(p => p.FarmaciaId == farmacia.Id && p.DataPedido >= inicio && p.DataPedido <= fim && p.Status != PedidoStatusEnum.Cancelado)
                .Select(p => new { ValorFinal = p.ValorTotal - p.Desconto })
                .ToListAsync();

            var totalClientes = await _context.Clientes
                .Where(c => c.FarmaciaId == farmacia.Id && c.Ativo)
                .CountAsync();

            var receita = pedidos.Sum(p => p.ValorFinal);
            var totalPedidos = pedidos.Count;

            sheet.Cells[row, 1].Value = farmacia.Nome;
            sheet.Cells[row, 2].Value = atendimentos.Count;
            sheet.Cells[row, 3].Value = atendimentos.Count(a => a.StatusEnum == AtendimentoStatusEnum.Finalizado);
            sheet.Cells[row, 4].Value = receita;
            sheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
            sheet.Cells[row, 5].Value = totalPedidos;
            sheet.Cells[row, 6].Value = totalPedidos > 0 ? receita / totalPedidos : 0m;
            sheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            sheet.Cells[row, 7].Value = totalClientes;
            row++;
        }

        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
    }

    private async Task AddAdministradorSheetAsync(ExcelPackage package, int farmaciaId, DateTime inicio, DateTime fim)
    {
        var sheet = package.Workbook.Worksheets.Add("Desempenho Atendentes");

        var farmacia = await _context.Farmacias.FindAsync(farmaciaId);

        sheet.Cells[1, 1].Value = $"Relatório de Atendentes — {farmacia?.Nome ?? "Farmácia"}";
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[2, 1].Value = $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";

        var headers = new[] { "Atendente", "Status", "Total Atendimentos", "Finalizados", "Em Andamento", "Taxa Conversão (%)", "Tempo Médio (min)" };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cells[4, i + 1].Value = headers[i];
            sheet.Cells[4, i + 1].Style.Font.Bold = true;
            sheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(HeaderBackgroundColor);
            sheet.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
        }

        var atendentes = await _context.UsuariosFarmacia
            .Where(u => u.FarmaciaId == farmaciaId && u.Role == UserRoles.Atendente)
            .Select(u => new { u.Id, u.Nome, u.Ativo })
            .ToListAsync();

        var atendimentos = await _context.Atendimentos
            .Where(a => a.FarmaciaId == farmaciaId && a.DataInicio >= inicio && a.DataInicio <= fim)
            .Select(a => new { a.AtendenteId, a.StatusEnum, a.DuracaoSegundos })
            .ToListAsync();

        var row = 5;
        foreach (var atendente in atendentes)
        {
            var atendsDoAtendente = atendimentos.Where(a => a.AtendenteId == atendente.Id).ToList();
            var finalizados = atendsDoAtendente.Count(a => a.StatusEnum == AtendimentoStatusEnum.Finalizado);
            var emAndamento = atendsDoAtendente.Count(a => a.StatusEnum == AtendimentoStatusEnum.EmAndamento);
            var total = atendsDoAtendente.Count;
            var tempoMedioMin = atendsDoAtendente
                .Where(a => a.DuracaoSegundos.HasValue)
                .Select(a => a.DuracaoSegundos!.Value / 60.0)
                .DefaultIfEmpty(0)
                .Average();

            sheet.Cells[row, 1].Value = atendente.Nome;
            sheet.Cells[row, 2].Value = atendente.Ativo ? "Ativo" : "Inativo";
            sheet.Cells[row, 3].Value = total;
            sheet.Cells[row, 4].Value = finalizados;
            sheet.Cells[row, 5].Value = emAndamento;
            sheet.Cells[row, 6].Value = total > 0 ? Math.Round((double)finalizados / total * 100, 1) : 0.0;
            sheet.Cells[row, 6].Style.Numberformat.Format = "0.0";
            sheet.Cells[row, 7].Value = Math.Round(tempoMedioMin, 1);
            sheet.Cells[row, 7].Style.Numberformat.Format = "0.0";
            row++;
        }

        sheet.Cells[sheet.Dimension?.Address ?? "A1:G1"].AutoFitColumns();
    }

    private async Task AddAtendenteSheetAsync(ExcelPackage package, int farmaciaId, int atendenteId, DateTime inicio, DateTime fim)
    {
        var sheet = package.Workbook.Worksheets.Add("Histórico Pessoal");

        var atendente = await _context.UsuariosFarmacia.FindAsync(atendenteId);

        sheet.Cells[1, 1].Value = $"Histórico de Atendimentos — {atendente?.Nome ?? "Atendente"}";
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[2, 1].Value = $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";

        var headers = new[] { "ID", "Cliente", "Canal", "Data Início", "Data Fim", "Status", "Duração (min)" };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cells[4, i + 1].Value = headers[i];
            sheet.Cells[4, i + 1].Style.Font.Bold = true;
            sheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(HeaderBackgroundColor);
            sheet.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
        }

        var atendimentos = await _context.Atendimentos
            .Where(a => a.FarmaciaId == farmaciaId &&
                        a.AtendenteId == atendenteId &&
                        a.DataInicio >= inicio && a.DataInicio <= fim)
            .Include(a => a.Cliente)
            .OrderByDescending(a => a.DataInicio)
            .ToListAsync();

        var row = 5;
        foreach (var atendimento in atendimentos)
        {
            sheet.Cells[row, 1].Value = atendimento.Id;
            sheet.Cells[row, 2].Value = atendimento.Cliente?.Nome ?? atendimento.WhatsAppId;
            sheet.Cells[row, 3].Value = atendimento.Canal;
            sheet.Cells[row, 4].Value = atendimento.DataInicio.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            sheet.Cells[row, 5].Value = atendimento.DataFim.HasValue
                ? atendimento.DataFim.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "-";
            sheet.Cells[row, 6].Value = atendimento.Status;
            sheet.Cells[row, 7].Value = atendimento.DuracaoSegundos.HasValue
                ? Math.Round(atendimento.DuracaoSegundos.Value / 60.0, 1)
                : 0.0;
            sheet.Cells[row, 7].Style.Numberformat.Format = "0.0";
            row++;
        }

        sheet.Cells[sheet.Dimension?.Address ?? "A1:G1"].AutoFitColumns();
    }

    private async Task AddFaturamentoSheetAsync(ExcelPackage package, int farmaciaId, int? atendenteId, DateTime inicio, DateTime fim)
    {
        var sheet = package.Workbook.Worksheets.Add("Faturamento");

        var farmacia = await _context.Farmacias.FindAsync(farmaciaId);

        sheet.Cells[1, 1].Value = $"Relatório de Faturamento — {farmacia?.Nome ?? "Farmácia"}";
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[2, 1].Value = $"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";

        // Vendas por atendimento
        var headersVendas = new[] { "ID Atendimento", "Cliente", "Atendente", "Canal", "Data Finalização", "Valor Venda (R$)" };
        for (int i = 0; i < headersVendas.Length; i++)
        {
            sheet.Cells[4, i + 1].Value = headersVendas[i];
            sheet.Cells[4, i + 1].Style.Font.Bold = true;
            sheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            sheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(HeaderBackgroundColor);
            sheet.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
        }

        var atendimentosQuery = _context.Atendimentos
            .Where(a => a.FarmaciaId == farmaciaId &&
                        a.StatusEnum == AtendimentoStatusEnum.Finalizado &&
                        a.ValorVenda.HasValue && a.ValorVenda.Value > 0 &&
                        a.DataInicio >= inicio && a.DataInicio <= fim);

        if (atendenteId.HasValue)
            atendimentosQuery = atendimentosQuery.Where(a => a.AtendenteId == atendenteId.Value);

        var atendimentos = await atendimentosQuery
            .Include(a => a.Cliente)
            .Include(a => a.Atendente)
            .OrderByDescending(a => a.DataFim)
            .ToListAsync();

        var row = 5;
        foreach (var a in atendimentos)
        {
            sheet.Cells[row, 1].Value = a.Id;
            sheet.Cells[row, 2].Value = a.Cliente?.Nome ?? a.WhatsAppId;
            sheet.Cells[row, 3].Value = a.Atendente?.Nome ?? "—";
            sheet.Cells[row, 4].Value = a.Canal;
            sheet.Cells[row, 5].Value = (a.DataFim ?? a.DataInicio).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            sheet.Cells[row, 6].Value = a.ValorVenda!.Value;
            sheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            row++;
        }

        // Total
        if (atendimentos.Count > 0)
        {
            sheet.Cells[row, 5].Value = "TOTAL";
            sheet.Cells[row, 5].Style.Font.Bold = true;
            sheet.Cells[row, 6].Value = atendimentos.Sum(a => a.ValorVenda!.Value);
            sheet.Cells[row, 6].Style.Font.Bold = true;
            sheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
        }

        sheet.Cells[sheet.Dimension?.Address ?? "A1:F1"].AutoFitColumns();
    }
}
