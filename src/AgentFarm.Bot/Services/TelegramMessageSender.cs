using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace AgentFarm.Bot.Services;

/// <summary>
/// Telegram ga xabar yuboradi. Har agent o'z natijasini shu orqali yuboradi.
/// </summary>
public sealed class TelegramMessageSender : ITelegramMessageSender
{
    private readonly ITelegramBotClient _botClient;
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
            await _botClient.SendMessage(
                chatId:    message.ChatId,
                text:      message.FormattedText,
                parseMode: message.UseMarkdown ? ParseMode.MarkdownV2 : null,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram xabar yuborishda xato. ChatId={ChatId}", message.ChatId);
            // Markdown parse xatosi bo'lsa plain text bilan qayta urinib ko'ramiz
            await SendPlainFallbackAsync(message.ChatId, message.FormattedText, ct);
        }
    }

    public async Task SendTextAsync(long chatId, string text, bool useMarkdown = true, CancellationToken ct = default)
    {
        var message = new TelegramMessage
        {
            ChatId      = chatId,
            Text        = text,
            UseMarkdown = useMarkdown
        };
        await SendMessageAsync(message, ct);
    }

    private async Task SendPlainFallbackAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            // Markdown belgilarini olib tashlaymiz
            var plain = text
                .Replace("*", "")
                .Replace("_", "")
                .Replace("`", "")
                .Replace("\\", "");

            await _botClient.SendMessage(chatId, plain, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback xabar ham yuborilmadi. ChatId={ChatId}", chatId);
        }
    }
}
