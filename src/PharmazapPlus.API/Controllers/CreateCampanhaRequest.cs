using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.API.Controllers;

// ── Request Models ──────────────────────────────────────────────────────────

public record CreateCampanhaRequest(
    string Nome,
    string Mensagem,
    CampanhaTipoEnum Tipo,
    bool EnviarParaTodosAtivos,
    bool EnviarParaUsoContinuo,
    bool EnviarParaAceitaMarketing,
    string? Observacoes,
    List<int>? EtiquetaIds);
