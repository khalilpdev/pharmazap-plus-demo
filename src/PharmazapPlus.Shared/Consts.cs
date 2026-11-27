namespace PharmazapPlus.Shared;

public static class Consts
{
    public static class Web
    {
        public const string ApiClientName = "PharmazapApi";
        public const string TokenStorageKey = "pharmazap_token";
        public const string UserStorageKey = "pharmazap_user";
        public const string CookieName = "PharmazapAuth";
        public const string LoginPath = "/login";
        public const string LogoutPath = "/logout";
        public const string AcessoNegadoPath = "/acesso-negado";
        public const int CookieExpiracaoDias = 30;
        public const int HttpTimeoutSeconds = 60;
    }

    public static class Endpoints
    {
        public const string Login = "/api/account/login";
        public const string UserInfo = "/api/account/userinfo";
        public const string UpdateTheme = "/api/account/theme";
        public const string UpdateAccentColor = "/api/account/accent-color";
        public const string AceitarTermos = "/api/account/aceitar-termos";
        public const string PharmaHub = "/hubs/pharma";
        public const string InstagramWebhook = "/api/instagram/webhook";
        public const string SwaggerJson = "/swagger/v1/swagger.json";
        public const string ApiDocs = "api-docs";
        public const string Health = "/health";
    }

    public struct ClienteFormDialog
    {
        public const string TituloNovo = "Novo Cliente";
        public const string TituloEdicao = "Editar Cliente";

        public const string StatusAtivo = "Ativo";
        public const string StatusInativo = "Inativo";
        public const string StatusBloqueado = "Bloqueado";
        public const string StatusPotencial = "Potencial";

        public const string TipoPessoaFisica = "PessoaFisica";
        public const string TipoPessoaJuridica = "PessoaJuridica";
        public const string TipoProfissionalSaude = "ProfissionalSaude";

        public const string PerfilProspecto = "Prospecto";
        public const string PerfilOcasional = "Ocasional";
        public const string PerfilRecorrente = "Recorrente";
        public const string PerfilUsoContinuo = "UsoContinuo";
        public const string PerfilFidelizado = "Fidelizado";

        public const string MensagemErroNomeObrigatorio = "Nome é obrigatório.";
        public const string MensagemErroWhatsAppObrigatorio = "WhatsApp é obrigatório.";
    }

    public struct WhatsAppConfig
    {
        public const string WhatsAppDevelopmentMode = "WhatsApp:DevelopmentMode";
        public const string UserRealApi = "WhatsApp:UseRealApi";
    }

    public struct InstagramConfig
    {
        public const string InstagramDevelopmentMode = "Instagram:DevelopmentMode";
        public const string UseRealApi = "Instagram:UseRealApi";
    }

    public struct ClienteSegmentosConfig
    {
        public const decimal LimiteVip = 1000m;
        public const decimal LimiteRegular = 500m;
        public const decimal DivisorPontuacaoFidelidade = 10m;
    }

    public struct ClienteDefaults
    {
        public const string OrigemCadastro = "WhatsApp";
        public const string PreferenciasContato = "WhatsApp";
        public const string CriadoPor = "Sistema";
    }

    public struct SignalR
    {
        public const string GrupoFarmaciaPrefix = "farmacia-{0}";
        public const string GrupoAtendentesPrefix = "atendentes-{0}";
        public const string GrupoAtendimentoPrefix = "atendimento-{0}";

        public static string GrupoFarmacia(string farmaciaId) => string.Format(GrupoFarmaciaPrefix, farmaciaId);
        public static string GrupoAtendentes(string farmaciaId) => string.Format(GrupoAtendentesPrefix, farmaciaId);
        public static string GrupoAtendimento(int atendimentoId) => string.Format(GrupoAtendimentoPrefix, atendimentoId);

