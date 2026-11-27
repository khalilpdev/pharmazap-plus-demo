namespace PharmazapPlus.Web.Services
{
    public record LoginResponseData
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string FarmaciaId { get; set; } = "";
        public string FarmaciaNome { get; set; } = "";
        public string? FarmaciaLogoUrl { get; set; }
        public string Token { get; set; } = "";
        public DateTime Expiration { get; set; }
        public string Theme { get; set; } = "Light";
        public string? AccentColor { get; set; }
        public bool TermoAceiteRequired { get; set; }
    }
}
