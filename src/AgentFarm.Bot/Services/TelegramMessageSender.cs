using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace AgentFarm.Bot.Services;

public sealed class TelegramMessageSender : ITelegramMessageSender
{
    private readonly ITelegramBotClient          _botClient;
    private readonly ILogger<TelegramMessageSender> _logger;

    public TelegramMessageSender(ITelegramBotClient botClient, ILogger<TelegramMessageSender> logger)
    {
        _botClient = botClient;
        _logger    = logger;
    }

    public async Task SendMessageAsync(TelegramMessage message, CancellationToken ct = default)
    {
        try
        {
            // Har doim plain text (ParseMode = null)
            await _botClient.SendMessage(
                chatId:            message.ChatId,
                text:              message.FormattedText,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xabar yuborishda xato. ChatId={ChatId}", message.ChatId);
        }
    }

    public async Task SendTextAsync(long chatId, string text, bool useMarkdown = false, CancellationToken ct = default)
    {
        await SendMessageAsync(new TelegramMessage
        {
            ChatId      = chatId,
            Text        = text,
            UseMarkdown = useMarkdown
        }, ct);
    }
}