        public struct Metodos
        {
            public const string NovaMensagemRecebida = "NovaMensagemRecebida";
            public const string NovoAtendimentoDisponivel = "NovoAtendimentoDisponivel";
            public const string Notification = "Notification";
            public const string AgendamentoReminder = "AgendamentoReminder";
            public const string UserStatusChanged = "UserStatusChanged";
            public const string AtendimentoAssumido = "AtendimentoAssumido";
            public const string AtendimentoTransferido = "AtendimentoTransferido";
            public const string AtendimentoTransferidoAuto = "AtendimentoTransferidoAuto";
            public const string AtendimentoVoltadoPendente = "AtendimentoVoltadoPendente";
            public const string AtendimentosReabertos = "AtendimentosReabertos";
            public const string LoadMensagens = "LoadMensagens";
            public const string NewMessage = "NewMessage";
            public const string UserTyping = "UserTyping";
            public const string MessageStatusUpdated = "MessageStatusUpdated";
            public const string ClienteUpdated = "ClienteUpdated";
        }
    }

    public struct Filas
    {
        public const string NovasMensagens = "new-messages";
        public const string Notificacoes = "notifications";
        public const string Agendamentos = "agendamentos";
    }

    public struct Claims
    {
        public const string FarmaciaId = "FarmaciaId";
    }

    public struct Cors
    {
        public const string PolicyName = "PharmazapCors";
        public static readonly string[] DefaultOrigins = [
            "http://localhost:8080",
            "https://localhost:8080",
            "http://localhost:5000",
            "https://localhost:5000",
            "https://*.pharmazapplus.com"
        ];
    }

    public struct ProcessamentoDeMensagens
    {
        public const int IntervalSeconds = 5;
        public const int MaxMensagensPorCiclo = 10;
        public const int MaxNotificacoesPorCiclo = 5;
        public const int MaxAgendamentosParaVerificar = 20;
    }

    public struct Database
    {
        public const int MaxRetryCount = 5;
        public const int MaxRetryDelaySeconds = 30;
        public const int PasswordSaltBytes = 16;
    }

    public struct SignalRConfig
    {
        public const long MaximumReceiveMessageSizeBytes = 1024L * 1024 * 10; // 10 MB
        public const string UrlConfigurationKey = "Application:SignalRUrl";
        public const string ChannelPrefix = "PharmazapPlus";
    }

    public struct MetaApi
    {
        public const string BaseUrl = "https://graph.facebook.com/";
        public const string LoginBaseUrl = "https://www.facebook.com/";
        public const string ClientName = "MetaAPI";
        public const string TestAppId = "1759112728787714";
        public const string ProductionAppId = "953591737464975";
    }

    public struct Farmacia
    {
        public const int DefaultLimiteAtendentes = 5;
        public const int DefaultLimiteClientes = 1000;
        public const string DefaultPlano = "Basic";
    }

    public struct Agendamento
    {
        public const int FrequenciaPadraoEmDias = 30;
    }

    public struct Formato
    {
        public const string DataHora = "dd/MM HH:mm";
    }

    public struct Midia
    {
        public const string Image = "image";
        public const string Sticker = "sticker";
        public const string Video = "video";
        public const string Audio = "audio";
        public const string Document = "document";
        public const string BlobStorageHost = "pharmazapplus.blob.core.windows.net";

        public const string MimeVideoDefault = "video/mp4";
        public const string MimeAudioDefault = "audio/ogg";

        /// <summary>
        /// Relative path (from ContentRootPath) to the media uploads directory.
        /// </summary>
        public const string UploadsRelativePath = "wwwroot/uploads/media";

        /// <summary>
        /// URL prefix for locally stored media files.
        /// </summary>
        public const string UploadUrlPrefix = "/uploads/media/";
    }

    public struct WhatsAppCdn
    {
        /// <summary>Primary WhatsApp CDN prefix (mmg.whatsapp.net).</summary>
        public const string MmgPrefix = "https://mmg.whatsapp.net";

        /// <summary>Alternate WhatsApp CDN prefix (mmg-files.*).</summary>
        public const string MmgFilesPrefix = "https://mmg-files.";

        /// <summary>Domain fragment shared by all WhatsApp CDN hosts.</summary>
        public const string WhatsAppNetDomain = "whatsapp.net";
    }

