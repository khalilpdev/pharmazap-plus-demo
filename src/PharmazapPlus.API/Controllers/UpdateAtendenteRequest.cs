namespace PharmazapPlus.API.Controllers;

public record UpdateAtendenteRequest
{
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Turno { get; set; }
    public bool? RecebeNotificacoes { get; set; }

    public int? DepartamentoId
    {
        get => _departamentoId;
        set
        {
            _departamentoId = value;
            DepartamentoIdInformado = true;
        }
    }

    public bool DepartamentoIdInformado { get; private set; }

    private int? _departamentoId;
}
