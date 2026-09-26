using FormsSystemStatsWidget.OnnxGenaiServer;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;
using FormsSystemStatsWidget.OnnxGenaiServer.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Phase 1: Infrastructure - Configuration & Diagnostics
// ============================================================

// Configure strongly typed options from appsettings.json + env vars
builder.Services.Configure<EngineOptions>(builder.Configuration.GetSection("Engine"));
builder.Services.Configure<RuntimeOptions>(builder.Configuration.GetSection("Runtime"));
builder.Services.Configure<CudaOptions>(builder.Configuration.GetSection("Cuda"));
builder.Services.Configure<GenerationDefaults>(builder.Configuration.GetSection("Generation"));
builder.Services.Configure<DiagnosticsOptions>(builder.Configuration.GetSection("Diagnostics"));
builder.Services.Configure<OnnxGenaiServerOptions>(builder.Configuration.GetSection(OnnxGenaiServerOptions.SectionName));

// Register singleton options
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<EngineOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RuntimeOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CudaOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenerationDefaults>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<DiagnosticsOptions>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OnnxGenaiServerOptions>>().Value);

// Configure logging with structured fields
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
    o.IncludeScopes = true;
});

// Add debug logger for development
builder.Logging.AddDebug();

// Log startup diagnostics
IConfiguration config = builder.Configuration;
var engineOptions = config.GetSection("Engine").Get<EngineOptions>() ?? new EngineOptions();
var runtimeOptions = config.GetSection("Runtime").Get<RuntimeOptions>() ?? new RuntimeOptions();
var cudaOptions = config.GetSection("Cuda").Get<CudaOptions>() ?? new CudaOptions();

var startupLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("=== FSSW ONNX GenAI Server Starting ===");
startupLogger.LogInformation("Target Framework: .NET {Framework}", Environment.Version);
startupLogger.LogInformation("Engine Options: ModelRootDirectory={ModelRootDir}, DefaultModel={DefaultModel}, ExecutionProvider={EP}",
    engineOptions.ModelRootDirectory, engineOptions.DefaultModel, engineOptions.ExecutionProvider);
startupLogger.LogInformation("Runtime Options: LazyLoad={LazyLoad}, ExecutionProviders={Providers}",
    runtimeOptions.LazyLoad, string.Join(", ", runtimeOptions.ExecutionProviders));
startupLogger.LogInformation("Cuda Options: Enabled={Enabled}, Stage0Device={Stage0Device}, Stage1Device={Stage1Device}",
    cudaOptions.Enabled, cudaOptions.Stage0Device, cudaOptions.Stage1Device);
startupLogger.LogInformation("===========================================");

var serverOptions = builder.Configuration.GetSection(OnnxGenaiServerOptions.SectionName).Get<OnnxGenaiServerOptions>() ?? new OnnxGenaiServerOptions();
if (serverOptions.ListenUrls.Length == 0)
{
    serverOptions.ListenUrls = ["http://localhost:8080"];
}

builder.WebHost.UseUrls(serverOptions.ListenUrls);

var app = builder.Build();

// ============================================================
// Engine Initialization
// ============================================================

// Get configured options
var engineOpts = app.Services.GetRequiredService<EngineOptions>();
var runtimeOpts = app.Services.GetRequiredService<RuntimeOptions>();
var cudaOpts = app.Services.GetRequiredService<CudaOptions>();

// Create engine with configured options
var serverOpts = app.Services.GetRequiredService<OnnxGenaiServerOptions>();
var engine = new OnnxGenaiEngine(serverOpts, app.Services.GetRequiredService<ILogger<OnnxGenaiEngine>>());

string[] apiBaseUrls = serverOptions.ListenUrls.Select(url => url.TrimEnd('/')).ToArray();
foreach (string apiBaseUrl in apiBaseUrls)
{
    app.Logger.LogInformation("API endpoints (available after model initialization): {BaseUrl}/ready, {BaseUrl}/health, {BaseUrl}/status, {BaseUrl}/v1/models",
        apiBaseUrl, apiBaseUrl, apiBaseUrl, apiBaseUrl);
    app.Logger.LogInformation("OpenAI-compatible chat endpoint: POST {Url}", $"{apiBaseUrl}/v1/chat/completions");
}

