#nullable disable

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public class ClienteImporter : IClienteImporter
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<ClienteImporter> _logger;

    public ClienteImporter(
        PharmazapDbContext context,
        ILogger<ClienteImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImportResult> ImportFromCsvAsync(string farmaciaId, Stream csvStream)
    {
        var result = new ImportResult();

        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
            {
                result.Errors.Add("ID da farmácia inválido");
                return result;
            }

            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = DetectDelimiter(reader),
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = context =>
                {
                    result.Errors.Add($"Dados inválidos: {context.RawRecord}");
                },
                HeaderValidated = null,
                PrepareHeaderForMatch = args => args.Header.ToLower()
            });

            // Registrar mapeamentos
            csv.Context.RegisterClassMap<ClienteCsvImportMap>();

            var records = new List<ClienteCsvImport>();
            await foreach (var record in csv.GetRecordsAsync<ClienteCsvImport>())
            {
                records.Add(record);
            }

            result.TotalRows = records.Count;

            // Processar registros em lotes
            var batchSize = 100;
            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize).ToList();
                await ProcessImportBatchAsync(farmaciaIdInt, batch, result);
            }

            result.Success = result.ImportedRows > 0;

            _logger.LogInformation("Importação CSV concluída: {Importados} de {Total} clientes importados com sucesso",
                result.ImportedRows, result.TotalRows);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Erro geral na importação: {ex.Message}");
            _logger.LogError(ex, "Erro na importação CSV");
        }

        return result;
    }

    public async Task<ImportResult> ImportFromExcelAsync(string farmaciaId, Stream excelStream)
    {
        var result = new ImportResult();

        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
            {
                result.Errors.Add("ID da farmácia inválido");
                return result;
            }

            using var package = new ExcelPackage(excelStream);
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet == null || worksheet.Dimension == null)
            {
                result.Errors.Add("Planilha vazia ou inválida");
                return result;
            }

            var records = new List<ClienteCsvImport>();
            int rowCount = worksheet.Dimension.Rows;
            int colCount = worksheet.Dimension.Columns;

            // Mapear cabeçalhos
            var headers = new Dictionary<string, int>();
            for (int col = 1; col <= colCount; col++)
            {
                var header = worksheet.Cells[1, col].Text?.Trim().ToLower();
                if (!string.IsNullOrEmpty(header))
                {
                    headers[header] = col;
                }
            }

            // Processar linhas
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var record = new ClienteCsvImport();

                    // Mapear colunas dinamicamente
                    if (headers.ContainsKey("nome")) record.Nome = worksheet.Cells[row, headers["nome"]].Text;
                    if (headers.ContainsKey("sobrenome")) record.Sobrenome = worksheet.Cells[row, headers["sobrenome"]].Text;
                    if (headers.ContainsKey("whatsappid")) record.WhatsAppId = worksheet.Cells[row, headers["whatsappid"]].Text;
                    if (headers.ContainsKey("email")) record.Email = worksheet.Cells[row, headers["email"]].Text;
                    if (headers.ContainsKey("telefone")) record.Telefone = worksheet.Cells[row, headers["telefone"]].Text;
                    if (headers.ContainsKey("cpf")) record.Cpf = worksheet.Cells[row, headers["cpf"]].Text;

                    if (headers.ContainsKey("datanascimento"))
                    {
                        var dataNascimentoCell = worksheet.Cells[row, headers["datanascimento"]];
                        if (dataNascimentoCell.Value != null)
                        {
                            if (dataNascimentoCell.Value is DateTime dt)
                                record.DataNascimento = dt;
                            else if (DateTime.TryParse(dataNascimentoCell.Text, out var parsedDate))
                                record.DataNascimento = parsedDate;
                        }
                    }

                    if (headers.ContainsKey("endereco")) record.Endereco = worksheet.Cells[row, headers["endereco"]].Text;
                    if (headers.ContainsKey("cidade")) record.Cidade = worksheet.Cells[row, headers["cidade"]].Text;
                    if (headers.ContainsKey("estado")) record.Estado = worksheet.Cells[row, headers["estado"]].Text;
                    if (headers.ContainsKey("cep")) record.Cep = worksheet.Cells[row, headers["cep"]].Text;

                    if (headers.ContainsKey("usocontinuo"))
                    {
                        var usoContinuoCell = worksheet.Cells[row, headers["usocontinuo"]];
                        record.UsoContinuo = usoContinuoCell.GetValue<bool>() ||
                                            usoContinuoCell.Text.Equals("sim", StringComparison.OrdinalIgnoreCase) ||
                                            usoContinuoCell.Text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                            usoContinuoCell.Text == "1";
                    }

                    if (headers.ContainsKey("medicamentoscontinuos")) record.MedicamentosContinuos = worksheet.Cells[row, headers["medicamentoscontinuos"]].Text;
                    if (headers.ContainsKey("alergias")) record.Alergias = worksheet.Cells[row, headers["alergias"]].Text;
                    if (headers.ContainsKey("observacoessaude")) record.ObservacoesSaude = worksheet.Cells[row, headers["observacoessaude"]].Text;
                    if (headers.ContainsKey("observacoesgerais")) record.ObservacoesGerais = worksheet.Cells[row, headers["observacoesgerais"]].Text;
                    if (headers.ContainsKey("tags")) record.Tags = worksheet.Cells[row, headers["tags"]].Text;

                    if (headers.ContainsKey("aceitamarketing"))
                    {
                        var aceitaMarketingCell = worksheet.Cells[row, headers["aceitamarketing"]];
                        record.AceitaMarketing = !aceitaMarketingCell.Text.Equals("não", StringComparison.OrdinalIgnoreCase) &&
                                                !aceitaMarketingCell.Text.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                                                !aceitaMarketingCell.Text.Equals("0", StringComparison.OrdinalIgnoreCase);
                    }

                    if (headers.ContainsKey("aceitanotificacoes"))
                    {
                        var aceitaNotificacoesCell = worksheet.Cells[row, headers["aceitanotificacoes"]];
                        record.AceitaNotificacoes = !aceitaNotificacoesCell.Text.Equals("não", StringComparison.OrdinalIgnoreCase) &&
                                                   !aceitaNotificacoesCell.Text.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                                                   !aceitaNotificacoesCell.Text.Equals("0", StringComparison.OrdinalIgnoreCase);
                    }

                    if (headers.ContainsKey("preferenciascontato")) record.PreferenciasContato = worksheet.Cells[row, headers["preferenciascontato"]].Text;

                    records.Add(record);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Linha {row}: Erro ao processar - {ex.Message}");
                    result.FailedRows++;
                }
            }

            result.TotalRows = records.Count;

            // Processar em lotes
            var batchSize = 100;
            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize).ToList();
                await ProcessImportBatchAsync(farmaciaIdInt, batch, result);
            }

            result.Success = result.ImportedRows > 0;

            _logger.LogInformation("Importação Excel concluída: {Importados} de {Total} clientes importados com sucesso",
                result.ImportedRows, result.TotalRows);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Erro geral na importação Excel: {ex.Message}");
            _logger.LogError(ex, "Erro na importação Excel");
        }

        return result;
    }

    public async Task<ValidationResult> ValidateClienteData(ClienteCsvImport record)
    {
        var result = new ValidationResult { IsValid = true };

        // Validações obrigatórias
        if (string.IsNullOrEmpty(record.Nome) && string.IsNullOrEmpty(record.WhatsAppId))
        {
            result.IsValid = false;
            result.Errors.Add("Nome ou WhatsAppId é obrigatório");
        }

        // Validar WhatsAppId (se fornecido)
        if (!string.IsNullOrEmpty(record.WhatsAppId))
        {
            var cleanWhatsApp = CleanPhoneNumber(record.WhatsAppId);
            if (cleanWhatsApp.Length < 10)
            {
                result.IsValid = false;
                result.Errors.Add($"WhatsAppId inválido: {record.WhatsAppId}");
            }
        }

        // Validar CPF (se fornecido)
        if (!string.IsNullOrEmpty(record.Cpf))
        {
            if (!IsValidCpf(record.Cpf))
            {
                result.IsValid = false;
                result.Errors.Add($"CPF inválido: {record.Cpf}");
            }
        }

        // Validar email (se fornecido)
        if (!string.IsNullOrEmpty(record.Email))
        {
            try
            {
                var email = new System.Net.Mail.MailAddress(record.Email);
                if (email.Address != record.Email)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Email inválido: {record.Email}");
                }
            }
            catch
            {
                result.IsValid = false;
                result.Errors.Add($"Email inválido: {record.Email}");
            }
        }

        // Validar data de nascimento (se fornecida)
        if (record.DataNascimento.HasValue)
        {
            if (record.DataNascimento.Value > DateTime.Now)
            {
                result.IsValid = false;
                result.Errors.Add($"Data de nascimento futura: {record.DataNascimento.Value}");
            }

            if (record.DataNascimento.Value < DateTime.Now.AddYears(-120))
            {
                result.IsValid = false;
                result.Errors.Add($"Data de nascimento muito antiga: {record.DataNascimento.Value}");
            }
        }

        return result;
    }

    public async Task<Cliente> MapToClienteEntity(int farmaciaId, ClienteCsvImport record)
    {
        var farmacia = await _context.Farmacias.FindAsync(farmaciaId);
        if (farmacia == null)
        {
            throw new InvalidOperationException($"Farmácia com ID {farmaciaId} não encontrada.");
        }

        var cliente = new Cliente
        {
            FarmaciaId = farmaciaId,
            Farmacia = farmacia,
            Nome = record.Nome?.Trim(),
            Sobrenome = record.Sobrenome?.Trim(),
            WhatsAppId = CleanPhoneNumber(record.WhatsAppId ?? ""),
            Email = record.Email?.Trim(),
            Telefone = CleanPhoneNumber(record.Telefone ?? ""),
            Cpf = CleanCpf(record.Cpf ?? ""),
            DataNascimento = record.DataNascimento,
            Endereco = record.Endereco?.Trim(),
            Cidade = record.Cidade?.Trim(),
            Estado = record.Estado?.Trim(),
            Cep = CleanCep(record.Cep ?? ""),
            UsoContinuo = record.UsoContinuo,
            MedicamentosContinuos = record.MedicamentosContinuos,
            Alergias = record.Alergias,
            ObservacoesSaude = record.ObservacoesSaude,
            ObservacoesGerais = record.ObservacoesGerais,
            Status = ClienteStatus.Ativo,
            Tipo = ClienteTipo.PessoaFisica,
            Segmento = "Importado",
            OrigemCadastro = "Importação",
            DataCadastro = DateTime.UtcNow,
            UltimoContato = DateTime.UtcNow,
            CriadoPor = "Importação",
            AceitaMarketing = record.AceitaMarketing,
            AceitaNotificacoes = record.AceitaNotificacoes,
            PreferenciasContato = record.PreferenciasContato ?? "WhatsApp"
        };

        // Gerar código do cliente
        cliente.CodigoCliente = await GenerateClientCodeAsync(farmaciaId);

        return cliente;
    }

    private async Task ProcessImportBatchAsync(int farmaciaId, List<ClienteCsvImport> batch, ImportResult result)
    {
        foreach (var record in batch)
        {
            try
            {
                // Validar registro
                var validation = await ValidateClienteData(record);
                if (!validation.IsValid)
                {
                    result.Errors.AddRange(validation.Errors);
                    result.FailedRows++;
                    continue;
                }

                // Verificar duplicatas
                var existingClient = await FindExistingClientAsync(farmaciaId, record);
                if (existingClient != null)
                {
                    // Atualizar cliente existente
                    await UpdateExistingClientAsync(existingClient, record);
                    result.ImportedClientes.Add(existingClient);
                    result.ImportedRows++;
                    continue;
                }

                // Criar novo cliente
                var newCliente = await MapToClienteEntity(farmaciaId, record);

                // Adicionar tags
                if (!string.IsNullOrEmpty(record.Tags))
                {
                    var tags = record.Tags.Split(';', ',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in tags.Select(t => t.Trim()).Distinct())
                    {
                        if (!string.IsNullOrEmpty(tag))
                        {
                            newCliente.Tags.Add(new ClienteTag
                            {
                                Cliente = newCliente,
                                Tag = tag,
                                Cor = GetTagColor(tag),
                                DataAdicao = DateTime.UtcNow,
                                AdicionadoPor = "Importação"
                            });
                        }
                    }
                }

                await _context.Clientes.AddAsync(newCliente);
                result.ImportedClientes.Add(newCliente);
                result.ImportedRows++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Erro ao processar registro: {ex.Message}");
                result.FailedRows++;
                _logger.LogError(ex, "Erro ao processar registro de importação");
            }
        }

        // Salvar batch
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Erro ao salvar batch: {ex.Message}");
            _logger.LogError(ex, "Erro ao salvar batch de importação");
        }
    }

    private async Task<Cliente> FindExistingClientAsync(int farmaciaId, ClienteCsvImport record)
    {
        var query = _context.Clientes.Where(c => c.FarmaciaId == farmaciaId);

        // Tentar encontrar por WhatsAppId
        if (!string.IsNullOrEmpty(record.WhatsAppId))
        {
            var cleanWhatsApp = CleanPhoneNumber(record.WhatsAppId);
            var byWhatsApp = await query.FirstOrDefaultAsync(c => c.WhatsAppId == cleanWhatsApp);
            if (byWhatsApp != null) return byWhatsApp;
        }

        // Tentar encontrar por CPF
        if (!string.IsNullOrEmpty(record.Cpf))
        {
            var cleanCpf = CleanCpf(record.Cpf);
            var byCpf = await query.FirstOrDefaultAsync(c => c.Cpf == cleanCpf);
            if (byCpf != null) return byCpf;
        }

        // Tentar encontrar por email
        if (!string.IsNullOrEmpty(record.Email))
        {
            var byEmail = await query.FirstOrDefaultAsync(c => c.Email == record.Email);
            if (byEmail != null) return byEmail;
        }

        // Tentar encontrar por nome e telefone
        if (!string.IsNullOrEmpty(record.Nome) && !string.IsNullOrEmpty(record.Telefone))
        {
            var cleanTelefone = CleanPhoneNumber(record.Telefone);
            var byNamePhone = await query.FirstOrDefaultAsync(c =>
                c.Nome.Contains(record.Nome) && c.Telefone == cleanTelefone);
            if (byNamePhone != null) return byNamePhone;
        }

        return null;
    }

    private async Task UpdateExistingClientAsync(Cliente cliente, ClienteCsvImport record)
    {
        // Atualizar apenas campos vazios ou que precisam ser completados
        if (string.IsNullOrEmpty(cliente.Nome) && !string.IsNullOrEmpty(record.Nome))
            cliente.Nome = record.Nome;

        if (string.IsNullOrEmpty(cliente.Sobrenome) && !string.IsNullOrEmpty(record.Sobrenome))
            cliente.Sobrenome = record.Sobrenome;

        if (string.IsNullOrEmpty(cliente.Email) && !string.IsNullOrEmpty(record.Email))
            cliente.Email = record.Email;

        if (string.IsNullOrEmpty(cliente.Telefone) && !string.IsNullOrEmpty(record.Telefone))
            cliente.Telefone = CleanPhoneNumber(record.Telefone);

        if (string.IsNullOrEmpty(cliente.Cpf) && !string.IsNullOrEmpty(record.Cpf))
            cliente.Cpf = CleanCpf(record.Cpf);

        if (!cliente.DataNascimento.HasValue && record.DataNascimento.HasValue)
            cliente.DataNascimento = record.DataNascimento;

        if (string.IsNullOrEmpty(cliente.Endereco) && !string.IsNullOrEmpty(record.Endereco))
            cliente.Endereco = record.Endereco;

        if (string.IsNullOrEmpty(cliente.Cidade) && !string.IsNullOrEmpty(record.Cidade))
            cliente.Cidade = record.Cidade;

        if (string.IsNullOrEmpty(cliente.Estado) && !string.IsNullOrEmpty(record.Estado))
            cliente.Estado = record.Estado;

        if (string.IsNullOrEmpty(cliente.Cep) && !string.IsNullOrEmpty(record.Cep))
            cliente.Cep = CleanCep(record.Cep);

        // Atualizar informações de saúde
        if (!cliente.UsoContinuo && record.UsoContinuo)
            cliente.UsoContinuo = true;

        if (string.IsNullOrEmpty(cliente.MedicamentosContinuos) && !string.IsNullOrEmpty(record.MedicamentosContinuos))
            cliente.MedicamentosContinuos = record.MedicamentosContinuos;

        if (string.IsNullOrEmpty(cliente.Alergias) && !string.IsNullOrEmpty(record.Alergias))
            cliente.Alergias = record.Alergias;

        if (string.IsNullOrEmpty(cliente.ObservacoesSaude) && !string.IsNullOrEmpty(record.ObservacoesSaude))
            cliente.ObservacoesSaude = record.ObservacoesSaude;

        // Adicionar tags se especificado
        if (!string.IsNullOrEmpty(record.Tags))
        {
            var existingTags = cliente.Tags.Select(t => t.Tag.ToLower()).ToList();
            var newTags = record.Tags.Split(';', ',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Distinct()
                .Where(t => !existingTags.Contains(t.ToLower()));

            foreach (var tag in newTags)
            {
                cliente.Tags.Add(new ClienteTag
                {
                    Cliente = null!,
                    Tag = tag,
                    Cor = GetTagColor(tag),
                    DataAdicao = DateTime.UtcNow,
                    AdicionadoPor = "Importação"
                });
            }
        }

        cliente.AtualizadoEm = DateTime.UtcNow;
        cliente.AtualizadoPor = "Importação";

        _context.Clientes.Update(cliente);
    }

    private async Task<string> GenerateClientCodeAsync(int farmaciaId)
    {
        var lastCode = await _context.Clientes
            .Where(c => c.FarmaciaId == farmaciaId && c.CodigoCliente != null)
            .OrderByDescending(c => c.CodigoCliente)
            .Select(c => c.CodigoCliente)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(lastCode))
            return "CL-001";

        if (lastCode.StartsWith("CL-") && int.TryParse(lastCode.Substring(3), out int lastNumber))
        {
            return $"CL-{(lastNumber + 1):000}";
        }

        // Se o formato não for o esperado, buscar o maior número
        var maxNumber = await _context.Clientes
            .Where(c => c.FarmaciaId == farmaciaId && c.CodigoCliente != null)
            .Select(c => c.CodigoCliente)
            .ToListAsync();

        var numbers = maxNumber
            .Where(c => c.StartsWith("CL-"))
            .Select(c => int.TryParse(c.Substring(3), out int n) ? n : 0)
            .ToList();

        var nextNumber = numbers.Any() ? numbers.Max() + 1 : 1;
        return $"CL-{nextNumber:000}";
    }

    // Métodos auxiliares
    private string DetectDelimiter(StreamReader reader)
    {
        var firstLine = reader.ReadLine();
        reader.BaseStream.Position = 0;
        reader.DiscardBufferedData();

        if (firstLine.Contains(';')) return ";";
        if (firstLine.Contains(',')) return ",";
        if (firstLine.Contains('\t')) return "\t";

        return ","; // Default
    }

    private string CleanPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private string CleanCpf(string cpf)
    {
        if (string.IsNullOrEmpty(cpf)) return "";
        return new string(cpf.Where(char.IsDigit).ToArray());
    }

    private string CleanCep(string cep)
    {
        if (string.IsNullOrEmpty(cep)) return "";
        return new string(cep.Where(char.IsDigit).ToArray());
    }

    private bool IsValidCpf(string cpf)
    {
        cpf = CleanCpf(cpf);

        if (cpf.Length != 11)
            return false;

        // Verificar se todos os dígitos são iguais
        if (cpf.Distinct().Count() == 1)
            return false;

        // Validar dígitos verificadores
        int[] multiplicador1 = new int[9] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multiplicador2 = new int[10] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        string tempCpf = cpf.Substring(0, 9);
        int soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        int resto = soma % 11;
        if (resto < 2)
            resto = 0;
        else
            resto = 11 - resto;

        string digito = resto.ToString();
        tempCpf = tempCpf + digito;

        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        if (resto < 2)
            resto = 0;
        else
            resto = 11 - resto;

        digito = digito + resto.ToString();

        return cpf.EndsWith(digito);
    }

    private string GetTagColor(string tag)
    {
        // Mapear cores padrão para tags comuns
        var tagLower = tag.ToLower();

        return tagLower switch
        {
            var t when t.Contains("diabet") => "#d13438",
            var t when t.Contains("hipertens") => "#0078d4",
            var t when t.Contains("gestant") => "#e3008c",
            var t when t.Contains("idos") => "#8661c5",
            var t when t.Contains("alerg") => "#ff8c00",
            var t when t.Contains("crônic") => "#107c10",
            var t when t.Contains("vip") => "#e6b700",
            var t when t.Contains("fidel") => "#0099bc",
            _ => GetRandomColor(tag)
        };
    }

    private string GetRandomColor(string seed)
    {
        var colors = new[]
        {
            "#0078d4", "#107c10", "#e3008c", "#d13438",
            "#ff8c00", "#8661c5", "#0099bc", "#e6b700",
            "#744da9", "#038387", "#4f6bed", "#c239b3"
        };

        var hash = seed.GetHashCode();
        var index = Math.Abs(hash) % colors.Length;
        return colors[index];
    }
}

