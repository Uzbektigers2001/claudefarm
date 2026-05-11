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

// --- GitHub Integration ---
builder.Services.Configure<GitHubOptions>(
    builder.Configuration.GetSection("GitHub"));

builder.Services.AddSingleton<GitHubService>();
builder.Services.AddSingleton<ProjectRepoService>();

// --- Session Store ---
builder.Services.AddSingleton<InMemorySessionStore>();

// --- Agentlar ---
builder.Services.AddSingleton<PlannerAgent>();
builder.Services.AddSingleton<AnalystAgent>();
builder.Services.AddSingleton<ArchitectAgent>();
builder.Services.AddSingleton<BackendAgent>();
builder.Services.AddSingleton<FrontendAgent>();
builder.Services.AddSingleton<DevOpsAgent>();
builder.Services.AddSingleton<BusinessAnalystAgent>();
builder.Services.AddSingleton<SecurityAgent>();
builder.Services.AddSingleton<DatabaseAdminAgent>();
builder.Services.AddSingleton<QAAgent>();
builder.Services.AddSingleton<ReviewerAgent>();

// --- Project Builder & Code Writer Services ---
builder.Services.AddSingleton<ProjectBuilderService>();
builder.Services.AddSingleton<CodeWriterService>();
builder.Services.AddSingleton<OrchestratorDecision>();

// --- Pipeline ---
builder.Services.AddSingleton<IAgentPipelineRunner>(sp =>
    new OrchestratorPipelineRunner(
        sp.GetRequiredService<PlannerAgent>(),
        sp.GetRequiredService<AnalystAgent>(),
        sp.GetRequiredService<ArchitectAgent>(),
        sp.GetRequiredService<BackendAgent>(),
        sp.GetRequiredService<FrontendAgent>(),
        sp.GetRequiredService<DevOpsAgent>(),
        sp.GetRequiredService<BusinessAnalystAgent>(),
        sp.GetRequiredService<SecurityAgent>(),
        sp.GetRequiredService<DatabaseAdminAgent>(),
        sp.GetRequiredService<QAAgent>(),
        sp.GetRequiredService<ReviewerAgent>(),
        sp.GetRequiredService<ProjectBuilderService>(),
        sp.GetRequiredService<CodeWriterService>(),
        sp.GetRequiredService<InMemorySessionStore>(),
        sp.GetRequiredService<ITelegramMessageSender>(),
        sp.GetRequiredService<GitHubService>(),
        sp.GetRequiredService<ProjectRepoService>(),
        sp.GetRequiredService<OrchestratorDecision>(),
        sp.GetRequiredService<ILogger<OrchestratorPipelineRunner>>()));

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
