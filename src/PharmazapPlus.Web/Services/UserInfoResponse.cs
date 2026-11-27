namespace PharmazapPlus.Web.Services
{
    public record UserInfoResponse
    {
        public bool Success { get; set; }
        public UserInfoResponseData? Data { get; set; }
    }
}
