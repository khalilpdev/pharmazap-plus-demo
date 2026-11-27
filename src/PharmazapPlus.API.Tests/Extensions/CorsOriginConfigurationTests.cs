using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PharmazapPlus.API.Extensions;

namespace PharmazapPlus.API.Tests.Extensions;

public class CorsOriginConfigurationTests
{
    [Fact]
    public void GetAdditionalOrigins_WithIndexedConfiguration_ReturnsConfiguredOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AdditionalOrigins:0"] = "http://localhost:9080",
                ["Cors:AdditionalOrigins:1"] = "https://app.pharmazapplus.com"
            })
            .Build();

        var origins = CorsOriginConfiguration.GetAdditionalOrigins(configuration);

        origins.Should().BeEquivalentTo("http://localhost:9080", "https://app.pharmazapplus.com");
    }

    [Fact]
    public void GetAdditionalOrigins_WithDelimitedString_ReturnsDistinctOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AdditionalOrigins"] = "http://108.174.150.4:9080; http://108.174.150.4:9080, https://app.pharmazapplus.com"
            })
            .Build();

        var origins = CorsOriginConfiguration.GetAdditionalOrigins(configuration);

        origins.Should().BeEquivalentTo("http://108.174.150.4:9080", "https://app.pharmazapplus.com");
    }
}
