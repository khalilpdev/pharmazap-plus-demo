using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.Campanhas;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Models.Vendas;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmazapPlus.Shared.Models.Core;

public class Farmacia : BaseEntity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Nome { get; set; }

    [Required]
    [MaxLength(20)]
    public required string Cnpj { get; set; }

    [MaxLength(20)]
    public required string Telefone { get; set; }

    [MaxLength(200)]
    public required string Email { get; set; }

    [MaxLength(500)]
    public required string Endereco { get; set; }

    [MaxLength(100)]
    public required string Cidade { get; set; }

    [MaxLength(2)]
    public required string Estado { get; set; }

    [MaxLength(10)]
    public required string Cep { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    // Status
    public FarmaciaStatus Status { get; set; } = FarmaciaStatus.Ativa;

    // Plano
    [MaxLength(50)]
    public string Plano { get; set; } = "Basic";

    // Datas
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtivacao { get; set; }
    public DateTime? DataSuspensao { get; set; }
    public DateTime? DataCancelamento { get; set; }

    // LGPD
    public bool TermoAceite { get; set; } = false;
    public DateTime? TermoAceiteDataHora { get; set; }

    // Configurações
    public bool Ativa { get; set; } = true;
    [MaxLength(500)]
    public string? MotivoBloqueio { get; set; }
    public int LimiteAtendentes { get; set; } = 5;
    public int LimiteClientes { get; set; } = 1000;

    // Integração (1:1)
    public virtual FarmaciaIntegracoes? Integracoes { get; set; }

    // Relacionamentos
    public virtual ICollection<UsuarioFarmacia> Usuarios { get; set; } = new List<UsuarioFarmacia>();
    public virtual ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
    public virtual ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public virtual ICollection<AgendamentoRecorrente> Agendamentos { get; set; } = new List<AgendamentoRecorrente>();
    public virtual ICollection<Departamento> Departamentos { get; set; } = new List<Departamento>();
    public virtual ICollection<Etiqueta> Etiquetas { get; set; } = new List<Etiqueta>();
    public virtual ICollection<MotivoEncerramento> MotivosEncerramento { get; set; } = new List<MotivoEncerramento>();
    public virtual ICollection<Campanha> Campanhas { get; set; } = new List<Campanha>();
    public virtual ICollection<MensagemPreDefinida> MensagensPreDefinidas { get; set; } = new List<MensagemPreDefinida>();

    // Propriedades calculadas
    [NotMapped]
    public int TotalUsuariosAtivos => Usuarios?.Count(u => u.Ativo) ?? 0;

    [NotMapped]
    public int TotalClientesAtivos => Clientes?.Count(c => c.Status == ClienteStatus.Ativo) ?? 0;

    [NotMapped]
    public int TotalAtendimentosHoje => Atendimentos?.Count(a => a.DataInicio.Date == DateTime.UtcNow.Date) ?? 0;

    [NotMapped]
    public bool WhatsAppConfigurado => !string.IsNullOrEmpty(Integracoes?.WhatsAppPhoneNumberId) &&
                                      !string.IsNullOrEmpty(Integracoes?.WhatsAppAccessToken);

    [NotMapped]
    public bool InstagramConfigurado => !string.IsNullOrEmpty(Integracoes?.InstagramAccountId) &&
                                       !string.IsNullOrEmpty(Integracoes?.InstagramAccessToken);

    public bool IsActive { get; set; }
}
