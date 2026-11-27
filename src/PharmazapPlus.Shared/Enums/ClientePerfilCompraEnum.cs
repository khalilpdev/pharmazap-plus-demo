namespace PharmazapPlus.Shared.Enums;

public enum ClientePerfilCompraEnum
{
    /// <summary>Primeiro contato, ainda sem histórico de compra</summary>
    Prospecto = 0,

    /// <summary>Comprou ao menos uma vez, mas sem regularidade</summary>
    Ocasional = 1,

    /// <summary>Compra com certa frequência, mas não toda semana/mês</summary>
    Recorrente = 2,

    /// <summary>Uso contínuo — compra regularmente, geralmente medicamentos fixos</summary>
    UsoContinuo = 3,

    /// <summary>Alto valor + alta frequência — cliente estratégico</summary>
    Fidelizado = 4
}
