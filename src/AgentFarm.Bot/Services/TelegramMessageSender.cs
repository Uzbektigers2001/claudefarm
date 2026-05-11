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
            if (message.UseMarkdown)
            {
                await _botClient.SendMessage(
                    chatId:            message.ChatId,
                    text:              message.FormattedText,
                    parseMode:         ParseMode.MarkdownV2,
                    cancellationToken: ct);
            }
            else
            {
                await _botClient.SendMessage(
                    chatId:            message.ChatId,
                    text:              message.FormattedText,
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MarkdownV2 bilan yuborishda xato, plain text ga o'tamiz. ChatId={ChatId}", message.ChatId);
            await SendPlainAsync(message.ChatId, StripMarkdown(message.FormattedText), ct);
        }
    }

    public async Task SendTextAsync(long chatId, string text, bool useMarkdown = true, CancellationToken ct = default)
    {
        await SendMessageAsync(new TelegramMessage
        {
            ChatId      = chatId,
            Text        = text,
            UseMarkdown = useMarkdown
        }, ct);
    }

    private async Task SendPlainAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            await _botClient.SendMessage(chatId, text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plain text ham yuborilmadi. ChatId={ChatId}", chatId);
        }
    }

    private static string StripMarkdown(string text) =>
        text.Replace("\\.", ".").Replace("\\!", "!").Replace("\\-", "-")
            .Replace("\\(", "(").Replace("\\)", ")").Replace("\\[", "[")
            .Replace("\\]", "]").Replace("\\*", "*").Replace("\\_", "_")
            .Replace("\\`", "`").Replace("\\>", ">").Replace("\\#", "#")
            .Replace("\\+", "+").Replace("\\=", "=").Replace("\\|", "|")
            .Replace("\\{", "{").Replace("\\}", "}").Replace("\\~", "~")
            .Replace("\\\\", "\\");
}
