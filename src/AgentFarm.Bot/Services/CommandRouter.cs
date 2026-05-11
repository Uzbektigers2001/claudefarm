using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace AgentFarm.Bot.Services;

/// <summary>
/// Telegram dan kelgan xabarni tahlil qilib AgentRequest ga aylantiradi.
/// </summary>
public sealed class CommandRouter
{
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(ILogger<CommandRouter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Telegram xabaridan AgentRequest yasaydi.
    /// null qaytarsa — bu buyruq Agent Farm uchun emas.
    /// </summary>
    public AgentRequest? Parse(Message message)
    {
        if (message.Text is not { } text)
            return null;

        var chatId = message.Chat.Id;

        // /task <vazifa matni>
        if (text.StartsWith("/task ", StringComparison.OrdinalIgnoreCase))
        {
            var prompt = text["/task ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("Bo'sh /task buyrug'i. ChatId={ChatId}", chatId);
                return null;
            }

            _logger.LogInformation("Yangi vazifa. ChatId={ChatId}, Prompt={Prompt}", chatId, prompt);

            return new AgentRequest
            {
                ChatId         = chatId,
                Prompt         = prompt,
                RequestedRoles = [AgentRole.Backend, AgentRole.QA, AgentRole.Reviewer]
            };
        }

        // /dev <vazifa> — faqat backend developer
        if (text.StartsWith("/dev ", StringComparison.OrdinalIgnoreCase))
        {
            var prompt = text["/dev ".Length..].Trim();
            return string.IsNullOrWhiteSpace(prompt) ? null : new AgentRequest
            {
                ChatId         = chatId,
                Prompt         = prompt,
                RequestedRoles = [AgentRole.Backend]
            };
        }

        // /review <kod> — faqat reviewer
        if (text.StartsWith("/review ", StringComparison.OrdinalIgnoreCase))
        {
            var prompt = text["/review ".Length..].Trim();
            return string.IsNullOrWhiteSpace(prompt) ? null : new AgentRequest
            {
                ChatId         = chatId,
                Prompt         = prompt,
                RequestedRoles = [AgentRole.Reviewer]
            };
        }

        // Eskalatsiya buyruqlari — UpdateHandler to'g'ridan-to'g'ri qayta ishlaydi
        // /continue, /skip, /stop — bu yerda null qaytaradi (UpdateHandler o'zi hal qiladi)

        return null;
    }

    /// <summary>
    /// Qo'llab-quvvatlanadigan buyruqlar ro'yxati.
    /// </summary>
    public static string HelpText => """
        *ClaudeFarm Bot* 🤖

        *Buyruqlar:*
        `/task <vazifa>` — Developer \+ QA \+ Reviewer
        `/dev <vazifa>`  — Faqat Developer
        `/review <kod>`  — Faqat Reviewer
        `/help`          — Ushbu yordam

        *Misol:*
        `/task Login funksiyasini C\# da yoz`
        """;
}
