using Microsoft.AspNetCore.Http;

namespace PharmazapPlus.Web.Services;

/// <summary>
/// Modo portfólio: a versão publicada expõe apenas o fluxo operacional do produto
/// (dashboard, atendimento, clientes, relatórios e configurações). Áreas de
/// back-office, integrações de terceiros e o harness interno de testes ficam
/// ocultas do menu e bloqueadas por redirecionamento.
/// Desative com "Portfolio:Habilitado" = false (appsettings ou variável
/// de ambiente Portfolio__Habilitado).
/// </summary>
public sealed class PortfolioMode
{
    public const string ConfigKey = "Portfolio:Habilitado";
    public const string RotaInicial = "/dashboard";

    /// <summary>Rotas (e subrotas) fora do escopo da versão de portfólio.</summary>
    private static readonly string[] RotasOcultas =
    [
        "/administradores",
        "/campanhas",
        "/channels",
        "/farmacias",
        "/tests",
    ];

    private PortfolioMode(bool habilitado) => Habilitado = habilitado;

    /// <summary>Indica se a restrição está ativa.</summary>
    public bool Habilitado { get; }

    public static PortfolioMode FromConfiguration(IConfiguration configuration) =>
        new(configuration.GetValue(ConfigKey, true));

    /// <summary>True quando a rota informada pertence a uma área oculta.</summary>
    public static bool RotaOculta(string? rota)
    {
        var caminho = rota?.TrimEnd('/');
        if (string.IsNullOrEmpty(caminho) || caminho == "/")
            return false;

        return RotasOcultas.Any(oculta =>
            caminho.Equals(oculta, StringComparison.OrdinalIgnoreCase) ||
            caminho.StartsWith(oculta + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True quando a rota pode ser exibida/acessada no modo atual.</summary>
    public bool RotaPermitida(string? rota) => !Habilitado || !RotaOculta(rota);

    /// <summary>True quando a requisição deve ser redirecionada para a rota inicial.</summary>
    public bool DeveBloquear(PathString path) => Habilitado && RotaOculta(path.Value);
}
