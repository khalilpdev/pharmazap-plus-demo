using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

// ── Shared ────────────────────────────────────────────────────────────────────

public record RelatorioPeriodo(
    [property: JsonPropertyName("inicio")] DateTime Inicio,
    [property: JsonPropertyName("fim")] DateTime Fim);

// ── Gestor ────────────────────────────────────────────────────────────────────

public record RelatorioGestorResumo(
    [property: JsonPropertyName("totalFarmacias")] int TotalFarmacias,
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("totalReceita")] decimal TotalReceita,
    [property: JsonPropertyName("totalPedidos")] int TotalPedidos,
    [property: JsonPropertyName("ticketMedio")] decimal TicketMedio);

public record RelatorioFarmaciaItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("atendimentosFinalizados")] int AtendimentosFinalizados,
    [property: JsonPropertyName("receita")] decimal Receita,
    [property: JsonPropertyName("totalPedidos")] int TotalPedidos,
    [property: JsonPropertyName("ticketMedio")] decimal TicketMedio,
    [property: JsonPropertyName("totalClientes")] int TotalClientes);

public record TendenciaDia(
    [property: JsonPropertyName("data")] string Data,
    [property: JsonPropertyName("quantidade")] int Quantidade);

public record RelatorioGestorData(
    [property: JsonPropertyName("periodo")] RelatorioPeriodo Periodo,
    [property: JsonPropertyName("resumo")] RelatorioGestorResumo Resumo,
    [property: JsonPropertyName("farmacias")] List<RelatorioFarmaciaItem> Farmacias,
    [property: JsonPropertyName("tendenciaAtendimentos")] List<TendenciaDia> TendenciaAtendimentos);

public record RelatorioGestorResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] RelatorioGestorData? Data);

// ── Administrador ─────────────────────────────────────────────────────────────

public record RelatorioAdministradorResumo(
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("atendimentosFinalizados")] int AtendimentosFinalizados,
    [property: JsonPropertyName("taxaConversao")] double TaxaConversao,
    [property: JsonPropertyName("tempoMedioSegundos")] int TempoMedioSegundos,
    [property: JsonPropertyName("totalClientes")] int TotalClientes,
    [property: JsonPropertyName("totalAtendentes")] int TotalAtendentes);

public record RelatorioAtendenteItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("ativo")] bool Ativo,
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("atendimentosFinalizados")] int AtendimentosFinalizados,
    [property: JsonPropertyName("atendimentosEmAndamento")] int AtendimentosEmAndamento,
    [property: JsonPropertyName("taxaConversao")] double TaxaConversao,
    [property: JsonPropertyName("tempoMedioSegundos")] int TempoMedioSegundos);

public record RelatorioFarmaciaInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome);

public record RelatorioAdministradorData(
    [property: JsonPropertyName("periodo")] RelatorioPeriodo Periodo,
    [property: JsonPropertyName("farmacia")] RelatorioFarmaciaInfo Farmacia,
    [property: JsonPropertyName("resumo")] RelatorioAdministradorResumo Resumo,
    [property: JsonPropertyName("atendentes")] List<RelatorioAtendenteItem> Atendentes,
    [property: JsonPropertyName("tendenciaAtendimentos")] List<TendenciaDia> TendenciaAtendimentos);

public record RelatorioAdministradorResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] RelatorioAdministradorData? Data);

// ── Atendente ─────────────────────────────────────────────────────────────────

public record RelatorioAtendenteResumo(
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("atendimentosFinalizados")] int AtendimentosFinalizados,
    [property: JsonPropertyName("atendimentosEmAndamento")] int AtendimentosEmAndamento,
    [property: JsonPropertyName("atendimentosPendentes")] int AtendimentosPendentes,
    [property: JsonPropertyName("tempoMedioSegundos")] int TempoMedioSegundos);

public record RelatorioHistoricoItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("clienteNome")] string ClienteNome,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("dataInicio")] DateTime DataInicio,
    [property: JsonPropertyName("dataFim")] DateTime? DataFim,
    [property: JsonPropertyName("duracaoSegundos")] int? DuracaoSegundos,
    [property: JsonPropertyName("canal")] string Canal);

public record RelatorioAtendenteData(
    [property: JsonPropertyName("periodo")] RelatorioPeriodo Periodo,
    [property: JsonPropertyName("resumo")] RelatorioAtendenteResumo Resumo,
    [property: JsonPropertyName("historico")] List<RelatorioHistoricoItem> Historico);

public record RelatorioAtendenteResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] RelatorioAtendenteData? Data);

// ── Faturamento ───────────────────────────────────────────────────────────────

public record RelatorioFaturamentoResumo(
    [property: JsonPropertyName("totalFaturado")] decimal TotalFaturado,
    [property: JsonPropertyName("totalAtendimentosComVenda")] int TotalAtendimentosComVenda,
    [property: JsonPropertyName("totalAtendimentosFinalizados")] int TotalAtendimentosFinalizados,
    [property: JsonPropertyName("ticketMedio")] decimal TicketMedio,
    [property: JsonPropertyName("taxaConversaoVenda")] double TaxaConversaoVenda);

public record RelatorioFaturamentoAtendenteItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("totalAtendimentos")] int TotalAtendimentos,
    [property: JsonPropertyName("atendimentosComVenda")] int AtendimentosComVenda,
    [property: JsonPropertyName("faturamento")] decimal Faturamento,
    [property: JsonPropertyName("ticketMedio")] decimal TicketMedio,
    [property: JsonPropertyName("taxaConversaoVenda")] double TaxaConversaoVenda);

public record RelatorioFaturamentoVendaItem(
    [property: JsonPropertyName("atendimentoId")] int AtendimentoId,
    [property: JsonPropertyName("clienteNome")] string ClienteNome,
    [property: JsonPropertyName("atendenteNome")] string AtendenteNome,
    [property: JsonPropertyName("dataFim")] DateTime DataFim,
    [property: JsonPropertyName("valorVenda")] decimal ValorVenda,
    [property: JsonPropertyName("canal")] string Canal);

public record RelatorioFaturamentoData(
    [property: JsonPropertyName("periodo")] RelatorioPeriodo Periodo,
    [property: JsonPropertyName("farmacia")] RelatorioFarmaciaInfo? Farmacia,
    [property: JsonPropertyName("resumo")] RelatorioFaturamentoResumo Resumo,
    [property: JsonPropertyName("porAtendente")] List<RelatorioFaturamentoAtendenteItem> PorAtendente,
    [property: JsonPropertyName("vendas")] List<RelatorioFaturamentoVendaItem> Vendas);

public record RelatorioFaturamentoResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] RelatorioFaturamentoData? Data);
