#nullable disable
using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace PharmazapPlus.Shared.Models.CRM;

public class ClienteCsvImport
{
    // Identificação (Obrigatório: Nome ou WhatsAppId)
    [Name("nome", "Nome", "NOME", "name", "Name")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Name("sobrenome", "Sobrenome", "SOBRENOME", "lastname", "LastName")]
    [MaxLength(100)]
    public string Sobrenome { get; set; } = string.Empty;

    [Name("whatsappid", "WhatsAppId", "WHATSAPPID", "whatsapp", "WhatsApp", "telefone_whatsapp")]
    [MaxLength(50)]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Número de WhatsApp inválido")]
    public string WhatsAppId { get; set; } = string.Empty;

    [Name("apelido", "Apelido", "APELIDO", "nickname")]
    [MaxLength(50)]
    public string Apelido { get; set; } = string.Empty;

    // Contato
    [Name("email", "Email", "EMAIL", "e-mail")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Name("telefone", "Telefone", "TELEFONE", "phone", "Phone", "telefone_fixo")]
    [MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [Name("telefone2", "Telefone2", "TELEFONE2", "phone2", "celular_alternativo")]
    [MaxLength(20)]
    public string Telefone2 { get; set; } = string.Empty;

    // Documentos
    [Name("cpf", "Cpf", "CPF", "documento", "Documento", "CPF_CNPJ")]
    [MaxLength(14)]
    public string Cpf { get; set; } = string.Empty;

    [Name("rg", "Rg", "RG", "identidade")]
    [MaxLength(20)]
    public string Rg { get; set; } = string.Empty;

    [Name("datanascimento", "DataNascimento", "DATANASCIMENTO", "nascimento", "birthdate", "data_nascimento")]
    public DateTime? DataNascimento { get; set; }

    [Name("sexo", "Sexo", "SEXO", "gender", "genero")]
    [MaxLength(1)]
    [RegularExpression(@"^[MFO]$", ErrorMessage = "Sexo deve ser M, F ou O")]
    public string Sexo { get; set; } = string.Empty;

    // Endereço
    [Name("endereco", "Endereco", "ENDERECO", "address", "Address", "logradouro")]
    [MaxLength(200)]
    public string Endereco { get; set; } = string.Empty;

    [Name("numero", "Numero", "NUMERO", "number", "AddressNumber")]
    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Name("complemento", "Complemento", "COMPLEMENTO", "complement", "AddressComplement")]
    [MaxLength(100)]
    public string Complemento { get; set; } = string.Empty;

    [Name("bairro", "Bairro", "BAIRRO", "district", "neighborhood")]
    [MaxLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [Name("cidade", "Cidade", "CIDADE", "city", "City")]
    [MaxLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [Name("estado", "Estado", "ESTADO", "state", "State", "uf", "UF")]
    [MaxLength(2)]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "Estado deve ser sigla de 2 letras (ex: SP)")]
    public string Estado { get; set; } = string.Empty;

    [Name("cep", "Cep", "CEP", "zipcode", "ZipCode", "postal_code")]
    [MaxLength(10)]
    public string Cep { get; set; } = string.Empty;

    [Name("referencia", "Referencia", "REFERENCIA", "reference", "ponto_referencia")]
    [MaxLength(200)]
    public string Referencia { get; set; } = string.Empty;

    // Informações de saúde
    [Name("usocontinuo", "UsoContinuo", "USOCONTINUO", "uso_continuo", "medicamento_continuo")]
    [BooleanTrueValues("sim", "Sim", "SIM", "true", "True", "TRUE", "1", "s", "S", "yes", "Yes", "YES")]
    [BooleanFalseValues("não", "Não", "NÃO", "nao", "Nao", "NAO", "false", "False", "FALSE", "0", "n", "N", "no", "No", "NO")]
    public bool UsoContinuo { get; set; }

    [Name("medicamentoscontinuos", "MedicamentosContinuos", "MEDICAMENTOSCONTINUOS", "medicamentos_continuos", "medicamentos_uso_continuo")]
    [MaxLength(1000)]
    public string MedicamentosContinuos { get; set; } = string.Empty;