app.Logger.LogInformation("Initialisiere Modell {Model} mit Layout {Layout}; große ONNX-Modelle können einige Minuten laden.",
    serverOpts.DefaultModel, serverOpts.ModelLayout);
await engine.InitializeAsync();

if (!engine.IsReady)
{
    app.Logger.LogWarning("ONNX GenAI Engine nicht bereit - Server startet, aber Routen liefern 503 bis Modell geladen ist.");
    app.Logger.LogError(engine.LastError);
}
else
{
    app.Logger.LogInformation("Modell vollständig geladen: {Model}. API ist bereit.", engine.LoadedModelId);
}

// Map health endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    engineReady = engine.IsReady,
    loadedModelId = engine.LoadedModelId,
    modelIds = engine.ListModelIds(),
    uptime = DateTime.UtcNow - engine.StartTimeUtc
}));

// Map models endpoint
app.MapGet("/v1/models", () => Results.Ok(engine.BuildModelsResponse()));

// Map chat completions endpoint
app.MapPost("/v1/chat/completions", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleChatCompletionsAsync(ctx, engine, ct));

// Map completions endpoint
app.MapPost("/v1/completions", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleCompletionsAsync(ctx, engine, ct));

// Map embeddings endpoint (if supported)
app.MapPost("/v1/embeddings", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleEmbeddingsAsync(ctx, engine, ct));

// Map diagnostics endpoints (R41, R61)
app.MapGet("/status", () =>
{
    var cuda = EnvironmentDiscovery.DiscoverCuda();
    var ortVersion = EnvironmentDiscovery.DiscoverOrtVersion();
    return Results.Ok(new
    {
        engine = new
        {
            version = "1.0.0",
            ready = engine.IsReady,
            loadedModelId = engine.LoadedModelId,
            lastError = engine.LastError,
            uptime = DateTime.UtcNow - engine.StartTimeUtc
        },
        runtime = new
        {
            dotnet = EnvironmentDiscovery.GetDotNetVersion(),
            os = EnvironmentDiscovery.GetOsInfo(),
            cpu = EnvironmentDiscovery.GetCpuInfo(),
            ramMb = EnvironmentDiscovery.GetRamMb(),
            ortVersion
        },
        cuda = new
        {
            available = cuda.IsAvailable,
            deviceCount = cuda.DeviceCount,
            driverVersion = cuda.DriverVersion,
            devices = cuda.Devices.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                computeCapability = d.ComputeCapability,
                vramTotalMb = d.VramTotalMb,
                vramUsedMb = d.VramUsedMb,
                vramFreeMb = d.VramFreeMb
            }),
            warnings = cuda.Warnings
        },
        models = engine.ListModelIds()
    });
});

app.MapGet("/ready", (HttpContext ctx) =>
{
    if (!engine.IsReady)
    {
        ctx.Response.StatusCode = 503;
        ctx.Response.ContentType = "application/json";
        return Results.Json(new { ready = false, reason = engine.LastError });
    }
    return Results.Ok(new { ready = true, loadedModelId = engine.LoadedModelId });
});

app.MapGet("/metrics", () =>
{
    var cuda = EnvironmentDiscovery.DiscoverCuda();
    return Results.Ok(new
    {
        timestamp = DateTime.UtcNow,
        engine = new
        {
            ready = engine.IsReady,
            loadedModelId = engine.LoadedModelId,
            uptimeSeconds = (long)(DateTime.UtcNow - engine.StartTimeUtc).TotalSeconds
        },
        cuda = new
        {
            available = cuda.IsAvailable,
            deviceCount = cuda.DeviceCount,
            devices = cuda.Devices.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                vramTotalMb = d.VramTotalMb,
                vramUsedMb = d.VramUsedMb,
                vramFreeMb = d.VramFreeMb
            })
        },
        models = engine.ListModelIds()
    });
});

app.Logger.LogInformation("FSSW ONNX GenAI Server laeuft auf: {Urls}", string.Join(", ", serverOptions.ListenUrls));
try
{
    await app.RunAsync();
}
finally
{
    await engine.DisposeAsync();
}
