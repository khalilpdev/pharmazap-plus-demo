using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public class ClienteExporter : IClienteExporter
{
    private readonly PharmazapDbContext _context;
    private readonly ILogger<ClienteExporter> _logger;

    public ClienteExporter(
        PharmazapDbContext context,
        ILogger<ClienteExporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Stream> ExportToCsvAsync(string farmaciaId, ClienteExportFilter filter)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, Encoding.UTF8);
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            Encoding = Encoding.UTF8
        });

        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var clientes = await GetClientesForExport(farmaciaIdInt, filter);

            // Escrever cabeçalho
            csv.WriteField("Código");
            csv.WriteField("Nome");
            csv.WriteField("Sobrenome");
            csv.WriteField("WhatsApp");
            csv.WriteField("Email");
            csv.WriteField("Telefone");
            csv.WriteField("CPF");
            csv.WriteField("Data Nascimento");
            csv.WriteField("Endereço");
            csv.WriteField("Cidade");
            csv.WriteField("Estado");
            csv.WriteField("CEP");
            csv.WriteField("Uso Contínuo");
            csv.WriteField("Medicamentos Contínuos");
            csv.WriteField("Alergias");
            csv.WriteField("Observações Saúde");
            csv.WriteField("Status");
            csv.WriteField("Segmento");
            csv.WriteField("Data Cadastro");
            csv.WriteField("Último Contato");
            csv.WriteField("Total Atendimentos");
            csv.WriteField("Total Pedidos");
            csv.WriteField("Total Gasto");
            csv.WriteField("Tags");
            csv.NextRecord();

            // Escrever dados
            foreach (var cliente in clientes)
            {
                csv.WriteField(cliente.CodigoCliente);
                csv.WriteField(cliente.Nome);
                csv.WriteField(cliente.Sobrenome);
                csv.WriteField(cliente.WhatsAppId);
                csv.WriteField(cliente.Email);
                csv.WriteField(cliente.Telefone);
                csv.WriteField(cliente.Cpf);
                csv.WriteField(cliente.DataNascimento?.ToString("dd/MM/yyyy"));
                csv.WriteField(cliente.Endereco);
                csv.WriteField(cliente.Cidade);
                csv.WriteField(cliente.Estado);
                csv.WriteField(cliente.Cep);
                csv.WriteField(cliente.UsoContinuo ? "Sim" : "Não");
                csv.WriteField(cliente.MedicamentosContinuos);
                csv.WriteField(cliente.Alergias);
                csv.WriteField(cliente.ObservacoesSaude);
                csv.WriteField(cliente.Status.ToString());
                csv.WriteField(cliente.Segmento);
                csv.WriteField(cliente.DataCadastro.ToString("dd/MM/yyyy HH:mm"));
                csv.WriteField(cliente.UltimoContato?.ToString("dd/MM/yyyy HH:mm"));
                csv.WriteField(cliente.TotalAtendimentos);
                csv.WriteField(cliente.TotalPedidos);
                csv.WriteField(cliente.TotalGasto.ToString("N2"));
                csv.WriteField(string.Join("; ", cliente.Tags.Select(t => t.Tag)));
                csv.NextRecord();
            }

            await writer.FlushAsync();
            stream.Position = 0;

            _logger.LogInformation("Exportação CSV concluída: {Count} clientes exportados", clientes.Count);

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar clientes para CSV");
            throw;
        }
    }

    public async Task<Stream> ExportToExcelAsync(string farmaciaId, ClienteExportFilter filter)
    {
        var stream = new MemoryStream();

        using (var package = new ExcelPackage(stream))
        {
            try
            {
                if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                    throw new ArgumentException("ID da farmácia inválido");

                var clientes = await GetClientesForExport(farmaciaIdInt, filter);
                var farmacia = await _context.Farmacias.FindAsync(farmaciaIdInt);

                // Criar worksheet principal
                var worksheet = package.Workbook.Worksheets.Add("Clientes");

                // Cabeçalho
                var headers = new[]
                {
                    "Código", "Nome", "Sobrenome", "WhatsApp", "Email", "Telefone", "CPF",
                    "Data Nascimento", "Endereço", "Cidade", "Estado", "CEP", "Uso Contínuo",
                    "Medicamentos Contínuos", "Alergias", "Observações Saúde", "Status",
                    "Segmento", "Data Cadastro", "Último Contato", "Total Atendimentos",
                    "Total Pedidos", "Total Gasto", "Tags"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    worksheet.Cells[1, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // Dados
                int row = 2;
                foreach (var cliente in clientes)
                {
                    worksheet.Cells[row, 1].Value = cliente.CodigoCliente;
                    worksheet.Cells[row, 2].Value = cliente.Nome;
                    worksheet.Cells[row, 3].Value = cliente.Sobrenome;
                    worksheet.Cells[row, 4].Value = cliente.WhatsAppId;
                    worksheet.Cells[row, 5].Value = cliente.Email;
                    worksheet.Cells[row, 6].Value = cliente.Telefone;
                    worksheet.Cells[row, 7].Value = cliente.Cpf;
                    worksheet.Cells[row, 8].Value = cliente.DataNascimento?.ToString("dd/MM/yyyy");
                    worksheet.Cells[row, 8].Style.Numberformat.Format = "dd/MM/yyyy";
                    worksheet.Cells[row, 9].Value = cliente.Endereco;
                    worksheet.Cells[row, 10].Value = cliente.Cidade;
                    worksheet.Cells[row, 11].Value = cliente.Estado;
                    worksheet.Cells[row, 12].Value = cliente.Cep;
                    worksheet.Cells[row, 13].Value = cliente.UsoContinuo ? "Sim" : "Não";
                    worksheet.Cells[row, 14].Value = cliente.MedicamentosContinuos;
                    worksheet.Cells[row, 15].Value = cliente.Alergias;
                    worksheet.Cells[row, 16].Value = cliente.ObservacoesSaude;
                    worksheet.Cells[row, 17].Value = cliente.Status.ToString();
                    worksheet.Cells[row, 18].Value = cliente.Segmento;
                    worksheet.Cells[row, 19].Value = cliente.DataCadastro;
                    worksheet.Cells[row, 19].Style.Numberformat.Format = "dd/MM/yyyy HH:mm";
                    worksheet.Cells[row, 20].Value = cliente.UltimoContato;
                    worksheet.Cells[row, 20].Style.Numberformat.Format = "dd/MM/yyyy HH:mm";
                    worksheet.Cells[row, 21].Value = cliente.TotalAtendimentos;
                    worksheet.Cells[row, 22].Value = cliente.TotalPedidos;
                    worksheet.Cells[row, 23].Value = cliente.TotalGasto;
                    worksheet.Cells[row, 23].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 24].Value = string.Join("; ", cliente.Tags.Select(t => t.Tag));

                    // Formatar linha
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }

                    row++;
                }

                // Auto ajustar colunas
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Adicionar worksheet de estatísticas
                AddStatisticsWorksheet(package, clientes, farmacia?.Nome);

                // Adicionar worksheet de segmentos
                AddSegmentsWorksheet(package, clientes);

                package.Save();

                _logger.LogInformation("Exportação Excel concluída: {Count} clientes exportados", clientes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exportar clientes para Excel");
                throw;
            }
        }

        stream.Position = 0;
        return stream;
    }

    public async Task<Stream> ExportToPdfAsync(string farmaciaId, ClienteExportFilter filter)
    {
        // Para PDF, usar uma biblioteca como QuestPDF ou iTextSharp
        // Por enquanto, retornar um placeholder
        throw new NotImplementedException("Exportação PDF será implementada em breve");
    }

    public async Task<byte[]> GenerateClienteReportAsync(int clienteId, string farmaciaId)
    {
        try
        {
            if (!int.TryParse(farmaciaId, out int farmaciaIdInt))
                throw new ArgumentException("ID da farmácia inválido");

            var cliente = await _context.Clientes
                .Include(c => c.Tags)
                .Include(c => c.Atendimentos)
                .Include(c => c.Pedidos)
                .Include(c => c.Observacoes)
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.FarmaciaId == farmaciaIdInt);

            if (cliente == null)
                throw new NotFoundException($"Cliente {clienteId} não encontrado");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add($"Cliente {cliente.CodigoCliente}");

            // Informações do cliente
            worksheet.Cells[1, 1].Value = "Relatório do Cliente";
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;

            worksheet.Cells[3, 1].Value = "Informações Básicas";
            worksheet.Cells[3, 1].Style.Font.Bold = true;

            var basicInfo = new Dictionary<string, string?>
            {
                { "Código", cliente.CodigoCliente },
                { "Nome", cliente.NomeCompleto },
                { "WhatsApp", cliente.WhatsAppId },
                { "Email", cliente.Email },
                { "Telefone", cliente.Telefone },
                { "CPF", cliente.Cpf },
                { "Data Nascimento", cliente.DataNascimento?.ToString("dd/MM/yyyy") },
                { "Idade", cliente.Idade.ToString() },
                { "Endereço", cliente.EnderecoCompleto },
                { "Status", cliente.Status.ToString() },
                { "Segmento", cliente.Segmento },
                { "Data Cadastro", cliente.DataCadastro.ToString("dd/MM/yyyy HH:mm") },
                { "Último Contato", cliente.UltimoContato?.ToString("dd/MM/yyyy HH:mm") }
            };

            int row = 4;
            foreach (var info in basicInfo)
            {
                worksheet.Cells[row, 1].Value = info.Key;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Value = info.Value;
                row++;
            }

            // Informações de saúde
            row += 2;
            worksheet.Cells[row, 1].Value = "Informações de Saúde";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            var healthInfo = new Dictionary<string, string?>
            {
                { "Uso Contínuo", cliente.UsoContinuo ? "Sim" : "Não" },
                { "Medicamentos", cliente.MedicamentosContinuos },
                { "Alergias", cliente.Alergias },
                { "Observações", cliente.ObservacoesSaude },
                { "Tipo Sanguíneo", cliente.TipoSanguineo },
                { "Peso", cliente.Peso?.ToString("N1") + " kg" },
                { "Altura", cliente.Altura?.ToString("N2") + " m" }
            };

            foreach (var info in healthInfo)
            {
                worksheet.Cells[row, 1].Value = info.Key;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Value = info.Value;
                row++;
            }

            // Estatísticas
            row += 2;
            worksheet.Cells[row, 1].Value = "Estatísticas";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            var stats = new Dictionary<string, string?>
            {
                { "Total Atendimentos", cliente.TotalAtendimentos.ToString() },
                { "Total Pedidos", cliente.TotalPedidos.ToString() },
                { "Total Gasto", cliente.TotalGasto.ToString("C2") },
                { "Ticket Médio", cliente.TicketMedio.ToString("C2") },
                { "Última Compra", cliente.UltimaCompra?.ToString("dd/MM/yyyy") },
                { "Pontuação Fidelidade", cliente.PontuacaoFidelidade?.ToString() }
            };

            foreach (var stat in stats)
            {
                worksheet.Cells[row, 1].Value = stat.Key;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Value = stat.Value;
                row++;
            }

            // Tags
            if (cliente.Tags.Any())
            {
                row += 2;
                worksheet.Cells[row, 1].Value = "Tags";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;

                foreach (var tag in cliente.Tags)
                {
                    worksheet.Cells[row, 1].Value = tag.Tag;
                    worksheet.Cells[row, 2].Value = tag.DataAdicao.ToString("dd/MM/yyyy");
                    row++;
                }
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return await package.GetAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar relatório do cliente {ClienteId}", clienteId);
            throw;
        }
    }

    private async Task<List<Cliente>> GetClientesForExport(int farmaciaId, ClienteExportFilter filter)
    {
        var query = _context.Clientes
            .Include(c => c.Tags)
            .Where(c => c.FarmaciaId == farmaciaId)
            .AsQueryable();

        // Aplicar filtros
        if (filter.Status.HasValue)
            query = query.Where(c => c.Status == filter.Status.Value);

        if (filter.Tipo.HasValue)
            query = query.Where(c => c.Tipo == filter.Tipo.Value);

        if (!string.IsNullOrEmpty(filter.Segmento))
            query = query.Where(c => c.Segmento == filter.Segmento);

        if (filter.UsoContinuo.HasValue)
            query = query.Where(c => c.UsoContinuo == filter.UsoContinuo.Value);

        if (filter.DataCadastroInicio.HasValue)
            query = query.Where(c => c.DataCadastro >= filter.DataCadastroInicio.Value);

        if (filter.DataCadastroFim.HasValue)
            query = query.Where(c => c.DataCadastro <= filter.DataCadastroFim.Value);

        if (!filter.IncludeInativos)
            query = query.Where(c => c.Status != ClienteStatus.Inativo);

        return await query
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    private void AddStatisticsWorksheet(ExcelPackage package, List<Cliente> clientes, string? farmaciaNome = null)
    {
        var statsWorksheet = package.Workbook.Worksheets.Add("Estatísticas");

        statsWorksheet.Cells[1, 1].Value = "Relatório Estatístico - Clientes";
        statsWorksheet.Cells[1, 1].Style.Font.Size = 14;
        statsWorksheet.Cells[1, 1].Style.Font.Bold = true;

        if (!string.IsNullOrEmpty(farmaciaNome))
        {
            statsWorksheet.Cells[2, 1].Value = $"Farmácia: {farmaciaNome}";
        }

        statsWorksheet.Cells[2, 3].Value = $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}";

        // Estatísticas básicas
        int row = 4;
        statsWorksheet.Cells[row, 1].Value = "Estatísticas Gerais";
        statsWorksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        var totalClientes = clientes.Count;
        var clientesAtivos = clientes.Count(c => c.Status == ClienteStatus.Ativo);
        var clientesInativos = clientes.Count(c => c.Status == ClienteStatus.Inativo);
        var clientesUsoContinuo = clientes.Count(c => c.UsoContinuo);
        var totalAtendimentos = clientes.Sum(c => c.TotalAtendimentos);
        var totalPedidos = clientes.Sum(c => c.TotalPedidos);
        var totalGasto = clientes.Sum(c => c.TotalGasto);
        var ticketMedio = totalPedidos > 0 ? totalGasto / totalPedidos : 0;

        var stats = new Dictionary<string, object>
        {
            { "Total de Clientes", totalClientes },
            { "Clientes Ativos", clientesAtivos },
            { "Clientes Inativos", clientesInativos },
            { "Clientes com Uso Contínuo", clientesUsoContinuo },
            { "Total de Atendimentos", totalAtendimentos },
            { "Total de Pedidos", totalPedidos },
            { "Faturamento Total", totalGasto },
            { "Ticket Médio", ticketMedio }
        };

        foreach (var stat in stats)
        {
            statsWorksheet.Cells[row, 1].Value = stat.Key;
            statsWorksheet.Cells[row, 1].Style.Font.Bold = true;
            statsWorksheet.Cells[row, 2].Value = stat.Value;

            if (stat.Key.Contains("Faturamento") || stat.Key.Contains("Ticket"))
            {
                statsWorksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";
            }

            row++;
        }

        // Distribuição por segmento
        row += 2;
        statsWorksheet.Cells[row, 1].Value = "Distribuição por Segmento";
        statsWorksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        var segmentos = clientes
            .GroupBy(c => c.Segmento ?? "Sem Segmento")
            .Select(g => new
            {
                Segmento = g.Key,
                Quantidade = g.Count(),
                Percentual = (decimal)g.Count() / totalClientes * 100
            })
            .OrderByDescending(s => s.Quantidade)
            .ToList();

        statsWorksheet.Cells[row, 1].Value = "Segmento";
        statsWorksheet.Cells[row, 2].Value = "Quantidade";
        statsWorksheet.Cells[row, 3].Value = "Percentual";
        statsWorksheet.Cells[row, 1, row, 3].Style.Font.Bold = true;
        statsWorksheet.Cells[row, 1, row, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
        statsWorksheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        row++;

        foreach (var segmento in segmentos)
        {
            statsWorksheet.Cells[row, 1].Value = segmento.Segmento;
            statsWorksheet.Cells[row, 2].Value = segmento.Quantidade;
            statsWorksheet.Cells[row, 3].Value = segmento.Percentual;
            statsWorksheet.Cells[row, 3].Style.Numberformat.Format = "0.00%";
            row++;
        }

        // Top 10 clientes por faturamento
        row += 2;
        statsWorksheet.Cells[row, 1].Value = "Top 10 Clientes por Faturamento";
        statsWorksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        var topClientes = clientes
            .Where(c => c.TotalGasto > 0)
            .OrderByDescending(c => c.TotalGasto)
            .Take(10)
            .ToList();

        statsWorksheet.Cells[row, 1].Value = "Cliente";
        statsWorksheet.Cells[row, 2].Value = "WhatsApp";
        statsWorksheet.Cells[row, 3].Value = "Segmento";
        statsWorksheet.Cells[row, 4].Value = "Pedidos";
        statsWorksheet.Cells[row, 5].Value = "Faturamento";
        statsWorksheet.Cells[row, 1, row, 5].Style.Font.Bold = true;
        statsWorksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
        statsWorksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        row++;

        foreach (var cliente in topClientes)
        {
            statsWorksheet.Cells[row, 1].Value = cliente.NomeCompleto;
            statsWorksheet.Cells[row, 2].Value = cliente.WhatsAppId;
            statsWorksheet.Cells[row, 3].Value = cliente.Segmento;
            statsWorksheet.Cells[row, 4].Value = cliente.TotalPedidos;
            statsWorksheet.Cells[row, 5].Value = cliente.TotalGasto;
            statsWorksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            row++;
        }

        statsWorksheet.Cells[statsWorksheet.Dimension.Address].AutoFitColumns();
    }

    private void AddSegmentsWorksheet(ExcelPackage package, List<Cliente> clientes)
    {
        var segmentsWorksheet = package.Workbook.Worksheets.Add("Segmentos");

        segmentsWorksheet.Cells[1, 1].Value = "Análise por Segmento";
        segmentsWorksheet.Cells[1, 1].Style.Font.Size = 14;
        segmentsWorksheet.Cells[1, 1].Style.Font.Bold = true;

        // Agrupar por segmento e calcular métricas
        var segmentAnalysis = clientes
            .GroupBy(c => c.Segmento ?? "Sem Segmento")
            .Select(g => new
            {
                Segmento = g.Key,
                Quantidade = g.Count(),
                ClientesAtivos = g.Count(c => c.Status == ClienteStatus.Ativo),
                UsoContinuo = g.Count(c => c.UsoContinuo),
                TotalAtendimentos = g.Sum(c => c.TotalAtendimentos),
                TotalPedidos = g.Sum(c => c.TotalPedidos),
                TotalGasto = g.Sum(c => c.TotalGasto),
                TicketMedio = g.Sum(c => c.TotalPedidos) > 0 ?
                    g.Sum(c => c.TotalGasto) / g.Sum(c => c.TotalPedidos) : 0,
                AtendimentosPorCliente = g.Average(c => c.TotalAtendimentos),
                PedidosPorCliente = g.Average(c => c.TotalPedidos)
            })
            .OrderByDescending(s => s.TotalGasto)
            .ToList();

        int row = 3;

        // Cabeçalho
        var headers = new[]
        {
            "Segmento", "Quantidade", "Ativos", "Uso Contínuo", "Atendimentos",
            "Pedidos", "Faturamento", "Ticket Médio", "Atend./Cliente", "Pedidos/Cliente"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            segmentsWorksheet.Cells[row, i + 1].Value = headers[i];
            segmentsWorksheet.Cells[row, i + 1].Style.Font.Bold = true;
            segmentsWorksheet.Cells[row, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            segmentsWorksheet.Cells[row, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            segmentsWorksheet.Cells[row, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        row++;

        // Dados
        foreach (var segment in segmentAnalysis)
        {
            segmentsWorksheet.Cells[row, 1].Value = segment.Segmento;
            segmentsWorksheet.Cells[row, 2].Value = segment.Quantidade;
            segmentsWorksheet.Cells[row, 3].Value = segment.ClientesAtivos;
            segmentsWorksheet.Cells[row, 4].Value = segment.UsoContinuo;
            segmentsWorksheet.Cells[row, 5].Value = segment.TotalAtendimentos;
            segmentsWorksheet.Cells[row, 6].Value = segment.TotalPedidos;
            segmentsWorksheet.Cells[row, 7].Value = segment.TotalGasto;
            segmentsWorksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
            segmentsWorksheet.Cells[row, 8].Value = segment.TicketMedio;
            segmentsWorksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
            segmentsWorksheet.Cells[row, 9].Value = segment.AtendimentosPorCliente;
            segmentsWorksheet.Cells[row, 9].Style.Numberformat.Format = "0.00";
            segmentsWorksheet.Cells[row, 10].Value = segment.PedidosPorCliente;
            segmentsWorksheet.Cells[row, 10].Style.Numberformat.Format = "0.00";

            // Formatar linha
            for (int col = 1; col <= headers.Length; col++)
            {
                segmentsWorksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            row++;
        }

        // Totais
        segmentsWorksheet.Cells[row, 1].Value = "TOTAL";
        segmentsWorksheet.Cells[row, 1].Style.Font.Bold = true;
        segmentsWorksheet.Cells[row, 2].Value = segmentAnalysis.Sum(s => s.Quantidade);
        segmentsWorksheet.Cells[row, 3].Value = segmentAnalysis.Sum(s => s.ClientesAtivos);
        segmentsWorksheet.Cells[row, 4].Value = segmentAnalysis.Sum(s => s.UsoContinuo);
        segmentsWorksheet.Cells[row, 5].Value = segmentAnalysis.Sum(s => s.TotalAtendimentos);
        segmentsWorksheet.Cells[row, 6].Value = segmentAnalysis.Sum(s => s.TotalPedidos);
        segmentsWorksheet.Cells[row, 7].Value = segmentAnalysis.Sum(s => s.TotalGasto);
        segmentsWorksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
        segmentsWorksheet.Cells[row, 7].Style.Font.Bold = true;

        for (int col = 1; col <= headers.Length; col++)
        {
            segmentsWorksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            segmentsWorksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            segmentsWorksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        segmentsWorksheet.Cells[segmentsWorksheet.Dimension.Address].AutoFitColumns();
    }
}