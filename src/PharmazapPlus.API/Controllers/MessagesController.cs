using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.Shared;

namespace PharmazapPlus.API.Controllers
{
    [ApiController]
    [Route("api/messages")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageQueue _messageQueue;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(IMessageQueue messageQueue, ILogger<MessagesController> logger)
        {
            _messageQueue = messageQueue;
            _logger = logger;
        }

        /// <summary>
        /// Retorna as contagens atuais das filas Redis para monitoramento.
        /// </summary>
        /// <remarks>
        /// Retorna o número de itens pendentes em cada fila Redis (novas mensagens, notificações e agendamentos),
        /// além do timestamp da consulta. Requer autenticação JWT.
        /// </remarks>
        /// <response code="200">Estatísticas das filas retornadas com sucesso.</response>
        /// <response code="401">Não autenticado.</response>
        /// <response code="500">Erro interno ao consultar as filas.</response>
        [HttpGet("queue-stats")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetQueueStats()
        {
            try
            {
                var novasMensagens = await _messageQueue.GetQueueLengthAsync(Consts.Filas.NovasMensagens);
                var notificacoes = await _messageQueue.GetQueueLengthAsync(Consts.Filas.Notificacoes);
                var agendamentos = await _messageQueue.GetQueueLengthAsync(Consts.Filas.Agendamentos);

                _logger.LogInformation("GetQueueStats: NovasMensagens={NovasMensagens}, Notificacoes={Notificacoes}, Agendamentos={Agendamentos}",
                    novasMensagens, notificacoes, agendamentos);

                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        NovasMensagens = novasMensagens,
                        Notificacoes = notificacoes,
                        Agendamentos = agendamentos,
                        Timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter estatísticas das filas");
                return StatusCode(500, new { Success = false, Message = "Erro ao consultar filas" });
            }
        }
    }
}

