namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Representa uma atividade recente do usuário retornada pela API.
    /// </summary>
    public class RecentActivityItem
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNome { get; set; } = string.Empty;
        public string TipoAtividade { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int? AtendimentoId { get; set; }
        public string? ClienteNome { get; set; }
        public DateTime DataHora { get; set; }
    }
}
