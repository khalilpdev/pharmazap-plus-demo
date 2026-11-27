namespace PharmazapPlus.API.Services.CRM;

public interface IClienteExporter
{
    Task<Stream> ExportToCsvAsync(string farmaciaId, ClienteExportFilter filter);
    Task<Stream> ExportToExcelAsync(string farmaciaId, ClienteExportFilter filter);
    Task<Stream> ExportToPdfAsync(string farmaciaId, ClienteExportFilter filter);
    Task<byte[]> GenerateClienteReportAsync(int clienteId, string farmaciaId);
}
