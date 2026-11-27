namespace PharmazapPlus.Web.Services
{
    public record LoginResponse
    {
        public bool Success { get; set; }
        public LoginResponseData? Data { get; set; }
    }
}
