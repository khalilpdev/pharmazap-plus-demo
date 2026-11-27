using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Extensions;
using PharmazapPlus.Web.Components;
using PharmazapPlus.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Allow developers to override settings locally via appsettings.Local.json (gitignored)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// ============================================
// AUTENTICAÇÃO COM COOKIES
// ============================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = Consts.Web.CookieName;
        options.LoginPath = Consts.Web.LoginPath;
        options.LogoutPath = Consts.Web.LogoutPath;
        options.AccessDeniedPath = Consts.Web.AcessoNegadoPath;
        options.ExpireTimeSpan = TimeSpan.FromDays(Consts.Web.CookieExpiracaoDias);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, PharmazapAuthStateProvider>();
builder.Services.AddHttpContextAccessor();

// ============================================
// HTTP CLIENT PARA API
// ============================================
builder.Services.AddHttpClient();
var pharmazapApiBuilder = builder.Services.AddHttpClient("PharmazapApi", client =>
{
    var apiUrl = builder.Configuration["Application:ApiUrl"] ?? "https://localhost:5051";
    client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Consts.Web.HttpTimeoutSeconds);
});
if (builder.Environment.IsDevelopment())
{
    pharmazapApiBuilder.ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
}

// ============================================
// SEUS SERVIÇOS
// ============================================
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<IWhatsAppClient, WhatsAppClient>();
builder.Services.AddScoped<ICampanhaService, CampanhaService>();
builder.Services.AddScoped<IAtendenteService, AtendenteService>();
builder.Services.AddScoped<IAdministradorService, AdministradorService>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IPharmazapApiService, PharmazapApiService>();
builder.Services.AddScoped<PharmaHubService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ============================================
// MODO PORTFÓLIO
// ============================================
// A versão de portfólio expõe somente o fluxo operacional do produto.
// Áreas de back-office, integrações e testes internos ficam ocultas
// do menu e bloqueadas por redirecionamento.
var portfolioMode = PortfolioMode.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(portfolioMode);

var app = builder.Build();

