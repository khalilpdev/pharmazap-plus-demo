namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Estatísticas das filas Redis retornadas pela API.
    /// </summary>
    public record QueueStats
    {
        public long NovasMensagens { get; init; }
        public long Notificacoes { get; init; }
        public long Agendamentos { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
