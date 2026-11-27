namespace PharmazapPlus.Shared.Models.Core;

public static class UserRoles
{
    public const string Desenvolvedor = "Desenvolvedor";
    public const string Gestor = "Gestor";
    public const string Administrador = "Administrador";
    public const string Atendente = "Atendente";

    // Combined role strings for use in [Authorize(Roles = ...)] attributes
    public const string GestorOuAdministrador = Desenvolvedor + "," + Gestor + "," + Administrador;
    public const string AdministradorOuAtendente = Administrador + "," + Atendente;
    public const string Todos = Desenvolvedor + "," + Gestor + "," + Administrador + "," + Atendente;

    public static string[] AllRoles = { Desenvolvedor, Gestor, Administrador, Atendente };

    public static string GetRoleDisplayName(string role)
    {
        return role switch
        {
            Desenvolvedor => "Desenvolvedor",
            Gestor => "Gestor do Sistema",
            Administrador => "Administrador da Farmácia",
            Atendente => "Atendente",
            _ => "Usuário"
        };
    }

    public static bool CanManageUsers(string currentRole, string targetRole)
    {
        var hierarchy = new Dictionary<string, int>
        {
            { Desenvolvedor, 0 },
            { Gestor, 1 },
            { Administrador, 2 },
            { Atendente, 3 }
        };

        return hierarchy.ContainsKey(currentRole) &&
               hierarchy.ContainsKey(targetRole) &&
               hierarchy[currentRole] <= hierarchy[targetRole];
    }

    public static bool IsGestorLike(string? role)
        => role == Desenvolvedor || role == Gestor;
}
