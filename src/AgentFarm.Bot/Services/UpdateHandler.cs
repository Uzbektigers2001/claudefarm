using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AgentFarm.Bot.Services;

public sealed class UpdateHandler
{
    private readonly CommandRouter          _router;
    private readonly ITelegramMessageSender _sender;
    private readonly IAgentPipelineRunner   _pipeline;
    private readonly IEscalationStore       _escalationStore;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        CommandRouter          router,
        ITelegramMessageSender sender,
        IAgentPipelineRunner   pipeline,
        IEscalationStore       escalationStore,
        ILogger<UpdateHandler> logger)
    {
        _router          = router;
        _sender          = sender;
        _pipeline        = pipeline;
        _escalationStore = escalationStore;
        _logger          = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken ct = default)
    {
        if (update.Type == UpdateType.Message && update.Message is { } message)
            await HandleMessageAsync(message, ct);
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var text   = message.Text ?? string.Empty;

        // /help yoki /start
        if (text.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _sender.SendTextAsync(chatId, CommandRouter.HelpText, useMarkdown: false, ct);
            return;
        }

        // Kutilayotgan eskalatsiya bo'lsa — faqat eskalatsiya buyruqlari qabul qilinadi
        if (_escalationStore.HasPendingEscalation(chatId))
        {
            await HandleEscalationResponseAsync(chatId, text, ct);
            return;
        }

        // Oddiy buyruqlar
        var request = _router.Parse(message);
        if (request is null)
            return;

        await _sender.SendTextAsync(chatId, "⚙️ Vazifa qabul qilindi. Agentlar ishlamoqda...", useMarkdown: false, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                await _pipeline.RunAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline xatosi. TaskId={TaskId}", request.TaskId);
                await _sender.SendTextAsync(chatId, "❌ Xato yuz berdi. Iltimos qayta urinib ko'ring.", useMarkdown: false);
            }
        }, ct);
    }

    private async Task HandleEscalationResponseAsync(long chatId, string text, CancellationToken ct)
    {
        var esc = _escalationStore.GetEscalation(chatId);
        if (esc == null) return;

        // /stop
        if (text.Equals("/stop", StringComparison.OrdinalIgnoreCase))
        {
            esc.WaitingForResponse = false;
            esc.Tcs.TrySetResult(IEscalationStore.StopToken);
            await _sender.SendTextAsync(chatId, "🛑 Vazifa to'xtatildi. Yozilgan fayllar saqlanmoqda...", useMarkdown: false, ct);
            return;
        }

        // /skip
        if (text.Equals("/skip", StringComparison.OrdinalIgnoreCase))
        {
            esc.WaitingForResponse = false;
            esc.Tcs.TrySetResult(IEscalationStore.SkipToken);
            await _sender.SendTextAsync(chatId, "⏭️ Qadam o'tkazib yuborildi. Pipeline davom etmoqda...", useMarkdown: false, ct);
            return;
        }

        // /continue {ko'rsatma}
        if (text.StartsWith("/continue", StringComparison.OrdinalIgnoreCase))
        {
            var instructions = text.Length > "/continue".Length
                ? text["/continue".Length..].Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(instructions))
            {
                await _sender.SendTextAsync(chatId,
                    "Ko'rsatma kiriting: /continue {agentga ko'rsatma}", useMarkdown: false, ct);
                return;
            }

            esc.WaitingForResponse = false;
            esc.Tcs.TrySetResult(instructions);
            await _sender.SendTextAsync(chatId, "▶️ Ko'rsatma qabul qilindi. Pipeline davom etmoqda...", useMarkdown: false, ct);
            return;
        }

        // Boshqa xabar — eslatma
        await _sender.SendTextAsync(chatId,
            "Ferma javob kutmoqda.\n/continue {ko'rsatma}, /skip yoki /stop yozing.",
            useMarkdown: false, ct);
    }
}
