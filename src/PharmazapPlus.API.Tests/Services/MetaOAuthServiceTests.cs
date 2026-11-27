using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PharmazapPlus.API.Services.MetaOAuth;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Config;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PharmazapPlus.API.Tests.Services;

public class MetaOAuthServiceTests
{
    private static MetaOAuthService CreateService(HttpMessageHandler? handler = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(Consts.MetaApi.ClientName)).Returns(new HttpClient(handler ?? new QueueHttpMessageHandler()));

        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:BaseUrl"] = "https://localhost:5051"
            })
            .Build();

        var config = Options.Create(new MetaOAuthConfig
        {
            AppSecret = "meta-secret",
            RedirectUri = "https://localhost:5051/api/meta/oauth/callback",
            ApiVersion = "v21.0"
        });

        return new MetaOAuthService(
            httpClientFactory.Object,
            cache,
            config,
            configuration,
            NullLogger<MetaOAuthService>.Instance);
    }

    [Theory]
    [InlineData(MetaAppMode.Test, Consts.MetaApi.TestAppId)]
    [InlineData(MetaAppMode.Production, Consts.MetaApi.ProductionAppId)]
    public async Task CreateAuthorizationUrlAsync_UsesAppIdForSelectedMode(MetaAppMode appMode, string expectedAppId)
    {
        var service = CreateService();

        var result = await service.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            MetaOAuthChannelType.WhatsApp,
            MetaConfigSource.PharmazapCentral,
            appMode));

        result.Success.Should().BeTrue();
        result.AuthorizationUrl.Should().Contain($"client_id={expectedAppId}");
    }

    [Fact]
    public async Task CreateAuthorizationUrlAsync_WithInvalidMode_ReturnsFailure()
    {
        var service = CreateService();

        var result = await service.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            MetaOAuthChannelType.Instagram,
            MetaConfigSource.PharmazapCentral,
            (MetaAppMode)99));

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Modo do app Meta inválido.");
    }

    [Fact]
    public async Task CreateAuthorizationUrlAsync_ForInstagram_UsesOnlyInstagramLoginScopes()
    {
        var service = CreateService();

        var result = await service.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            MetaOAuthChannelType.Instagram,
            MetaConfigSource.PharmazapCentral,
            MetaAppMode.Test));

        result.Success.Should().BeTrue();
        result.AuthorizationUrl.Should().StartWith("https://www.instagram.com/oauth/authorize");
        result.AuthorizationUrl.Should().Contain("scope=instagram_business_basic%2Cinstagram_business_manage_messages");
        result.AuthorizationUrl.Should().NotContain("pages_show_list");
        result.AuthorizationUrl.Should().NotContain("pages_manage_metadata");
        result.AuthorizationUrl.Should().NotContain("business_management");
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_ForInstagram_UsesModeSpecificAppIdAndInstagramMeEndpoint()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new[]
                    {
                        new { access_token = "short-lived-token", user_id = "app-scoped-user", permissions = "instagram_business_basic,instagram_business_manage_messages" }
                    }
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "long-lived-token", token_type = "bearer", expires_in = 5184000 })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { user_id = "ig-user-123", username = "farmacia.teste", name = "Farmacia Teste" })
            });

        var service = CreateService(handler);
        var start = await service.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            MetaOAuthChannelType.Instagram,
            MetaConfigSource.PharmazapCentral,
            MetaAppMode.Test));

        var result = await service.CompleteAuthorizationAsync("oauth-code", start.State!);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("long-lived-token");
        result.InstagramBusinessId.Should().Be("ig-user-123");
        handler.Methods[0].Should().Be(HttpMethod.Post);
        handler.RequestUris[0].Should().Be("https://api.instagram.com/oauth/access_token");
        handler.RequestBodies[0].Should().Contain($"client_id={Consts.MetaApi.TestAppId}");
        handler.RequestBodies[0].Should().Contain("grant_type=authorization_code");
        handler.RequestUris[2].Should().StartWith("https://graph.instagram.com/v21.0/me");
        handler.RequestUris[1].Should().StartWith("https://graph.instagram.com/access_token?grant_type=ig_exchange_token");
    }

    [Fact]
    public async Task CompleteAuthorizationAsync_ForOwnConfig_UsesProvidedAppCredentials()
    {
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new[]
                    {
                        new { access_token = "short-lived-token", user_id = "app-scoped-user" }
                    }
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "long-lived-token", token_type = "bearer", expires_in = 5184000 })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { user_id = "ig-user-123", username = "farmacia.teste", name = "Farmacia Teste" })
            });

        var service = CreateService(handler);
        var start = await service.CreateAuthorizationUrlAsync(new MetaOAuthStartRequest(
            MetaOAuthChannelType.Instagram,
            MetaConfigSource.Own,
            MetaAppMode.Production,
            AppId: "own-app-id",
            AppSecret: "own-app-secret"));

        var result = await service.CompleteAuthorizationAsync("oauth-code", start.State!);

        result.Success.Should().BeTrue();
        handler.RequestBodies[0].Should().Contain("client_id=own-app-id");
        handler.RequestBodies[0].Should().Contain("client_secret=own-app-secret");
    }

    private sealed class QueueHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestUris { get; } = new();
        public List<string> RequestBodies { get; } = new();
        public List<HttpMethod> Methods { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            RequestUris.Add(request.RequestUri!.ToString());
            RequestBodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
