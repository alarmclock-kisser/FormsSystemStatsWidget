using FormsSystemStatsWidget.OnnxGenaiServer;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;
using FormsSystemStatsWidget.OnnxGenaiServer.OpenAi;

var builder = WebApplication.CreateBuilder(args);

var serverOptions = builder.Configuration.Get<OnnxGenaiServerOptions>() ?? new OnnxGenaiServerOptions();
builder.WebHost.UseUrls(serverOptions.ListenUrls);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

var app = builder.Build();

var engine = new OnnxGenaiEngine(serverOptions, app.Services.GetRequiredService<ILogger<OnnxGenaiEngine>>());
await engine.InitializeAsync();

if (!engine.IsReady)
{
    app.Logger.LogError("ONNX GenAI Engine konnte nicht initialisiert werden. Server startet trotzdem (Routen liefern 503).");
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    engineReady = engine.IsReady,
    models = engine.ListModelIds(),
    uptime = DateTime.UtcNow - engine.StartTimeUtc
}));

app.MapGet("/v1/models", () => Results.Ok(engine.BuildModelsResponse()));

app.MapPost("/v1/chat/completions", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleChatCompletionsAsync(ctx, engine, ct));

app.MapPost("/v1/completions", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleCompletionsAsync(ctx, engine, ct));

app.MapPost("/v1/embeddings", async (HttpContext ctx, CancellationToken ct) =>
    await OpenAiApiHandler.HandleEmbeddingsAsync(ctx, engine, ct));

app.Logger.LogInformation("FSSW ONNX GenAI Server laeuft auf: {Urls}", string.Join(", ", serverOptions.ListenUrls));

await app.RunAsync();

app.Lifetime.ApplicationStopping.Register(() => _ = engine.DisposeAsync());
