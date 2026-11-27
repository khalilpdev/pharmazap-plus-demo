namespace PharmazapPlus.Web.Services
{
    public class UsuarioInfo
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Role { get; set; } = "";
        public string? FarmaciaNome { get; set; }
    }
}