    [Name("alergias", "Alergias", "ALERGIAS", "allergies", "alergias_medicamentosas")]
    [MaxLength(1000)]
    public string Alergias { get; set; } = string.Empty;

    [Name("condicoesmedicas", "CondicoesMedicas", "CONDICOESMEDICAS", "condicoes_medicas", "doencas_cronicas")]
    [MaxLength(1000)]
    public string CondicoesMedicas { get; set; } = string.Empty;

    [Name("tiposanguineo", "TipoSanguineo", "TIPOSANGUINEO", "tipo_sanguineo", "blood_type")]
    [MaxLength(5)]
    [RegularExpression(@"^(A|B|AB|O)[+-]$", ErrorMessage = "Tipo sanguíneo inválido (ex: A+, O-)")]
    public string TipoSanguineo { get; set; } = string.Empty;

    [Name("peso", "Peso", "PESO", "weight", "peso_kg")]
    [Range(0, 300, ErrorMessage = "Peso deve estar entre 0 e 300 kg")]
    public decimal? Peso { get; set; }

    [Name("altura", "Altura", "ALTURA", "height", "altura_cm")]
    [Range(0, 250, ErrorMessage = "Altura deve estar entre 0 e 250 cm")]
    public decimal? Altura { get; set; }

    [Name("observacoessaude", "ObservacoesSaude", "OBSERVACOESSAUDE", "observacoes_saude", "health_notes")]
    [MaxLength(2000)]
    public string ObservacoesSaude { get; set; } = string.Empty;

    // Informações profissionais (para profissionais de saúde)
    [Name("crmcrf", "CrmCrf", "CRMCRF", "crm_crf", "registro_profissional")]
    [MaxLength(50)]
    public string CrmCrf { get; set; } = string.Empty;

    [Name("especialidade", "Especialidade", "ESPECIALIDADE", "specialty", "especialidade_medica")]
    [MaxLength(100)]
    public string Especialidade { get; set; } = string.Empty;

    [Name("localtrabalho", "LocalTrabalho", "LOCALTRABALHO", "local_trabalho", "workplace")]
    [MaxLength(200)]
    public string LocalTrabalho { get; set; } = string.Empty;

    // Segmentação e status
    [Name("status", "Status", "STATUS", "client_status")]
    [MaxLength(20)]
    [RegularExpression(@"^(Ativo|Inativo|Bloqueado|Potencial)$", ErrorMessage = "Status inválido. Use: Ativo, Inativo, Bloqueado ou Potencial")]
    public string Status { get; set; } = string.Empty;

    [Name("tipo", "Tipo", "TIPO", "client_type", "tipo_cliente")]
    [MaxLength(20)]
    [RegularExpression(@"^(PessoaFisica|PessoaJuridica|ProfissionalSaude)$", ErrorMessage = "Tipo inválido. Use: PessoaFisica, PessoaJuridica ou ProfissionalSaude")]
    public string Tipo { get; set; } = string.Empty;

    [Name("segmento", "Segmento", "SEGMENTO", "segment", "client_segment")]
    [MaxLength(50)]
    public string Segmento { get; set; } = string.Empty;

    // Metadados
    [Name("origemcadastro", "OrigemCadastro", "ORIGEMCADASTRO", "origem_cadastro", "source")]
    [MaxLength(50)]
    public string OrigemCadastro { get; set; } = string.Empty;

    [Name("indicadopor", "IndicadoPor", "INDICADOPOR", "indicado_por", "referred_by")]
    [MaxLength(100)]
    public string IndicadoPor { get; set; } = string.Empty;

    // Preferências
    [Name("aceitamarketing", "AceitaMarketing", "ACEITAMARKETING", "aceita_marketing", "accepts_marketing")]
    [BooleanTrueValues("sim", "Sim", "SIM", "true", "True", "TRUE", "1", "s", "S", "yes", "Yes", "YES")]
    [BooleanFalseValues("não", "Não", "NÃO", "nao", "Nao", "NAO", "false", "False", "FALSE", "0", "n", "N", "no", "No", "NO")]
    public bool AceitaMarketing { get; set; } = true;

