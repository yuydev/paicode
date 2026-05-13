using Microsoft.SemanticKernel;
using VibeCodingDemo.Hubs;
using VibeCodingDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 1024 * 1024);

// ── Ollama via OpenAI-compatible API ─────────────────────
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var model = cfg["Ollama:Model"] ?? "qwen2.5-coder:7b";
    var endpoint = cfg["Ollama:Endpoint"] ?? "http://localhost:11434";

#pragma warning disable SKEXP0010
    return Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(
            modelId: model,
            apiKey: "ollama",
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{endpoint}/v1/"),
                Timeout = TimeSpan.FromMinutes(10)
            })
        .Build();
#pragma warning restore SKEXP0010
});

builder.Services.AddScoped<VibeCodingOrchestrator>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapHub<AgentHub>("/agenthub");
app.MapRazorComponents<VibeCodingDemo.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
