using CsvHelper.Configuration;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public sealed class ClienteCsvImportMap : ClassMap<ClienteCsvImport>
{
    public ClienteCsvImportMap()
    {
        Map(m => m.Nome).Name("nome", "Nome", "NOME", "name", "Name");
        Map(m => m.Sobrenome).Name("sobrenome", "Sobrenome", "SOBRENOME", "lastname", "LastName");
        Map(m => m.WhatsAppId).Name("whatsappid", "WhatsAppId", "WHATSAPPID", "whatsapp", "WhatsApp");
        Map(m => m.Email).Name("email", "Email", "EMAIL", "e-mail");
        Map(m => m.Telefone).Name("telefone", "Telefone", "TELEFONE", "phone", "Phone");
        Map(m => m.Cpf).Name("cpf", "Cpf", "CPF", "documento", "Documento");
        Map(m => m.DataNascimento).Name("datanascimento", "DataNascimento", "DATANASCIMENTO", "nascimento", "birthdate");
        Map(m => m.Endereco).Name("endereco", "Endereco", "ENDERECO", "address", "Address");
        Map(m => m.Cidade).Name("cidade", "Cidade", "CIDADE", "city", "City");
        Map(m => m.Estado).Name("estado", "Estado", "ESTADO", "state", "State");
        Map(m => m.Cep).Name("cep", "Cep", "CEP", "zipcode", "ZipCode");
        Map(m => m.UsoContinuo).Name("usocontinuo", "UsoContinuo", "USOCONTINUO", "uso_continuo");
        Map(m => m.MedicamentosContinuos).Name("medicamentoscontinuos", "MedicamentosContinuos", "MEDICAMENTOSCONTINUOS");
        Map(m => m.Alergias).Name("alergias", "Alergias", "ALERGIAS", "allergies");
        Map(m => m.ObservacoesSaude).Name("observacoessaude", "ObservacoesSaude", "OBSERVACOESSAUDE");
        Map(m => m.ObservacoesGerais).Name("observacoesgerais", "ObservacoesGerais", "OBSERVACOESGERAIS");
        Map(m => m.AceitaMarketing).Name("aceitamarketing", "AceitaMarketing", "ACEITAMARKETING");
        Map(m => m.AceitaNotificacoes).Name("aceitanotificacoes", "AceitaNotificacoes", "ACEITANOTIFICACOES");
        Map(m => m.PreferenciasContato).Name("preferenciascontato", "PreferenciasContato", "PREFERENCIASCONTATO");
        Map(m => m.Tags).Name("tags", "Tags", "TAGS", "tag", "Tag");
    }
}
