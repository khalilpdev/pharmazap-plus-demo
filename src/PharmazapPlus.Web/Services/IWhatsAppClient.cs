using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PharmazapPlus.Shared.Models;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Interface para gerenciar clientes WhatsApp
    /// </summary>
    public interface IWhatsAppClient
    {
        Task<List<Mensagem>> GetMessagesAsync(int atendimentoId);
    }

}
