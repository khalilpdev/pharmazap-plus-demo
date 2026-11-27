namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Estatísticas do dashboard retornadas pela API para o usuário autenticado.
    /// </summary>
    public class DashboardStats
    {
        // Gestor
        public int? TotalFarmacias { get; set; }
        public int? TotalAdministradores { get; set; }
        // Gestor + Administrador
        public int? AtendimentosHoje { get; set; }
        public int? AtendimentosEmAndamento { get; set; }
        // Administrador
        public int? TotalAtendentes { get; set; }
        public int? TotalClientes { get; set; }
        // Atendente
        public int? AtendimentosPendentes { get; set; }
        public int? AtendimentosFinalizadosHoje { get; set; }
    }
}
