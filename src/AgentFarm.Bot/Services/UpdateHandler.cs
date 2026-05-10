using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AgentFarm.Bot.Services;

/// <summary>
/// Telegram dan kelgan har bir update ni qayta ishlaydi.
/// </summary>
public sealed class UpdateHandler
{
    private readonly CommandRouter          _router;
    private readonly ITelegramMessageSender _sender;
    private readonly IAgentPipelineRunner   _pipeline;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        CommandRouter          router,
        ITelegramMessageSender sender,
        IAgentPipelineRunner   pipeline,
        ILogger<UpdateHandler> logger)
    {
        _router   = router;
        _sender   = sender;
        _pipeline = pipeline;
        _logger   = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken ct = default)
    {
        if (update.Type == UpdateType.Message && update.Message is { } message)
            await HandleMessageAsync(message, ct);
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        // /help
        if (message.Text?.Equals("/help", StringComparison.OrdinalIgnoreCase) == true ||
            message.Text?.Equals("/start", StringComparison.OrdinalIgnoreCase) == true)
        {
            await _sender.SendTextAsync(chatId, CommandRouter.HelpText, useMarkdown: true, ct);
            return;
        }

        // Buyruqni parse qilamiz
        var request = _router.Parse(message);
        if (request is null)
            return;

        // "Boshlandi..." xabari
        await _sender.SendTextAsync(
            chatId,
            $"⚙️ Vazifa qabul qilindi\\. Agentlar ishlamoqda\\.\\.\\.",
            useMarkdown: true,
            ct);

        // Pipeline ni ishga tushiramiz (background da)
        _ = Task.Run(async () =>
        {
            try
            {
                await _pipeline.RunAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline xatosi. TaskId={TaskId}", request.TaskId);
                await _sender.SendTextAsync(
                    chatId,
                    "❌ Xato yuz berdi\\. Iltimos qayta urinib ko'ring\\.",
                    useMarkdown: true);
            }
        }, ct);
    }
}
