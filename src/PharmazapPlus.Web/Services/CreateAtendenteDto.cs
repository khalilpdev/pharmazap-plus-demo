namespace PharmazapPlus.Web.Services
{
    public record CreateAtendenteDto
    {
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Telefone { get; set; }
        public string? Turno { get; set; }
        public bool RecebeNotificacoes { get; set; } = true;
        public int? DepartamentoId { get; set; }
    }
}
