namespace PharmazapPlus.Shared.Enums;

public static class EnumExtensions
{
    /// <summary>Retorna o nome do enum em lowercase, compatível com os endpoints da API.</summary>
    public static string ToApiString(this RelatorioTipoEnum value) =>
        value.ToString().ToLowerInvariant();
}