static bool IsAllowedExternalMediaUri(string? url, out Uri? uri)
{
    uri = null;
    if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
        !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var host = parsed.Host.ToLowerInvariant();
    if (host == Consts.WhatsAppCdn.WhatsAppNetDomain ||
        host.EndsWith($".{Consts.WhatsAppCdn.WhatsAppNetDomain}", StringComparison.Ordinal) ||
        host == Consts.Midia.BlobStorageHost)
    {
        uri = parsed;
        return true;
    }

    return false;
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

// Bloqueia o acesso direto por URL às áreas fora do escopo do portfólio.
// Deve vir antes da autenticação para que a rota oculta não seja tratada
// como página protegida (o usuário é enviado ao Dashboard, não ao login).
if (portfolioMode.Habilitado)
{
    app.Use(async (context, next) =>
    {
        if (portfolioMode.DeveBloquear(context.Request.Path))
        {
            context.Response.Redirect(PortfolioMode.RotaInicial);
            return;
        }

        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Proxy endpoint: serve farmácia logos through the Web server.
// This avoids browser SSL certificate errors in development (Fedora, Linux, etc.)
// where the API's dev certificate is not trusted by the browser.
// The server-side C# HttpClient bypasses cert validation in dev and fetches the image,
// then streams it back to the browser from the same origin as the Web app.
app.MapGet("/logo-proxy/{fileName}", async (string fileName, IHttpClientFactory httpClientFactory) =>
{
    var cleanName = Path.GetFileName(fileName);
    if (string.IsNullOrWhiteSpace(cleanName)) return Results.NotFound();

    var ext = Path.GetExtension(cleanName).ToLowerInvariant();
    if (!Consts.FarmaciaLogo.AllowedExtensions.Contains(ext)) return Results.NotFound();

    try
    {
        var client = httpClientFactory.CreateClient(Consts.Web.ApiClientName);
        // Try new endpoint, then legacy path for backward compatibility
        var response = await client.GetAsync($"api/account/farmacia-logo/{Uri.EscapeDataString(cleanName)}");
        if (!response.IsSuccessStatusCode)
            response = await client.GetAsync($"uploads/logos/{Uri.EscapeDataString(cleanName)}");
        if (!response.IsSuccessStatusCode)
            return Results.NotFound();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType
                          ?? Consts.FarmaciaLogo.GetContentType(ext);
        return Results.File(bytes, contentType);
    }
    catch
    {
        return Results.NotFound();
    }
}).AllowAnonymous();

// QR codes are generated by the API, but the browser must load their image from
// the public Web origin. Proxying avoids exposing an internal API address such
// as localhost:5051 to the user's browser.
app.MapGet("/qr-code-proxy/{fileName}", async (
    string fileName,
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("QrCodeProxy");
    var cleanName = Path.GetFileName(fileName);
    var extension = Path.GetExtension(cleanName).ToLowerInvariant();
    var instanceName = Path.GetFileNameWithoutExtension(cleanName);
    var isValidQrFile = instanceName.Length > 0 &&
                        instanceName.All(character => char.IsLetterOrDigit(character) || character is '-' or '_') &&
                        extension is ".png" or ".jpg" or ".svg";
    if (!isValidQrFile)
        return Results.NotFound();

    try
    {
        var client = httpClientFactory.CreateClient(Consts.Web.ApiClientName);
        using var response = await client.GetAsync(
            $"uploads/qrcodes/{Uri.EscapeDataString(cleanName)}",
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            return Results.NotFound();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? extension switch
        {
            ".jpg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "image/png"
        };
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return Results.File(bytes, contentType);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to proxy QR code {FileName}", cleanName);
        return Results.NotFound();
    }
}).AllowAnonymous();

// Proxy endpoint for locally stored media files (images, audio, video, documents).
// Avoids browser CORS / cross-origin issues when the Web app and the API run on
// different origins (e.g. Web on 5160 and API on 5051). The server-side HttpClient
// fetches the file from the API and returns the content from the same origin as
// the Web app, so <img>, <audio> and <video> tags can load it without CORS errors.
app.MapGet("/media-proxy/uploads/{folder}/{fileName}", async (
    string folder,
    string fileName,
    string? contentType,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("MediaProxy");

    var cleanFolder = Path.GetFileName(folder);
    var cleanName = Path.GetFileName(fileName);
    if (string.IsNullOrWhiteSpace(cleanFolder) || cleanFolder.Contains("..") ||
        string.IsNullOrWhiteSpace(cleanName) || cleanName.Contains(".."))
    {
        return Results.NotFound();
    }

    // Allowlist: only the "media" folder is exposed via the proxy. Other upload
    // folders (logos, etc.) have their own dedicated proxy endpoints.
    if (cleanFolder is not "media")
        return Results.NotFound();

    var ext = Path.GetExtension(cleanName).ToLowerInvariant();
    string[] allowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
        ".mp4", ".webm", ".ogg", ".mp3", ".m4a", ".wav", ".aac",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".bin"
    ];
    if (!allowedExtensions.Contains(ext))
        return Results.NotFound();

    try
    {
        var client = httpClientFactory.CreateClient(Consts.Web.ApiClientName);
        var response = await client.GetAsync(
            $"uploads/{cleanFolder}/{Uri.EscapeDataString(cleanName)}",
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            return Results.NotFound();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var resolvedContentType =
            response.Content.Headers.ContentType?.MediaType.NormalizeAllowedMediaContentType() ??
            contentType.NormalizeAllowedMediaContentType() ??
            "application/octet-stream";
        return Results.File(bytes, resolvedContentType);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to proxy media file {Folder}/{File}", cleanFolder, cleanName);
        return Results.NotFound();
    }
}).AllowAnonymous();

app.MapGet("/media-proxy/fetch", async (
    string url,
    string? contentType,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("ExternalMediaProxy");

    if (!IsAllowedExternalMediaUri(url, out var targetUri))
        return Results.NotFound();

    try
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(targetUri, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            return Results.NotFound();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var resolvedContentType =
            response.Content.Headers.ContentType?.MediaType.NormalizeAllowedMediaContentType() ??
            contentType.NormalizeAllowedMediaContentType() ??
            "application/octet-stream";

        return Results.File(bytes, resolvedContentType);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to proxy external media {Url}", url);
        return Results.NotFound();
    }
}).AllowAnonymous();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
