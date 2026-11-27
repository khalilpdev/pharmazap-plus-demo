using PharmazapPlus.Shared.Models.Campanhas;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Serviço para chamadas autenticadas à API PharmazapPlus.
    /// </summary>
    public interface IPharmazapApiService
    {
        /// <summary>
        /// Retorna estatísticas do dashboard de acordo com o papel do usuário autenticado.
        /// </summary>
        Task<DashboardStats?> GetDashboardStatsAsync();

        /// <summary>
        /// Retorna atendimentos da farmácia. Filtra por status se fornecido.
        /// </summary>
        Task<List<Atendimento>> GetAtendimentosAsync(string? status = null);

        /// <summary>
        /// Atendente assume um atendimento pendente via POST /api/atendimentos/{id}/assumir.
        /// </summary>
        Task<bool> AssumirAtendimentoAsync(int id);

        /// <summary>
        /// Volta um atendimento em andamento para pendente via POST /api/atendimentos/{id}/voltar-pendente.
        /// </summary>
        Task<bool> VoltarPendenteAsync(int id);

        /// <summary>
        /// Retorna a lista de clientes da farmácia via GET /api/clientes.
        /// </summary>
        Task<List<Cliente>> GetClientesAsync();

        /// <summary>
        /// Retorna a lista de atendentes da farmácia via GET /api/atendentes.
        /// </summary>
        Task<List<AtendenteDto>> GetAtendentesAsync();

        /// <summary>
        /// Cria um novo atendente via POST /api/atendentes.
        /// </summary>
        Task<(bool Success, string Message)> CreateAtendenteAsync(CreateAtendenteDto request);

        /// <summary>
        /// Atualiza um atendente via PUT /api/atendentes/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateAtendenteAsync(int id, UpdateAtendenteDto request);

        /// <summary>
        /// Inativa um atendente via DELETE /api/atendentes/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarAtendenteAsync(int id);

        /// <summary>
        /// Ativa um atendente via PUT /api/atendentes/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarAtendenteAsync(int id);

        /// <summary>
        /// Redefine a senha de um atendente via PUT /api/atendentes/{id}/reset-senha.
        /// </summary>
        Task<(bool Success, string Message)> ResetSenhaAtendenteAsync(int id, string newPassword);

        /// <summary>
        /// Cria um novo cliente via POST /api/clientes.
        /// </summary>
        Task<(bool Success, string Message)> CreateClienteAsync(CreateClienteDto request);

        /// <summary>
        /// Atualiza um cliente via PUT /api/clientes/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateClienteAsync(int id, UpdateClienteDto request);

        /// <summary>
        /// Remove um cliente via DELETE /api/clientes/{id}.
        /// </summary>
        Task<(bool Success, string Message)> DeleteClienteAsync(int id);

        /// <summary>
        /// Reativa um cliente via POST /api/clientes/{id}/reativar.
        /// </summary>
        Task<(bool Success, string Message)> ReativarClienteAsync(int id);

        /// <summary>
        /// Retorna o relatório consolidado para o Gestor via GET /api/relatorios/gestor.
        /// </summary>
        Task<RelatorioGestorData?> GetRelatorioGestorAsync(DateTime? dataInicio = null, DateTime? dataFim = null, int? farmaciaId = null);

        /// <summary>
        /// Retorna o relatório detalhado para o Administrador via GET /api/relatorios/administrador.
        /// </summary>
        Task<RelatorioAdministradorData?> GetRelatorioAdministradorAsync(DateTime? dataInicio = null, DateTime? dataFim = null);

        /// <summary>
        /// Retorna o relatório pessoal do Atendente via GET /api/relatorios/atendente.
        /// </summary>
        Task<RelatorioAtendenteData?> GetRelatorioAtendenteAsync(DateTime? dataInicio = null, DateTime? dataFim = null);

        /// <summary>
        /// Retorna o relatório de faturamento via GET /api/relatorios/faturamento (Gestor e Administrador).
        /// </summary>
        Task<RelatorioFaturamentoData?> GetRelatorioFaturamentoAsync(DateTime? dataInicio = null, DateTime? dataFim = null, int? atendenteId = null, int? farmaciaId = null);

        /// <summary>
        /// Exporta relatório em Excel e retorna os bytes via GET /api/relatorios/exportar.
        /// </summary>
        Task<byte[]?> ExportarRelatorioAsync(string tipo, DateTime? dataInicio = null, DateTime? dataFim = null);

        /// <summary>
        /// Retorna as contagens atuais das filas Redis via GET /api/messages/queue-stats.
        /// </summary>
        Task<QueueStats?> GetQueueStatsAsync();

        /// <summary>
        /// Retorna as mensagens de um atendimento via GET /api/atendimentos/{id}/mensagens.
        /// </summary>
        Task<List<Mensagem>> GetMensagensAtendimentoAsync(int atendimentoId);

        /// <summary>
        /// Envia uma mensagem de texto em um atendimento via POST /api/atendimentos/{id}/mensagens.
        /// </summary>
        Task<(bool Success, string Message)> EnviarMensagemAsync(int atendimentoId, string text);

        /// <summary>
        /// Transfere um atendimento para outro atendente via POST /api/atendimentos/{id}/transferir.
        /// </summary>
        Task<(bool Success, string Message)> TransferirAtendimentoAsync(int atendimentoId, int targetAtendenteId);

        /// <summary>
        /// Retorna a lista de etiquetas da farmácia via GET /api/etiquetas.
        /// </summary>
        Task<List<EtiquetaDto>> GetEtiquetasAsync(bool? ativo = null);

        /// <summary>
        /// Retorna a lista de departamentos da farmácia via GET /api/departamentos.
        /// </summary>
        Task<List<DepartamentoDto>> GetDepartamentosAsync(bool? ativo = null);

        /// <summary>
        /// Retorna a lista de departamentos de uma farmácia específica para gestão via GET /api/account/farmacia/{id}/departamentos.
        /// </summary>
        Task<List<DepartamentoDto>> GetDepartamentosByFarmaciaAsync(int farmaciaId, bool? ativo = null);

        /// <summary>
        /// Cria um novo departamento via POST /api/departamentos.
        /// </summary>
        Task<(bool Success, string Message)> CreateDepartamentoAsync(CreateDepartamentoDto request);

        /// <summary>
        /// Atualiza um departamento via PUT /api/departamentos/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateDepartamentoAsync(int id, UpdateDepartamentoDto request);

        /// <summary>
        /// Inativa um departamento via DELETE /api/departamentos/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarDepartamentoAsync(int id);

        /// <summary>
        /// Ativa um departamento via PUT /api/departamentos/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarDepartamentoAsync(int id);

        /// <summary>
        /// Cria uma nova etiqueta via POST /api/etiquetas.
        /// </summary>
        Task<(bool Success, string Message)> CreateEtiquetaAsync(CreateEtiquetaDto request);

        /// <summary>
        /// Atualiza uma etiqueta via PUT /api/etiquetas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateEtiquetaAsync(int id, UpdateEtiquetaDto request);

        /// <summary>
        /// Inativa uma etiqueta via DELETE /api/etiquetas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarEtiquetaAsync(int id);

        /// <summary>
        /// Ativa uma etiqueta via PUT /api/etiquetas/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarEtiquetaAsync(int id);

        /// <summary>
        /// Atualiza a etiqueta de um atendimento via PUT /api/atendimentos/{id}/etiqueta.
        /// </summary>
        Task<(bool Success, string Message)> AtualizarEtiquetaAtendimentoAsync(int atendimentoId, int? etiquetaId);

        /// <summary>
        /// Finaliza um atendimento via POST /api/atendimentos/{id}/finalizar.
        /// </summary>
        Task<(bool Success, string Message)> FinalizarAtendimentoAsync(int atendimentoId, string? motivo = null, decimal? valorVenda = null, int? motivoEncerramentoId = null, string? comprovanteUrl = null);

        /// <summary>
        /// Upload de comprovante (imagem) para finalização de atendimento via POST /api/atendimentos/{id}/comprovante.
        /// </summary>
        Task<(bool Success, string? ComprovanteUrl, string Message)> UploadComprovanteAsync(int atendimentoId, Stream fileStream, string fileName, string contentType);

        /// <summary>
        /// Baixa mídia (imagem/documento) com autenticação para uso em preview/download no chat.
        /// </summary>
        Task<(bool Success, byte[]? Content, string? ContentType, string Message)> DownloadMediaAsync(string mediaUrl, string? fallbackContentType = null);

        /// <summary>
        /// Retorna os motivos de encerramento ativos da farmácia via GET /api/motivos-encerramento?ativo=true.
        /// </summary>
        Task<List<MotivoEncerramentoDto>> GetMotivosEncerramentoAsync(bool? ativo = null);

        /// <summary>
        /// Cria um novo motivo de encerramento via POST /api/motivos-encerramento.
        /// </summary>
        Task<(bool Success, string Message)> CreateMotivoEncerramentoAsync(CreateMotivoEncerramentoDto request);

        /// <summary>
        /// Atualiza um motivo de encerramento via PUT /api/motivos-encerramento/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateMotivoEncerramentoAsync(int id, UpdateMotivoEncerramentoDto request);

        /// <summary>
        /// Inativa um motivo de encerramento via DELETE /api/motivos-encerramento/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarMotivoEncerramentoAsync(int id);

        /// <summary>
        /// Ativa um motivo de encerramento via PUT /api/motivos-encerramento/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarMotivoEncerramentoAsync(int id);

        /// <summary>
        /// Retorna as atividades recentes do usuário autenticado via GET /api/account/recent-activity.
        /// </summary>
        Task<List<RecentActivityItem>> GetRecentActivitiesAsync(int limit = 20);

        /// <summary>
        /// Retorna a lista de farmácias via GET /api/account/farmacias.
        /// </summary>
        Task<List<FarmaciaDto>> GetFarmaciasAsync();

        /// <summary>
        /// Registra uma nova farmácia via POST /api/account/register-farmacia.
        /// </summary>
        Task<(bool Success, string Message)> CreateFarmaciaAsync(CreateFarmaciaDto request);

        /// <summary>
        /// Atualiza uma farmácia via PUT /api/account/update-farmacia/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateFarmaciaAsync(int id, UpdateFarmaciaDto request);

        /// <summary>
        /// Faz upload da logo da farmácia via POST /api/account/upload-farmacia-logo.
        /// </summary>
        Task<(bool Success, string? LogoUrl, string Message)> UploadFarmaciaLogoAsync(Stream fileStream, string fileName, string contentType);

        /// <summary>
        /// Obtém flag da farmácia para salvar log completo de mídia na integração com API GO.
        /// </summary>
        Task<bool> GetSalvarLogCompletoMidiaApiGoAsync();

        /// <summary>
        /// Atualiza flag da farmácia para salvar log completo de mídia na integração com API GO.
        /// </summary>
        Task<(bool Success, string Message)> UpdateSalvarLogCompletoMidiaApiGoAsync(bool enabled);

        /// <summary>
        /// Retorna a lista de administradores via GET /api/usuarios?role=Administrador.
        /// </summary>
        Task<List<AdministradorDto>> GetAdministradoresAsync();

        /// <summary>
        /// Cria um novo administrador via POST /api/usuarios.
        /// </summary>
        Task<(bool Success, string Message)> CreateAdministradorAsync(CreateAdministradorDto request);

        /// <summary>
        /// Atualiza um administrador via PUT /api/usuarios/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateAdministradorAsync(int id, UpdateAdministradorDto request);

        /// <summary>
        /// Inativa um administrador via DELETE /api/usuarios/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarAdministradorAsync(int id);

        /// <summary>
        /// Ativa um administrador via PUT /api/usuarios/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarAdministradorAsync(int id);

        /// <summary>
        /// Redefine a senha de um administrador via PUT /api/usuarios/{id}/reset-password.
        /// </summary>
        Task<(bool Success, string Message)> ResetSenhaAdministradorAsync(int id, string newPassword);

        /// <summary>
        /// Retorna os atendimentos de um cliente via GET /api/clientes/{id}/atendimentos.
        /// </summary>
        Task<List<AtendimentoCompraDto>> GetAtendimentosClienteAsync(int clienteId);

        /// <summary>
        /// Retorna campanhas da farmácia via GET /api/campanhas.
        /// </summary>
        Task<List<Campanha>> GetCampanhasAsync(string? status = null);

        /// <summary>
        /// Cria uma nova campanha via POST /api/campanhas.
        /// </summary>
        Task<(bool Success, string? Message, Campanha? Data)> CreateCampanhaAsync(CreateCampanhaDto request);

        /// <summary>
        /// Atualiza uma campanha via PUT /api/campanhas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateCampanhaAsync(int id, UpdateCampanhaDto request);

        /// <summary>
        /// Exclui uma campanha via DELETE /api/campanhas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> DeleteCampanhaAsync(int id);

        /// <summary>
        /// Agenda execução de campanha via POST /api/campanhas/{id}/agendar.
        /// </summary>
        Task<(bool Success, string Message)> AgendarCampanhaAsync(int id, DateTime? dataAgendamento);

        /// <summary>
        /// Cancela uma campanha agendada via POST /api/campanhas/{id}/cancelar.
        /// </summary>
        Task<(bool Success, string Message)> CancelarCampanhaAsync(int id);

        /// <summary>
        /// Executa uma campanha imediatamente via POST /api/campanhas/{id}/executar.
        /// </summary>
        Task<(bool Success, string Message)> ExecutarCampanhaAsync(int id);

        /// <summary>
        /// Retorna as mensagens pré-definidas da farmácia via GET /api/mensagens-pre-definidas.
        /// </summary>
        Task<List<MensagemPreDefinidaDto>> GetMensagensPreDefinidasAsync(bool? ativo = null);

        /// <summary>
        /// Cria uma nova mensagem pré-definida via POST /api/mensagens-pre-definidas.
        /// </summary>
        Task<(bool Success, string Message)> CreateMensagemPreDefinidaAsync(CreateMensagemPreDefinidaDto request);

        /// <summary>
        /// Atualiza uma mensagem pré-definida via PUT /api/mensagens-pre-definidas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> UpdateMensagemPreDefinidaAsync(int id, UpdateMensagemPreDefinidaDto request);

        /// <summary>
        /// Inativa uma mensagem pré-definida via DELETE /api/mensagens-pre-definidas/{id}.
        /// </summary>
        Task<(bool Success, string Message)> InativarMensagemPreDefinidaAsync(int id);

        /// <summary>
        /// Ativa uma mensagem pré-definida via PUT /api/mensagens-pre-definidas/{id}/ativar.
        /// </summary>
        Task<(bool Success, string Message)> AtivarMensagemPreDefinidaAsync(int id);

        /// <summary>
        /// Bloqueia um remetente via DELETE /api/atendimentos/{atendimentoId}/bloquear-remetente.
        /// </summary>
        Task<(bool Success, string Message)> BloqueRemetenteAsync(int atendimentoId, string escopo, string? motivo = null);

        /// <summary>
        /// Lista remetentes bloqueados via GET /api/configuracoes/remetentes-bloqueados.
        /// </summary>
        Task<List<RemetenteBloqueado>> ListarRemetentesBlockeadosAsync();

        /// <summary>
        /// Desbloqueia um remetente via DELETE /api/configuracoes/remetentes-bloqueados/{id}.
        /// </summary>
        Task<(bool Success, string Message)> DesbloqueRemetenteAsync(int id);

        /// <summary>
        /// Obtém a configuração de bloqueio de remetentes para a farmácia atual via GET /api/configuracoes/habilitar-bloqueio-remetente.
        /// </summary>
        Task<bool> GetHabilitarBloqueioRemetenteAsync();

        /// <summary>
        /// Atualiza a configuração de bloqueio de remetentes para a farmácia atual via PUT /api/configuracoes/habilitar-bloqueio-remetente.
        /// </summary>
        Task<(bool Success, string Message)> UpdateHabilitarBloqueioRemetenteAsync(bool enabled);

        /// <summary>
        /// Obtém a configuração que libera gestão de etiquetas para atendentes.
        /// </summary>
        Task<bool> GetPermitirGerenciarEtiquetasAtendenteAsync();

        /// <summary>
        /// Atualiza a configuração que libera gestão de etiquetas para atendentes.
        /// </summary>
        Task<(bool Success, string Message)> UpdatePermitirGerenciarEtiquetasAtendenteAsync(bool enabled);

        /// <summary>
        /// Retorna se o usuário autenticado pode gerenciar etiquetas na página de etiquetas.
        /// </summary>
        Task<bool> CanManageEtiquetasAsync();
    }
}
