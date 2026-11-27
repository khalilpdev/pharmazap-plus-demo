using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public interface IClienteImporter
{
    Task<ImportResult> ImportFromCsvAsync(string farmaciaId, Stream csvStream);
    Task<ImportResult> ImportFromExcelAsync(string farmaciaId, Stream excelStream);
    Task<ValidationResult> ValidateClienteData(ClienteCsvImport record);
    Task<Cliente> MapToClienteEntity(int farmaciaId, ClienteCsvImport record);
}