    /// <summary>
    /// Tipo do canal de integração armazenado em <c>Canal.Tipo</c>.
    /// </summary>
    public struct CanalTipo
    {
        public const int WhatsApp = 1;
        public const int Instagram = 2;
    }

    public static class EvolutionGoWebhook
    {
        public static readonly string[] ContactNameDirectFields = ["pushName", "senderPushName", "notifyName", "contactName"];
        public static readonly string[] ContactNameContainerFields = ["Info", "sender", "contact", "key", "senderData", "participant"];
        public static readonly string[] ContactNameNestedFields = ["pushName", "senderPushName", "notifyName", "contactName", "name"];
        public const string GroupNameMetadataKey = "groupName";
        public const string SenderNameMetadataKey = "senderName";
    }

    /// <summary>
    /// Status de instância retornados pela Evolution Go API.
    /// </summary>
    public struct EvolutionGoInstance
    {
        /// <summary>Instância conectada e pronta para uso.</summary>
        public const string StatusOpen = "OPEN";
        /// <summary>Instância conectada (sinônimo de OPEN em algumas versões).</summary>
        public const string StatusConnected = "CONNECTED";
        /// <summary>Instância desconectada.</summary>
        public const string StatusClose = "CLOSE";
        /// <summary>Instância desconectada (sinônimo de CLOSE em algumas versões).</summary>
        public const string StatusDisconnected = "DISCONNECTED";
        /// <summary>Instância aguardando scan do QR Code.</summary>
        public const string StatusQrCode = "QRCODE";
    }

    public struct PharmazapApi
    {
        public const string ClientName = Web.ApiClientName;
        public const string TokenStorageKey = Web.TokenStorageKey;

        public struct Endpoints
        {
            public const string DashboardStats = "api/account/dashboard-stats";
            public const string RecentActivity = "api/account/recent-activity";
            public const string Atendimentos = "api/atendimentos";
            public const string Clientes = "api/clientes";
            public const string RelatorioGestor = "api/relatorios/gestor";
            public const string RelatorioAdministrador = "api/relatorios/administrador";
            public const string RelatorioAtendente = "api/relatorios/atendente";
            public const string RelatorioFaturamento = "api/relatorios/faturamento";
            public const string RelatorioExportar = "api/relatorios/exportar";
            public const string QueueStats = "api/messages/queue-stats";
            public const string Etiquetas = "api/etiquetas";
            public const string MotivosEncerramento = "api/motivos-encerramento";
            public const string Farmacias = "api/account/farmacias";
            public const string RegisterFarmacia = "api/account/register-farmacia";
            public const string ChannelsQrCode = "api/channels/qrcode";
            public const string ChannelsPairingCode = "api/channels/pairingcode";
            public const string ChannelsWhatsAppStatus = "api/channels/whatsapp/status";
            public const string ChannelsWhatsAppInstances = "api/channels/whatsapp/instances";
            public const string ChannelsWhatsAppInstanceInfo = "api/channels/whatsapp/instance-info";
            public const string ChannelsWhatsAppEnsureInstance = "api/channels/whatsapp/ensure-instance";
        }

