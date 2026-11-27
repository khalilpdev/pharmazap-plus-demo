namespace PharmazapPlus.Web.Services
{
    public record UpdateAtendenteDto
    {
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? Turno { get; set; }
        public bool? RecebeNotificacoes { get; set; }
        public int? DepartamentoId { get; set; }
    }
}
