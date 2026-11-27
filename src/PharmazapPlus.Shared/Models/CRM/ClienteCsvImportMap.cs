using CsvHelper.Configuration;

namespace PharmazapPlus.Shared.Models.CRM;

public sealed class ClienteCsvImportMap : ClassMap<ClienteCsvImport>
{
    public ClienteCsvImportMap()
    {
        // Mapeamento automático baseado nos atributos [Name]
        AutoMap(System.Globalization.CultureInfo.InvariantCulture);

        // Configurações adicionais
        Map(m => m.DataNascimento).TypeConverterOption.Format("dd/MM/yyyy")
                                   .TypeConverterOption.DateTimeStyles(System.Globalization.DateTimeStyles.None);

        // Ignorar propriedades calculadas
        Map(m => m.NomeCompleto).Ignore();
        Map(m => m.IsValid).Ignore();
        Map(m => m.ValidationErrors).Ignore();
    }
}