        public struct Mensagens
        {
            public const string ClienteCriadoComSucesso = "Cliente criado com sucesso";
            public const string ErroCriarCliente = "Erro ao criar cliente";
            public const string ClienteAtualizadoComSucesso = "Cliente atualizado com sucesso";
            public const string ErroAtualizarCliente = "Erro ao atualizar cliente";
            public const string ClienteRemovidoComSucesso = "Cliente inativado com sucesso";
            public const string ErroRemoverCliente = "Erro ao inativar cliente";
            public const string ClienteInativoComAtendimentos = "Cliente possui atendimentos ativos. Ao inativar, não receberá novas mensagens automaticamente.";
            public const string ClienteInativoSolicitouAtendimento = "Cliente inativo solicitou atendimento";
            public const string ClienteBloqueadoMensagem = "Mensagem de cliente bloqueado rejeitada";
            public const string AtendimentoTransferidoComSucesso = "Atendimento transferido com sucesso";
            public const string ErroTransferirAtendimento = "Erro ao transferir atendimento";
            public const string EtiquetaCriadaComSucesso = "Etiqueta criada com sucesso";
            public const string ErroCriarEtiqueta = "Erro ao criar etiqueta";
            public const string EtiquetaAtualizadaComSucesso = "Etiqueta atualizada com sucesso";
            public const string ErroAtualizarEtiqueta = "Erro ao atualizar etiqueta";
            public const string EtiquetaInativadaComSucesso = "Etiqueta inativada com sucesso";
            public const string ErroInativarEtiqueta = "Erro ao inativar etiqueta";
            public const string EtiquetaAtivadaComSucesso = "Etiqueta ativada com sucesso";
            public const string ErroAtivarEtiqueta = "Erro ao ativar etiqueta";
            public const string EtiquetaAtendimentoAtualizadaComSucesso = "Etiqueta atualizada com sucesso";
            public const string ErroAtualizarEtiquetaAtendimento = "Erro ao atualizar etiqueta do atendimento";
            public const string AtendimentoFinalizadoComSucesso = "Atendimento finalizado com sucesso";
            public const string ErroFinalizarAtendimento = "Erro ao finalizar atendimento";
            public const string UploadComprovanteComSucesso = "Upload realizado com sucesso";
            public const string ErroUploadComprovante = "Erro ao fazer upload do comprovante";
            public const string MotivoCriadoComSucesso = "Motivo criado com sucesso";
            public const string ErroCriarMotivo = "Erro ao criar motivo";
            public const string ErroCriarMotivoEncerramento = "Erro ao criar motivo de encerramento";
            public const string MotivoAtualizadoComSucesso = "Motivo atualizado com sucesso";
            public const string ErroAtualizarMotivo = "Erro ao atualizar motivo";
            public const string ErroAtualizarMotivoEncerramento = "Erro ao atualizar motivo de encerramento";
            public const string MotivoInativadoComSucesso = "Motivo inativado com sucesso";
            public const string ErroInativarMotivo = "Erro ao inativar motivo";
            public const string ErroInativarMotivoEncerramento = "Erro ao inativar motivo de encerramento";
            public const string MotivoAtivadoComSucesso = "Motivo ativado com sucesso";
            public const string ErroAtivarMotivo = "Erro ao ativar motivo";
            public const string ErroAtivarMotivoEncerramento = "Erro ao ativar motivo de encerramento";
            public const string FarmaciaRegistradaComSucesso = "Farmácia registrada com sucesso";
            public const string ErroRegistrarFarmacia = "Erro ao registrar farmácia";
            public const string FalhaBuscarEtiquetas = "Falha ao buscar etiquetas. Status: {0}";
            public const string FalhaBuscarFarmacias = "Falha ao buscar farmácias. Status: {0}";
        }
    }

    public struct Tabs
    {
        public const string Pending = "pending";
        public const string PendingGroups = "pending-groups";
        public const string Active = "active";
        public const string Finalized = "finalized";
    }

    public struct DesignTokens
    {
        public const string SuccessBackground = "var(--colorStatusSuccessBackground3)";
        public const string WarningBackground = "var(--colorStatusWarningBackground3)";
    }

    public struct EtiquetaCores
    {
        public const string Roxo = "#6264A7";
        public const string Azul = "#0078D4";
        public const string AzulClaro = "#2B88D8";
        public const string Ciano = "#038387";
        public const string Verde = "#107C10";
        public const string VerdeClaro = "#00B294";
        public const string Amarelo = "#FFB900";
        public const string Laranja = "#CA5010";
        public const string Vermelho = "#D13438";
        public const string Rosa = "#E3008C";
        public const string Cinza = "#69797E";
        public const string CinzaEscuro = "#323130";

        /// <summary>Cor padrão aplicada a novas etiquetas.</summary>
        public const string Padrao = Roxo;

