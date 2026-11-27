using System.Collections.Generic;
using System.Threading.Tasks;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Implementação do cliente de acesso a mensagens via API PharmazapPlus.
    /// </summary>
    public class WhatsAppClient : IWhatsAppClient
    {
        private readonly IPharmazapApiService _apiService;

        public WhatsAppClient(IPharmazapApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<Mensagem>> GetMessagesAsync(int atendimentoId)
        {
            return await _apiService.GetMensagensAtendimentoAsync(atendimentoId);
        }
    }
}
