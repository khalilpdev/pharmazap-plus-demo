namespace PharmazapPlus.API.Services.MetaOAuth;

public interface IMetaOAuthService
{
    Task<MetaOAuthStartResult> CreateAuthorizationUrlAsync(MetaOAuthStartRequest request);
    Task<MetaOAuthConnectionResult> CompleteAuthorizationAsync(string code, string state);
}
