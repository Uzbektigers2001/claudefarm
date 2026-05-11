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

// --- Claude / OmniRoute API ---
builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection("Anthropic"));

// HttpClient BaseUrl ni OmniRoute dan olamiz
builder.Services.AddHttpClient<ClaudeApiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromSeconds(120);
});

// --- Agentlar ---
builder.Services.AddSingleton<DeveloperAgent>();
builder.Services.AddSingleton<QAAgent>();
builder.Services.AddSingleton<ReviewerAgent>();

// --- Pipeline ---
builder.Services.AddSingleton<IAgentPipelineRunner, AgentPipelineRunner>();

// --- HTTP + Controllers ---
builder.Services.AddControllers();

// --- Health check ---
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

// --- Startup logi ---
var config  = app.Services.GetRequiredService<IConfiguration>();
var baseUrl = config["Anthropic:BaseUrl"] ?? "?";
var model   = config["Anthropic:Model"]   ?? "?";
app.Logger.LogInformation("ClaudeFarm ishga tushdi. API={BaseUrl}, Model={Model}", baseUrl, model);

// --- Webhook ni o'rnatamiz ---
var webhookUrl = builder.Configuration["TelegramBot:WebhookUrl"];
if (!string.IsNullOrWhiteSpace(webhookUrl))
{
    var bot = app.Services.GetRequiredService<ITelegramBotClient>();
    await bot.SetWebhook(webhookUrl);
    app.Logger.LogInformation("Webhook o'rnatildi: {Url}", webhookUrl);
}

app.Run();
