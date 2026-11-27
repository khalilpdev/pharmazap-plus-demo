using Microsoft.Extensions.Configuration;

namespace PharmazapPlus.API.Extensions;

public static class CorsOriginConfiguration
{
    public static string[] GetAdditionalOrigins(IConfiguration configuration)
    {
        var origins = new List<string>();

        var configuredArray = configuration
            .GetSection("Cors:AdditionalOrigins")
            .Get<string[]>();

        if (configuredArray is { Length: > 0 })
        {
            origins.AddRange(configuredArray);
        }

        var delimitedOrigins = configuration["Cors:AdditionalOrigins"];
        if (!string.IsNullOrWhiteSpace(delimitedOrigins))
        {
            origins.AddRange(delimitedOrigins.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return origins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
