using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Models.Vendas;

namespace PharmazapPlus.Shared.Services;

public class PedidoService
{
    public string GerarNumeroPedido(int? farmaciaId)
    {
        var now = DateTime.UtcNow;
        return $"PED-{now:yyyyMMdd}-{farmaciaId ?? 0:D4}";
    }

    public Pedido CriarDeAtendimento(Atendimento atendimento, decimal valorVenda)
    {
        return new Pedido
        {
            NumeroPedido = GerarNumeroPedido(atendimento.FarmaciaId),
            DataPedido = DateTime.UtcNow,
            Status = PedidoStatusEnum.Finalizado,
            TipoPedido = PedidoTipoEnum.Balcao,
            FormaPagamento = FormaPagamentoEnum.NaoEspecificado,
            ValorTotal = valorVenda,
            Desconto = 0,
            PrescricaoMedica = string.Empty,
            Observacoes = $"Atendimento #{atendimento.Id} - {atendimento.ComentarioAvaliacao ?? "Encerrado via chat"}",
            EnderecoEntrega = string.Empty,
            Entregador = string.Empty,
            ClienteId = atendimento.ClienteId ?? 0,
            Cliente = null!,
            FarmaciaId = atendimento.FarmaciaId,
            Farmacia = null!,
            AtendimentoId = atendimento.Id,
            AtendenteId = atendimento.AtendenteId
        };
    }
}