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

// --- Anthropic Claude API ---
builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection("Anthropic"));

builder.Services.AddHttpClient<ClaudeApiClient>();

// --- Agentlar ---
builder.Services.AddSingleton<DeveloperAgent>();
builder.Services.AddSingleton<QAAgent>();
builder.Services.AddSingleton<ReviewerAgent>();

// --- Pipeline ---
builder.Services.AddSingleton<IAgentPipelineRunner, AgentPipelineRunner>();

// --- HTTP + Controllers ---
builder.Services.AddControllers()
    .AddNewtonsoftJson(); // Telegram.Bot uchun

var app = builder.Build();

app.MapControllers();

// --- Webhook ni o'rnatamiz ---
var webhookUrl = builder.Configuration["TelegramBot:WebhookUrl"];
if (!string.IsNullOrWhiteSpace(webhookUrl))
{
    var bot = app.Services.GetRequiredService<ITelegramBotClient>();
    await bot.SetWebhook(webhookUrl);
    app.Logger.LogInformation("Webhook o'rnatildi: {Url}", webhookUrl);
}

app.Run();