    [Name("aceitanotificacoes", "AceitaNotificacoes", "ACEITANOTIFICACOES", "aceita_notificacoes", "accepts_notifications")]
    [BooleanTrueValues("sim", "Sim", "SIM", "true", "True", "TRUE", "1", "s", "S", "yes", "Yes", "YES")]
    [BooleanFalseValues("não", "Não", "NÃO", "nao", "Nao", "NAO", "false", "False", "FALSE", "0", "n", "N", "no", "No", "NO")]
    public bool AceitaNotificacoes { get; set; } = true;

    [Name("preferenciascontato", "PreferenciasContato", "PREFERENCIASCONTATO", "preferencias_contato", "contact_preferences")]
    [MaxLength(50)]
    [RegularExpression(@"^(WhatsApp|Email|Telefone|Todos)$", ErrorMessage = "Preferência de contato inválida. Use: WhatsApp, Email, Telefone ou Todos")]
    public string PreferenciasContato { get; set; } = "WhatsApp";

    [Name("observacoesgerais", "ObservacoesGerais", "OBSERVACOESGERAIS", "observacoes_gerais", "general_notes")]
    [MaxLength(2000)]
    public string ObservacoesGerais { get; set; } = string.Empty;

    // Tags
    [Name("tags", "Tags", "TAGS", "tag", "Tag", "cliente_tags")]
    [MaxLength(500)]
    public string Tags { get; set; } = string.Empty;

    // Campos de auditoria (opcionais para importação)
    [Name("criadopor", "CriadoPor", "CRIADOPOR", "criado_por", "created_by")]
    [MaxLength(100)]
    public string CriadoPor { get; set; } = string.Empty;

    // Propriedades calculadas/auxiliares
    [Ignore]
    public string NomeCompleto => $"{Nome} {Sobrenome}".Trim();

    [Ignore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Nome) || !string.IsNullOrWhiteSpace(WhatsAppId);

    [Ignore]
    public List<string> ValidationErrors { get; set; } = new();

