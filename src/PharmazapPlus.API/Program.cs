using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using NLog;
using NLog.Web;
using PharmazapPlus.API.Filters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerUI;
using PharmazapPlus.API.BackgroundServices;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Models;
using PharmazapPlus.API.Services.Campanhas;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Hubs;
using PharmazapPlus.API.Services.Monitoring;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.Instagram;
using PharmazapPlus.API.Services.MetaOAuth;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Config;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PharmazapPlus.API.Services.Login;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.API.Extensions;
using PharmazapPlus.Shared.Services;

var builder = WebApplication.CreateBuilder(args);
var nlogBasePath = AppContext.BaseDirectory;
var nlogConfigPath = Path.Combine(nlogBasePath, "nlog.config");

// Allow developers to override settings locally via appsettings.Local.json (gitignored)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

if (!File.Exists(nlogConfigPath))
{
    nlogConfigPath = Path.Combine(builder.Environment.ContentRootPath, "nlog.config");
    nlogBasePath = builder.Environment.ContentRootPath;
}

NLogDailyFileRetention.EnsureRetention(nlogBasePath);
GlobalDiagnosticsContext.Set("LogDirectory", Path.Combine(nlogBasePath, "logs"));

// Configuração logging com NLog
LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Configura��o servi�os
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();

// Registrar servi�os CRM
builder.Services.AddScoped<ICrmService, CrmService>();
builder.Services.AddScoped<IClienteImporter, ClienteImporter>();
builder.Services.AddScoped<IClienteExporter, ClienteExporter>();

// Registrar serviço de Campanhas
builder.Services.AddScoped<ICampanhaService, CampanhaService>();

// Registrar serviços de domínio
builder.Services.AddScoped<PedidoService>();

// Serviço de bloqueio de remetentes (spam)
builder.Services.AddScoped<IRemetenteBloqueadoService, RemetenteBloqueadoService>();

// Serviço de criptografia de mídia do WhatsApp (MMS3)
builder.Services.AddScoped<IEvolutionGoCryptoService, EvolutionGoCryptoService>();

// Criptografia AES-256 para dados sensíveis (LGPD)
{
    var encryptionKeyBase64 =
        Environment.GetEnvironmentVariable("PHARMAZAP_ENCRYPTION_KEY")
        ?? builder.Configuration["Encryption:Key"];

    if (string.IsNullOrEmpty(encryptionKeyBase64))
    {
        // Em desenvolvimento, gera uma chave efêmera e alerta o desenvolvedor
        encryptionKeyBase64 = CryptoService.GenerateBase64Key();
        Console.WriteLine("⚠️  PHARMAZAP_ENCRYPTION_KEY não configurada. " +
                          "Uma chave temporária foi gerada para este processo. " +
                          "Defina a variável de ambiente ou Encryption:Key para persistir a chave entre reinicializações.");
    }

    builder.Services.AddSingleton<ICryptoService>(CryptoService.FromBase64Key(encryptionKeyBase64));
}

// Swagger with authentication
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe o token JWT no formato: Bearer {token}"
    });

    c.OperationFilter<BearerAuthOperationFilter>();
});

// Autentica��o JWT
builder.Services.AddScoped<TokenProvider>();
var jwtKey = builder.Configuration["Jwt:Key"];
if (!string.IsNullOrEmpty(jwtKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };

            // Configura��o para SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(Consts.Endpoints.PharmaHub))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
}

// CORS
var corsOrigins = new List<string>(Consts.Cors.DefaultOrigins);

// Origens adicionais configuráveis via appsettings / variável de ambiente.
// Aceita tanto array (Cors:AdditionalOrigins:0) quanto string delimitada por ; ou ,
// para compatibilidade com scripts de deploy e overrides por ambiente.
var additionalOrigins = CorsOriginConfiguration.GetAdditionalOrigins(builder.Configuration);
if (additionalOrigins?.Length > 0)
    corsOrigins.AddRange(additionalOrigins.Where(o => !string.IsNullOrWhiteSpace(o)));

builder.Services.AddCors(options =>
{
    options.AddPolicy(Consts.Cors.PolicyName, policy =>
    {
        policy.WithOrigins(corsOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Database Context
builder.Services.AddDbContext<PharmazapDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: Consts.Database.MaxRetryCount,
                maxRetryDelay: TimeSpan.FromSeconds(Consts.Database.MaxRetryDelaySeconds),
                errorCodesToAdd: null);
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    }
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = Consts.SignalRConfig.MaximumReceiveMessageSizeBytes; // 10MB
});

// Serviços personalizados
// ═══════════════════════════════════════════════════════════════════════════════
// WhatsApp usa mock quando WhatsApp:MockMessages = true (configurável em
// appsettings.json, appsettings.Development.json, .env ou appsettings.Local.json).
// Instagram sempre usa a implementação real porque o fluxo inbound depende do webhook oficial.
// ═══════════════════════════════════════════════════════════════════════════════

var whatsAppMockMessages = builder.Configuration.GetValue<bool>("WhatsApp:MockMessages");

if (whatsAppMockMessages)
{
    builder.Services.AddScoped<IWhatsAppService, MockWhatsAppService>();
    Console.WriteLine("🎭 WhatsApp Service: Using MOCK (WhatsApp:MockMessages = true)");
}
else
{
    builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
    {
        client.BaseAddress = new Uri(Consts.MetaApi.BaseUrl);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    });
    Console.WriteLine("✅ WhatsApp Service: Using REAL API (Meta/WhatsApp Business)");
}

