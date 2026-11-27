// PharmazapPlus.Shared/Models/CRM/Cliente.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Shared.Models.CRM;

public class Cliente : BaseEntity
{
    public int Id { get; set; }
    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }

    // Identificação
    [MaxLength(20)]
    public string? CodigoCliente { get; set; }

    [Required]
    [MaxLength(50)]
    public required string WhatsAppId { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Nome { get; set; }

    [MaxLength(100)]
    public string? Sobrenome { get; set; }

    [NotMapped]
    public string NomeCompleto => $"{Nome} {Sobrenome}".Trim();

    [MaxLength(50)]
    public string? Apelido { get; set; }

    // Contato
    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Telefone { get; set; }

    [MaxLength(20)]
    public string? Telefone2 { get; set; }

    // Documentos
    [MaxLength(14)]
    public string? Cpf { get; set; }

    [MaxLength(20)]
    public string? Rg { get; set; }

    public DateTime? DataNascimento { get; set; }

    [MaxLength(1)]
    public string? Sexo { get; set; } // M, F, O

    // Endereço
    [MaxLength(200)]
    public string? Endereco { get; set; }

    [MaxLength(20)]
    public string? Numero { get; set; }

    [MaxLength(100)]
    public string? Complemento { get; set; }

    [MaxLength(100)]
    public string? Bairro { get; set; }

    [MaxLength(100)]
    public string? Cidade { get; set; }

    [MaxLength(2)]
    public string? Estado { get; set; }

    [MaxLength(10)]
    public string? Cep { get; set; }

    [MaxLength(200)]
    public string? Referencia { get; set; }

    // Informações de saúde
    public bool UsoContinuo { get; set; }

    [Column(TypeName = "jsonb")]
    public string? MedicamentosContinuos { get; set; } // JSON: List<string>

    [Column(TypeName = "jsonb")]
    public string? Alergias { get; set; } // JSON: List<string>

    [Column(TypeName = "jsonb")]
    public string? CondicoesMedicas { get; set; } // JSON: List<string>

    [MaxLength(5)]
    public string? TipoSanguineo { get; set; }

    public decimal? Peso { get; set; }
    public decimal? Altura { get; set; }

    [MaxLength(1000)]
    public string? ObservacoesSaude { get; set; }

    // Informações profissionais
    [MaxLength(50)]
    public string? CrmCrf { get; set; }

    [MaxLength(100)]
    public string? Especialidade { get; set; }

    [MaxLength(200)]
    public string? LocalTrabalho { get; set; }

    // Status e segmentação
    public ClienteStatus Status { get; set; } = ClienteStatus.Ativo;
    public ClienteTipo Tipo { get; set; } = ClienteTipo.PessoaFisica;
    public ClientePerfilCompraEnum PerfilCompra { get; set; } = ClientePerfilCompraEnum.Prospecto;

    [MaxLength(50)]
    public string? Segmento { get; set; } = ClienteSegmentoEnum.Novo.ToString();

    public int? PontuacaoFidelidade { get; set; } = 0;

    // Metadados
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoContato { get; set; }
    public DateTime? DataInativacao { get; set; }

    [MaxLength(50)]
    public string? OrigemCadastro { get; set; } = Consts.ClienteDefaults.OrigemCadastro;

    [MaxLength(100)]
    public string? IndicadoPor { get; set; }

    // Estatísticas
    public int TotalAtendimentos { get; set; } = 0;
    public int TotalPedidos { get; set; } = 0;
    public decimal TotalGasto { get; set; } = 0;
    public DateTime? UltimaCompra { get; set; }

    [NotMapped]
    public decimal TicketMedio => TotalPedidos > 0 ? TotalGasto / TotalPedidos : 0;

    // Preferências
    public bool AceitaMarketing { get; set; } = true;
    public bool AceitaNotificacoes { get; set; } = true;

    [MaxLength(50)]
    public string? PreferenciasContato { get; set; } = Consts.ClienteDefaults.PreferenciasContato;

    [MaxLength(1000)]
    public string? ObservacoesGerais { get; set; }

    // Campos de auditoria
    [MaxLength(100)]
    public string? CriadoPor { get; set; } = Consts.ClienteDefaults.CriadoPor;

    [MaxLength(100)]
    public string? AtualizadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    // Etiqueta
    public int? EtiquetaId { get; set; }
    public virtual Etiqueta? Etiqueta { get; set; }

    // Relacionamentos
    public virtual ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
    public virtual ICollection<AgendamentoRecorrente> Agendamentos { get; set; } = new List<AgendamentoRecorrente>();
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public virtual ICollection<ClienteTag> Tags { get; set; } = new List<ClienteTag>();
    public virtual ICollection<ClienteObservacao> Observacoes { get; set; } = new List<ClienteObservacao>();

    // Propriedades calculadas
    [NotMapped]
    public int Idade => DataNascimento.HasValue ?
        DateTime.Now.Year - DataNascimento.Value.Year : 0;

    [NotMapped]
    public bool TemUsoContinuo => UsoContinuo ||
        (!string.IsNullOrEmpty(MedicamentosContinuos) && MedicamentosContinuos != "[]");

    [NotMapped]
    public string EnderecoCompleto =>
        $"{Endereco}, {Numero} {Complemento} - {Bairro}, {Cidade} - {Estado}, {Cep}";

    // JSON Properties
    [NotMapped]
    public List<string> MedicamentosContinuosList
    {
        get => DeserializeJson<List<string>>(MedicamentosContinuos ?? string.Empty);
        set => MedicamentosContinuos = SerializeJson(value);
    }

    [NotMapped]
    public List<string> AlergiasList
    {
        get => DeserializeJson<List<string>>(Alergias ?? string.Empty);
        set => Alergias = SerializeJson(value);
    }

    [NotMapped]
    public List<string> CondicoesMedicasList
    {
        get => DeserializeJson<List<string>>(CondicoesMedicas ?? string.Empty);
        set => CondicoesMedicas = SerializeJson(value);
    }
    public bool Ativo { get; set; } = true;    

    // Métodos auxiliares
    public void AtualizarUltimoContato()
    {
        UltimoContato = DateTime.UtcNow;
        TotalAtendimentos++;
    }

    public void AdicionarPedido(decimal valor)
    {
        TotalPedidos++;
        TotalGasto += valor;
        UltimaCompra = DateTime.UtcNow;

        // Atualizar pontuação fidelidade (1 ponto a cada R$ 10 gastos)
        PontuacaoFidelidade = (int)(TotalGasto / Consts.ClienteSegmentosConfig.DivisorPontuacaoFidelidade);

        // Atualizar segmento baseado no valor gasto
        if (TotalGasto >= Consts.ClienteSegmentosConfig.LimiteVip) Segmento = ClienteSegmentoEnum.Vip.ToString();
        else if (TotalGasto >= Consts.ClienteSegmentosConfig.LimiteRegular) Segmento = ClienteSegmentoEnum.Regular.ToString();
        else Segmento = ClienteSegmentoEnum.Novo.ToString();

        // Perfil de compra por comportamento
        AtualizarPerfilCompra();
    }

    public void AtualizarPerfilCompra()
    {
        // Uso contínuo tem prioridade — campo já existente na model
        if (UsoContinuo || TemUsoContinuo)
        {
            PerfilCompra = ClientePerfilCompraEnum.UsoContinuo;
            return;
        }

        // Sem data de compra = nunca comprou, trata como infinitamente distante
        var diasDesdeUltimaCompra = UltimaCompra.HasValue
            ? (DateTime.UtcNow - UltimaCompra.Value).TotalDays
            : double.MaxValue;

        // Nota: as condições são avaliadas em ordem — Fidelizado (>= 5 pedidos, <= 30 dias)
        // tem prioridade sobre Recorrente (>= 2 pedidos, <= 45 dias) intencionalmente.
        PerfilCompra = (TotalPedidos, diasDesdeUltimaCompra) switch
        {
            (0, _)         => ClientePerfilCompraEnum.Prospecto,
            (1, _)         => ClientePerfilCompraEnum.Ocasional,
            ( >= 5, <= 30) => ClientePerfilCompraEnum.Fidelizado,  // alta frequência + recente
            ( >= 2, <= 45) => ClientePerfilCompraEnum.Recorrente,  // frequência moderada + ativo
            _              => ClientePerfilCompraEnum.Ocasional     // comprou mas distante
        };
    }

    private T DeserializeJson<T>(string json) where T : new()
    {
        try
        {
            return string.IsNullOrEmpty(json)
                ? new T()
                : JsonSerializer.Deserialize<T>(json)!;
        }
        catch
        {
            return new T();
        }
    }

    private string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value ?? new object());
    }
}