    // Método de validação
    public bool Validate()
    {
        ValidationErrors.Clear();

        // Validações obrigatórias
        if (string.IsNullOrWhiteSpace(Nome) && string.IsNullOrWhiteSpace(WhatsAppId))
        {
            ValidationErrors.Add("Nome ou WhatsAppId é obrigatório");
        }

        // Validação de WhatsAppId (se fornecido)
        if (!string.IsNullOrWhiteSpace(WhatsAppId))
        {
            var cleanNumber = new string(WhatsAppId.Where(char.IsDigit).ToArray());
            if (cleanNumber.Length < 10)
            {
                ValidationErrors.Add($"WhatsAppId inválido: {WhatsAppId}");
            }
        }

        // Validação de CPF (se fornecido)
        if (!string.IsNullOrWhiteSpace(Cpf) && !IsValidCpfCnpj(Cpf))
        {
            ValidationErrors.Add($"CPF/CNPJ inválido: {Cpf}");
        }

        // Validação de email (se fornecido)
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email))
        {
            ValidationErrors.Add($"Email inválido: {Email}");
        }

        // Validação de data de nascimento (se fornecida)
        if (DataNascimento.HasValue)
        {
            if (DataNascimento.Value > DateTime.Now)
            {
                ValidationErrors.Add($"Data de nascimento futura: {DataNascimento.Value:dd/MM/yyyy}");
            }
            if (DataNascimento.Value < DateTime.Now.AddYears(-120))
            {
                ValidationErrors.Add($"Data de nascimento muito antiga: {DataNascimento.Value:dd/MM/yyyy}");
            }
        }

        // Validação de estado (se fornecido)
        if (!string.IsNullOrWhiteSpace(Estado) && Estado.Length != 2)
        {
            ValidationErrors.Add($"Estado inválido: {Estado}. Use sigla de 2 letras (ex: SP)");
        }

        // Validação de CEP (se fornecido)
        if (!string.IsNullOrWhiteSpace(Cep))
        {
            var cleanCep = new string(Cep.Where(char.IsDigit).ToArray());
            if (cleanCep.Length != 8)
            {
                ValidationErrors.Add($"CEP inválido: {Cep}");
            }
        }

        return !ValidationErrors.Any();
    }

    // Método para limpar e formatar dados
    public void CleanData()
    {
        // Limpar espaços em branco
        Nome = Nome?.Trim();
        Sobrenome = Sobrenome?.Trim();
        WhatsAppId = WhatsAppId?.Trim();
        Email = Email?.Trim()?.ToLower();
        Telefone = Telefone?.Trim();
        Telefone2 = Telefone2?.Trim();
        Cpf = Cpf?.Trim();
        Rg = Rg?.Trim();
        Endereco = Endereco?.Trim();
        Numero = Numero?.Trim();
        Complemento = Complemento?.Trim();
        Bairro = Bairro?.Trim();
        Cidade = Cidade?.Trim();
        Estado = Estado?.Trim()?.ToUpper();
        Cep = Cep?.Trim();
        Referencia = Referencia?.Trim();
        CrmCrf = CrmCrf?.Trim();
        Especialidade = Especialidade?.Trim();
        LocalTrabalho = LocalTrabalho?.Trim();
        Segmento = Segmento?.Trim();
        OrigemCadastro = OrigemCadastro?.Trim();
        IndicadoPor = IndicadoPor?.Trim();
        PreferenciasContato = PreferenciasContato?.Trim();
        CriadoPor = CriadoPor?.Trim();
        Tags = Tags?.Trim();

        // Limpar WhatsAppId (remover +, espaços, etc.)
        if (!string.IsNullOrEmpty(WhatsAppId))
        {
            WhatsAppId = new string(WhatsAppId.Where(char.IsDigit).ToArray());
            if (!WhatsAppId.StartsWith("55") && WhatsAppId.Length >= 10)
            {
                WhatsAppId = "55" + WhatsAppId;
            }
        }

        // Limpar CPF/CNPJ
        if (!string.IsNullOrEmpty(Cpf))
        {
            Cpf = new string(Cpf.Where(char.IsDigit).ToArray());
        }

        // Limpar CEP
        if (!string.IsNullOrEmpty(Cep))
        {
            Cep = new string(Cep.Where(char.IsDigit).ToArray());
        }

        // Limpar telefones
        if (!string.IsNullOrEmpty(Telefone))
        {
            Telefone = new string(Telefone.Where(char.IsDigit).ToArray());
        }
        if (!string.IsNullOrEmpty(Telefone2))
        {
            Telefone2 = new string(Telefone2.Where(char.IsDigit).ToArray());
        }

        // Converter strings booleanas
        if (Status != null)
        {
            Status = Status.ToLower() switch
            {
                "ativo" or "active" or "1" => "Ativo",
                "inativo" or "inactive" or "0" => "Inativo",
                "bloqueado" or "blocked" => "Bloqueado",
                "potencial" or "potential" => "Potencial",
                _ => Status
            };
        }

        if (Tipo != null)
        {
            Tipo = Tipo.ToLower() switch
            {
                "pessoafisica" or "pf" or "fisica" => "PessoaFisica",
                "pessoajuridica" or "pj" or "juridica" => "PessoaJuridica",
                "profissionalsaude" or "saude" or "medico" => "ProfissionalSaude",
                _ => Tipo
            };
        }
    }

    // Método para converter para entidade Cliente
    public Cliente ToClienteEntity(int farmaciaId, string codigoCliente)
    {
        var cliente = new Cliente
        {
            FarmaciaId = farmaciaId,
            CodigoCliente = codigoCliente,
            Nome = Nome,
            Sobrenome = Sobrenome,
            WhatsAppId = WhatsAppId,
            Farmacia = null!,
            Apelido = Apelido,
            Email = Email,
            Telefone = Telefone,
            Telefone2 = Telefone2,
            Cpf = Cpf,
            Rg = Rg,
            DataNascimento = DataNascimento,
            Sexo = Sexo,
            Endereco = Endereco,
            Numero = Numero,
            Complemento = Complemento,
            Bairro = Bairro,
            Cidade = Cidade,
            Estado = Estado,
            Cep = Cep,
            Referencia = Referencia,
            UsoContinuo = UsoContinuo,
            MedicamentosContinuos = MedicamentosContinuos,
            Alergias = Alergias,
            CondicoesMedicas = CondicoesMedicas,
            TipoSanguineo = TipoSanguineo,
            Peso = Peso,
            Altura = Altura,
            ObservacoesSaude = ObservacoesSaude,
            CrmCrf = CrmCrf,
            Especialidade = Especialidade,
            LocalTrabalho = LocalTrabalho,
            Status = ParseClienteStatus(Status),
            Tipo = ParseClienteTipo(Tipo),
            Segmento = Segmento ?? "Novo",
            OrigemCadastro = OrigemCadastro ?? "Importação CSV",
            IndicadoPor = IndicadoPor,
            AceitaMarketing = AceitaMarketing,
            AceitaNotificacoes = AceitaNotificacoes,
            PreferenciasContato = PreferenciasContato,
            ObservacoesGerais = ObservacoesGerais,
            DataCadastro = DateTime.UtcNow,
            UltimoContato = DateTime.UtcNow,
            CriadoPor = CriadoPor ?? "Importação CSV"
        };

        return cliente;
    }

    // Métodos auxiliares de parsing
    private ClienteStatus ParseClienteStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return ClienteStatus.Ativo;

        return status.ToLower() switch
        {
            "ativo" => ClienteStatus.Ativo,
            "inativo" => ClienteStatus.Inativo,
            "bloqueado" => ClienteStatus.Bloqueado,
            "potencial" => ClienteStatus.Potencial,
            _ => ClienteStatus.Ativo
        };
    }

    private ClienteTipo ParseClienteTipo(string tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            return ClienteTipo.PessoaFisica;

        return tipo.ToLower() switch
        {
            "pessoafisica" => ClienteTipo.PessoaFisica,
            "pessoajuridica" => ClienteTipo.PessoaJuridica,
            "profissionalsaude" => ClienteTipo.ProfissionalSaude,
            _ => ClienteTipo.PessoaFisica
        };
    }

    // Validação de CPF/CNPJ
    private bool IsValidCpfCnpj(string cpfCnpj)
    {
        var numbers = new string(cpfCnpj.Where(char.IsDigit).ToArray());

        if (numbers.Length == 11)
            return IsValidCpf(numbers);
        else if (numbers.Length == 14)
            return IsValidCnpj(numbers);

        return false;
    }

    private bool IsValidCpf(string cpf)
    {
        if (cpf.Length != 11)
            return false;

        // Verifica se todos os dígitos são iguais
        if (cpf.Distinct().Count() == 1)
            return false;

        // Cálculo do primeiro dígito verificador
        int[] multiplicador1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        string tempCpf = cpf.Substring(0, 9);
        int soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        // Cálculo do segundo dígito verificador
        int[] multiplicador2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        tempCpf = tempCpf + digito1;
        soma = 0;

        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return cpf.EndsWith($"{digito1}{digito2}");
    }

    private bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Length != 14)
            return false;

        // Cálculo do primeiro dígito verificador
        int[] multiplicador1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        string tempCnpj = cnpj.Substring(0, 12);
        int soma = 0;

        for (int i = 0; i < 12; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicador1[i];

        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        // Cálculo do segundo dígito verificador
        int[] multiplicador2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        tempCnpj = tempCnpj + digito1;
        soma = 0;

        for (int i = 0; i < 13; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return cnpj.EndsWith($"{digito1}{digito2}");
    }
}
#nullable restore