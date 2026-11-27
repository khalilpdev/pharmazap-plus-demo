namespace PharmazapPlus.Shared.Enums;

/// <summary>
/// Define o escopo de um bloqueio de remetente:
/// - Local: bloqueio válido apenas para uma farmácia específica
/// - Global: bloqueio válido para todas as farmácias
/// </summary>
public enum RemetentoBloqueioEscopoEnum
{
    Local = 0,
    Global = 1
}
