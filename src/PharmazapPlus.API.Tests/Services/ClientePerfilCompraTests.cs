using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.CRM;
using FluentAssertions;

namespace PharmazapPlus.API.Tests.Services;

public class ClientePerfilCompraTests
{
    // ── AtualizarPerfilCompra ────────────────────────────────────────

    [Fact]
    public void AtualizarPerfilCompra_SemPedidos_RetornaProspecto()
    {
        var cliente = GetFakeCliente();
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Prospecto);
    }

    [Fact]
    public void AtualizarPerfilCompra_UmPedido_RetornaOcasional()
    {
        var cliente = GetFakeCliente();
        cliente.TotalPedidos = 1;
        cliente.UltimaCompra = DateTime.UtcNow.AddDays(-10);
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Ocasional);
    }

    [Fact]
    public void AtualizarPerfilCompra_DoisPedidosRecentes_RetornaRecorrente()
    {
        var cliente = GetFakeCliente();
        cliente.TotalPedidos = 2;
        cliente.UltimaCompra = DateTime.UtcNow.AddDays(-15);
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Recorrente);
    }

    [Fact]
    public void AtualizarPerfilCompra_CincoPedidosRecentes_RetornaFidelizado()
    {
        var cliente = GetFakeCliente();
        cliente.TotalPedidos = 5;   
        cliente.UltimaCompra = DateTime.UtcNow.AddDays(-15);
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Fidelizado);
    }

    [Fact]
    public void AtualizarPerfilCompra_MuitosPedidosMasDistante_RetornaOcasional()
    {
        var cliente = GetFakeCliente();
        cliente.TotalPedidos = 10;
        cliente.UltimaCompra = DateTime.UtcNow.AddDays(-90);
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Ocasional);
    }

    [Fact]
    public void AtualizarPerfilCompra_UsoContinuoTrue_RetornaUsoContinuo()
    {
        var cliente = GetFakeCliente();
        cliente.UsoContinuo = true;
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.UsoContinuo);
    }

    [Fact]
    public void AtualizarPerfilCompra_TemMedicamentosContinuos_RetornaUsoContinuo()
    {
        var cliente = GetFakeCliente();
        cliente.MedicamentosContinuos = "[\"Losartana\"]";
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.UsoContinuo);
    }

    [Fact]
    public void AtualizarPerfilCompra_SemDataCompra_RetornaProspecto()
    {
        var cliente = GetFakeCliente();
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Prospecto);
    }

    [Fact]
    public void AtualizarPerfilCompra_CincoPedidosMas46Dias_RetornaOcasional()
    {
        // >= 5 pedidos but 46 days since last purchase — misses both Fidelizado and Recorrente windows
        var cliente = new Cliente
        {
            Nome = "Test",
            WhatsAppId = "123",
            Farmacia = null!,
            TotalPedidos = 5,
            UltimaCompra = DateTime.UtcNow.AddDays(-46)
        };
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Ocasional);
    }

    [Fact]
    public void AtualizarPerfilCompra_QuatroPedidosDentro45Dias_RetornaRecorrente()
    {
        // 4 orders within 45 days — qualifies for Recorrente but NOT Fidelizado (needs >= 5)
        var cliente = new Cliente
        {
            Nome = "Test",
            WhatsAppId = "123",
            Farmacia = null!,
            TotalPedidos = 4,
            UltimaCompra = DateTime.UtcNow.AddDays(-30)
        };
        cliente.AtualizarPerfilCompra();
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Recorrente);
    }

    // ── AdicionarPedido integration ──────────────────────────────────

    [Fact]
    public void AdicionarPedido_PrimeiroPedido_ClassificaComoOcasional()
    {
        Cliente cliente = GetFakeCliente();
        cliente.AdicionarPedido(50m);
        // After first order: TotalPedidos = 1 → Ocasional
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.Ocasional);
        cliente.TotalPedidos.Should().Be(1);
        cliente.TotalGasto.Should().Be(50m);
    }

   
    [Fact]
    public void AdicionarPedido_UsoContinuoTrue_MantemUsoContinuo()
    {
        var cliente = new Cliente { TotalPedidos = 0, UsoContinuo = true, Nome = "Test", WhatsAppId = "123", Farmacia = null! };
        cliente.AdicionarPedido(100m);
        cliente.PerfilCompra.Should().Be(ClientePerfilCompraEnum.UsoContinuo);
    }

    [Fact]
    public void AdicionarPedido_AtualizaPontuacaoFidelidade()
    {
        var cliente = new Cliente { TotalPedidos = 0, TotalGasto = 0, Nome = "Test", WhatsAppId = "123", Farmacia = null! };
        cliente.AdicionarPedido(150m);
        // 150 / 10 = 15 pontos
        cliente.PontuacaoFidelidade.Should().Be(15);
    }

    [Fact]
    public void AdicionarPedido_UltimaCompraAtualizada()
    {
        var before = DateTime.UtcNow;
        var cliente = new Cliente
        {
            TotalPedidos = 0,
            Nome = "Test",
            WhatsAppId = "123",
            Farmacia = null!
        };

        cliente.AdicionarPedido(50m);
        cliente.UltimaCompra.Should().NotBeNull();
        cliente.UltimaCompra!.Value.Should().BeOnOrAfter(before);
    }

    private static Cliente GetFakeCliente()
    {
        return new Cliente
        {
            TotalPedidos = 0,
            Nome = "Test",
            WhatsAppId = "123",
            Farmacia = null!
        };
    }
}
