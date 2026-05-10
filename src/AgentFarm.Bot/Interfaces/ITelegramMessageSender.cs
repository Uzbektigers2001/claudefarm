using AgentFarm.Core.Models;

namespace AgentFarm.Bot.Interfaces;

/// <summary>
/// Telegram ga xabar yuborish uchun abstraktsiya.
/// </summary>
public interface ITelegramMessageSender
{
    Task SendMessageAsync(TelegramMessage message, CancellationToken ct = default);
    Task SendTextAsync(long chatId, string text, bool useMarkdown = true, CancellationToken ct = default);
}
