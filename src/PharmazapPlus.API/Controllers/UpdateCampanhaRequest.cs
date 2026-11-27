using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.API.Controllers;

public record UpdateCampanhaRequest(
    string? Nome,
    string? Mensagem,
    CampanhaTipoEnum Tipo,
    bool EnviarParaTodosAtivos,
    bool EnviarParaUsoContinuo,
    bool EnviarParaAceitaMarketing,
    string? Observacoes,
    List<int>? EtiquetaIds);
