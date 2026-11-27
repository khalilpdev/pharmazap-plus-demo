namespace PharmazapPlus.Web.Services;

using PharmazapPlus.Shared.Enums;

public interface IChannelService
{
    Task<(bool Success, string Message)> CreateChannelAsync(ChannelCreateRequest request);
    Task<(bool Success, List<ChannelListItem> Channels, string Message)> GetChannelsAsync();
    Task<(bool Success, ChannelDetailDto? Channel, string Message)> GetChannelAsync(int type, bool isOfficial);
    Task<(bool Success, ChannelDetailDto? Channel, string Message)> GetChannelByIdAsync(int id);
    Task<(bool Success, string Message)> UpdateChannelAsync(int type, bool isOfficial, ChannelCreateRequest request);
    Task<(bool Success, string Message)> UpdateChannelByIdAsync(int id, ChannelCreateRequest request);
    Task<(bool Success, string Message)> DeleteChannelAsync(int id);
    Task<QrCodeResult> GenerateQrCodeAsync(string instanceName, string apiUrl, string apiKey);
    Task<PairingCodeResult> GeneratePairingCodeAsync(string instanceName, string apiUrl, string apiKey, string phoneNumber);
    Task<WhatsAppStatusResult> GetWhatsAppStatusAsync(string instanceName, string apiUrl, string apiKey);
    Task<ListInstancesResult> ListWhatsAppInstancesAsync(string apiUrl, string apiKey);
    Task<InstanceInfoResult> CheckInstanceExistsAsync(string instanceName, string apiUrl, string apiKey);
    Task<EnsureInstanceResult> EnsureInstanceCreatedAsync(string instanceName, string apiUrl, string apiKey = "", bool forceRecreate = false);
    Task<MetaOAuthStartResult> StartMetaOAuthAsync(
        ChannelType channelType,
        MetaConfigSource metaConfigSource,
        MetaAppMode metaAppMode,
        string? metaAppId,
        string? metaAppSecret,
        int? channelId = null);
}