        /// <summary>
        /// Paleta completa para exibição em combos/selects.
        /// Cada entrada contém (Nome de exibição, valor Hex).
        /// </summary>
        public static readonly (string Nome, string Hex)[] Paleta =
        [
            ("Roxo (Padrão)", Roxo),
            ("Azul",          Azul),
            ("Azul Claro",    AzulClaro),
            ("Ciano",         Ciano),
            ("Verde",         Verde),
            ("Verde Claro",   VerdeClaro),
            ("Amarelo",       Amarelo),
            ("Laranja",       Laranja),
            ("Vermelho",      Vermelho),
            ("Rosa",          Rosa),
            ("Cinza",         Cinza),
            ("Cinza Escuro",  CinzaEscuro),
        ];
    }

    public struct AtendentesMessages
    {
        // Mensagens de validação
        public const string FarmaciaIdNaoIdentificada = "FarmaciaId não identificada";
        public const string CamposObrigatorios = "Nome, Email e Senha são obrigatórios";
        public const string SenhaMinima = "A senha deve ter no mínimo 6 caracteres";
        public const string FarmaciaNaoEncontrada = "Farmácia não encontrada";
        public const string EmailJaCadastrado = "Email já cadastrado nesta farmácia";
        public const string AtendenteNaoEncontrado = "Atendente não encontrado";
        public const string NovaSenhaObrigatoria = "A nova senha é obrigatória";

        // Mensagens de sucesso
        public const string AtendenteCriado = "Atendente criado com sucesso";
        public const string AtendenteAtualizado = "Atendente atualizado com sucesso";
        public const string AtendenteInativado = "Atendente inativado com sucesso";
        public const string AtendenteAtivado = "Atendente ativado com sucesso";
        public const string SenhaRedefinida = "Senha redefinida com sucesso";

        // Mensagens de erro
        public const string ErroListar = "Erro ao listar atendentes";
        public const string ErroBuscar = "Erro ao buscar atendente";
        public const string ErroCriar = "Erro ao criar atendente";
        public const string ErroAtualizar = "Erro ao atualizar atendente";
        public const string ErroInativar = "Erro ao inativar atendente";
        public const string ErroAtivar = "Erro ao ativar atendente";
        public const string ErroRedefinirSenha = "Erro ao redefinir senha";

        // Valores padrão
        public const string TurnoPadrao = "Integral";
    }

    public struct Auth
    {
        public const string Scheme = "Bearer";
        public const string SwaggerClientId = "swagger-ui";
        public const string SwaggerAppName = "Pharmazap Plus API - Swagger";
    }

    public struct System
    {
        public const string ApplicationName = "Pharmazap Plus";
        public const string ApiVersion = "1.0.0";
    }

    public struct FarmaciaLogo
    {
        /// <summary>
        /// Relative path (from ContentRootPath) to the logos uploads directory.
        /// </summary>
        public const string UploadsRelativePath = "wwwroot/uploads/logos";

        /// <summary>
        /// Route template for the dedicated logo-serving endpoint.
        /// </summary>
        public const string ServeEndpointRoute = "farmacia-logo/{fileName}";

        /// <summary>
        /// Backward-compatible route for old-format logo URLs already stored in the DB.
        /// </summary>
        public const string LegacyRoute = "/uploads/logos/{fileName}";

        /// <summary>
        /// Format string for the logo URL returned after upload ({0} = fileName, {1} = Unix timestamp).
        /// </summary>
        public const string UrlFormat = "/api/account/farmacia-logo/{0}?v={1}";

        /// <summary>
        /// Cache-Control header value applied when serving logo images.
        /// </summary>
        public const string CacheControlValue = "public, max-age=31536000, immutable";

        /// <summary>
        /// Allowed file extensions for logo uploads.
        /// </summary>
        public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

        /// <summary>
        /// Returns the MIME content type for the given (lower-case) file extension.
        /// </summary>
        public static string GetContentType(string lowerExtension) => lowerExtension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            _                 => "application/octet-stream"
        };
    }
}
