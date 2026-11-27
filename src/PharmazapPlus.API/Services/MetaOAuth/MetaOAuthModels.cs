using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.API.Services.MetaOAuth;

public enum MetaOAuthChannelType
{
    WhatsApp = 1,
    Instagram = 2
}

public sealed record MetaOAuthStartRequest(
    MetaOAuthChannelType ChannelType,
    MetaConfigSource ConfigSource,
    MetaAppMode AppMode,
    string? AppId = null,
    string? AppSecret = null,
    int? FarmaciaId = null,
    bool SaveFullLog = false);

public sealed record MetaOAuthStartResult(
    bool Success,
    string? AuthorizationUrl,
    string? State,
    string? Message,
    string? Error);

public sealed record MetaOAuthStatePayload(
    MetaOAuthChannelType ChannelType,
    MetaConfigSource ConfigSource,
    MetaAppMode AppMode,
    string? AppId,
    string? AppSecret,
    DateTimeOffset CreatedAt,
    int? FarmaciaId = null,
    bool SaveFullLog = false);

public sealed record MetaOAuthConnectionResult(
    bool Success,
    string Message,
    string? AccessToken,
    string? PageId,
    string? PageName,
    string? InstagramBusinessId,
    string? WhatsAppBusinessAccountId,
    string? PhoneNumberId,
    string? BusinessAccountId,
    string? ErrorCode = null);

public sealed record MetaOAuthStartHttpRequest(
    MetaOAuthChannelType ChannelType,
    MetaConfigSource ConfigSource,
    MetaAppMode AppMode = MetaAppMode.Production,
    string? AppId = null,
    string? AppSecret = null,
    int? ChannelId = null);
