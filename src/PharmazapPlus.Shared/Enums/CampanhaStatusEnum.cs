namespace PharmazapPlus.Shared.Enums;

public enum CampanhaStatusEnum
{
    /// <summary>Campanha criada mas ainda não agendada</summary>
    Rascunho = 0,

    /// <summary>Campanha agendada para disparo futuro</summary>
    Agendada = 1,

    /// <summary>Campanha em execução (enviando mensagens)</summary>
    Executando = 2,

    /// <summary>Campanha concluída com sucesso</summary>
    Concluida = 3,

    /// <summary>Campanha cancelada manualmente</summary>
    Cancelada = 4,

    /// <summary>Campanha concluída com erros</summary>
    ComErro = 5
}
