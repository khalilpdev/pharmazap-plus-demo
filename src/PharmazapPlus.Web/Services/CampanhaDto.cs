using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Web.Services
{
    public class CreateCampanhaDto
    {
        public string Nome { get; set; } = "";
        public string Mensagem { get; set; } = "";
        public CampanhaTipoEnum Tipo { get; set; } = CampanhaTipoEnum.Promocional;
        public bool EnviarParaTodosAtivos { get; set; } = false;
        public bool EnviarParaUsoContinuo { get; set; } = false;
        public bool EnviarParaAceitaMarketing { get; set; } = true;
        public string? Observacoes { get; set; }
        public List<int>? EtiquetaIds { get; set; }
    }

    public class UpdateCampanhaDto
    {
        public string? Nome { get; set; }
        public string? Mensagem { get; set; }
        public CampanhaTipoEnum Tipo { get; set; } = CampanhaTipoEnum.Promocional;
        public bool EnviarParaTodosAtivos { get; set; } = false;
        public bool EnviarParaUsoContinuo { get; set; } = false;
        public bool EnviarParaAceitaMarketing { get; set; } = true;
        public string? Observacoes { get; set; }
        public List<int>? EtiquetaIds { get; set; }
    }
}
