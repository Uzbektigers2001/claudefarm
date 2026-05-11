using AgentFarm.Agents.Agents;
using AgentFarm.Agents.Options;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Bot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// --- Telegram Bot ---
var botToken = builder.Configuration["TelegramBot:Token"]
    ?? throw new InvalidOperationException("TelegramBot:Token sozlanmagan!");

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botToken));

// --- Bot Services ---
builder.Services.AddSingleton<ITelegramMessageSender, TelegramMessageSender>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<UpdateHandler>();

// --- Claude / OmniRoute ---
builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection("Anthropic"));

builder.Services.AddHttpClient<ClaudeApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});

// --- Session Store ---
builder.Services.AddSingleton<InMemorySessionStore>();

// --- Agentlar ---
builder.Services.AddSingleton<OrchestratorAgent>();

// Developer agentlar - 3 ta alohida instance
builder.Services.AddTransient<DeveloperAgent>(sp =>
    new DeveloperAgent(
        sp.GetRequiredService<ClaudeApiClient>(),
        sp.GetRequiredService<ITelegramMessageSender>(),
        sp.GetRequiredService<ILogger<DeveloperAgent>>()
    ));

builder.Services.AddSingleton<QAAgent>();
builder.Services.AddSingleton<ReviewerAgent>();

// --- Pipeline (Orchestrator arxitekturasi) ---
builder.Services.AddSingleton<IAgentPipelineRunner>(sp =>
{
    var orchestrator = sp.GetRequiredService<OrchestratorAgent>();
    var developer1   = sp.GetRequiredService<DeveloperAgent>();
    var developer2   = sp.GetRequiredService<DeveloperAgent>();
    var developer3   = sp.GetRequiredService<DeveloperAgent>();
    var qa           = sp.GetRequiredService<QAAgent>();
    var reviewer     = sp.GetRequiredService<ReviewerAgent>();
    var sessionStore = sp.GetRequiredService<InMemorySessionStore>();
    var sender       = sp.GetRequiredService<ITelegramMessageSender>();
    var logger       = sp.GetRequiredService<ILogger<OrchestratorPipelineRunner>>();

    return new OrchestratorPipelineRunner(
        orchestrator, developer1, developer2, developer3,
        qa, reviewer, sessionStore, sender, logger);
});

// --- HTTP ---
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// --- Telegram Polling (local development) ---
var webhookUrl = builder.Configuration["TelegramBot:WebhookUrl"];
if (string.IsNullOrWhiteSpace(webhookUrl))
{
    builder.Services.AddHostedService<TelegramPollingService>();
}

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

var config  = app.Services.GetRequiredService<IConfiguration>();
app.Logger.LogInformation("ClaudeFarm ishga tushdi. API={Url}, Model={Model}",
    config["Anthropic:BaseUrl"], config["Anthropic:Model"]);

// --- Webhook o'rnatish (agar kerak bo'lsa) ---
if (!string.IsNullOrWhiteSpace(webhookUrl))
{
    var bot = app.Services.GetRequiredService<ITelegramBotClient>();
    await bot.SetWebhook(webhookUrl);
    app.Logger.LogInformation("Webhook o'rnatildi: {Url}", webhookUrl);
}
else
{
    app.Logger.LogInformation("Polling rejimi (local development)");
}

app.Run();