builder.Services.AddHttpClient<IInstagramService, InstagramService>(client =>
{
    client.BaseAddress = new Uri(Consts.MetaApi.BaseUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});
Console.WriteLine("✅ Instagram Service: Using REAL API (Meta/Instagram Business)");

// Registre ChatService apenas se a interface IChatService existir
// Se n�o existir, comente ou crie a interface
// builder.Services.AddScoped<IChatService, ChatService>();

builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddSingleton<IMessageQueue, RedisMessageQueue>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PharmazapDbContext>();
// Remova AddUrlGroup se n�o tiver o pacote
// .AddUrlGroup(new Uri("https://graph.facebook.com"), "Facebook API");

// Background Services
builder.Services.AddHostedService<MessageQueueProcessor>();
builder.Services.AddHostedService<ServerHealthMonitorService>();
// Certifique-se que AgendamentoReminderService implementa IHostedService
// builder.Services.AddHostedService<AgendamentoReminderService>();

// HTTP Client Factory
builder.Services.AddHttpClient(Consts.MetaApi.ClientName, client =>
{
    client.BaseAddress = new Uri(Consts.MetaApi.BaseUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// Configura��o WhatsApp
builder.Services.Configure<WhatsAppConfig>(builder.Configuration.GetSection("WhatsApp"));

// Configuração Instagram
builder.Services.Configure<PharmazapPlus.Shared.Models.Config.InstagramConfig>(builder.Configuration.GetSection("Instagram"));
builder.Services.Configure<MetaOAuthConfig>(builder.Configuration.GetSection("MetaOAuth"));
builder.Services.AddScoped<IMetaOAuthService, MetaOAuthService>();

// Configuração Health Monitor e Email
builder.Services.Configure<HealthMonitorConfig>(builder.Configuration.GetSection("HealthMonitor"));
builder.Services.Configure<EmailConfig>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
builder.Services.AddSingleton<IServerMonitorNotificationService, ServerMonitorNotificationService>();

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    });

    // Distributed cache via Redis
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
    });

    // SignalR com Redis backplane para escala
    builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal(Consts.SignalRConfig.ChannelPrefix);
    });
}

var app = builder.Build();
app.Logger.LogInformation("Pharmazap Plus API inicializando em {TimestampUtc}", DateTime.UtcNow);

// Configure EPPlus non-commercial license once at startup
OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("PharmazapPlus");

// Pipeline configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerSecurityFix();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint(Consts.Endpoints.SwaggerJson, "Pharmazap Plus API v1");
        c.RoutePrefix = Consts.Endpoints.ApiDocs;
        c.OAuthClientId(Consts.Auth.SwaggerClientId);
        c.OAuthAppName(Consts.Auth.SwaggerAppName);
    });
}


app.UseCors(Consts.Cors.PolicyName);
app.UseHttpsRedirection();

// Ensure wwwroot directory exists and configure static file serving with explicit provider.
// Without this, WebRootFileProvider stays NullFileProvider when wwwroot didn't exist at startup,
// causing uploaded logos to return 404 even after the directory is created dynamically.
var wwwrootPath = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwrootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

// Backward-compatibility route: old logo URLs stored in DB as /uploads/logos/{fileName}
// are served directly so clients don't need re-upload after the URL scheme change.
app.MapGet(Consts.FarmaciaLogo.LegacyRoute, (string fileName, IWebHostEnvironment env) =>
{
    var cleanName = Path.GetFileName(fileName);
    if (string.IsNullOrWhiteSpace(cleanName)) return Results.NotFound();

    var ext = Path.GetExtension(cleanName).ToLowerInvariant();
    if (!Consts.FarmaciaLogo.AllowedExtensions.Contains(ext)) return Results.NotFound();

    var uploadsDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, Consts.FarmaciaLogo.UploadsRelativePath));
    var filePath = Path.GetFullPath(Path.Combine(uploadsDir, cleanName));
    if (!filePath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        return Results.NotFound();

    if (!File.Exists(filePath)) return Results.NotFound();

    return Results.File(filePath, Consts.FarmaciaLogo.GetContentType(ext));
}).AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

// Mapeamento de endpoints
app.MapControllers();
app.MapHub<PharmaHub>(Consts.Endpoints.PharmaHub);
app.MapHealthChecks(Consts.Endpoints.Health, new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(e => new
            {
                Component = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds
            }),
            Timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// Endpoint de informa��es do sistema
app.MapGet("/system/info", () =>
{
    var assembly = Assembly.GetExecutingAssembly();
    return new
    {
        Application = Consts.System.ApplicationName,
        Version = assembly.GetName().Version?.ToString() ?? Consts.System.ApiVersion,
        Environment = app.Environment.EnvironmentName,
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        Timestamp = DateTime.UtcNow,
        Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()
    };
});

// Aplicar migrações automáticas e seed de dados
await DatabaseInitializer.InitializeAsync(app);

app.Logger.LogInformation($"Pharmazap Plus API iniciada em {DateTime.UtcNow}");
app.Run();
