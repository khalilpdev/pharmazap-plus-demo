using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Web.Services
{
    public record UpdateClienteDto
    {
        public string? Nome { get; set; }
        public string? Sobrenome { get; set; }
        public string? Apelido { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? Cpf { get; set; }
        public DateTime? DataNascimento { get; set; }
        public string? Sexo { get; set; }
        public string? Endereco { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Cep { get; set; }
        public bool? UsoContinuo { get; set; }
        public string? MedicamentosContinuos { get; set; }
        public string? Alergias { get; set; }
        public string? ObservacoesSaude { get; set; }
        public bool? AceitaMarketing { get; set; }
        public bool? AceitaNotificacoes { get; set; }
        public string? PreferenciasContato { get; set; }
        public string? ObservacoesGerais { get; set; }
        public string? Status { get; set; }
        public string? Tipo { get; set; }
        public string? Segmento { get; set; }
        public string? CrmCrf { get; set; }
        public string? Especialidade { get; set; }
        public string? LocalTrabalho { get; set; }
        public int? EtiquetaId { get; set; }
        public bool ClearEtiqueta { get; set; }
        public ClientePerfilCompraEnum? PerfilCompra { get; set; }
        public string? AtualizadoPor { get; set; }
    }
}
