namespace PharmazapPlus.Web.Services
{
    public record UserInfoResponseData
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? FarmaciaId { get; set; }
        public string? FarmaciaNome { get; set; }
        public string? FarmaciaLogoUrl { get; set; }
    }
}
