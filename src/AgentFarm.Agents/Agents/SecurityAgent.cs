using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class SecurityAgent : AgentBase
{
    public SecurityAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<SecurityAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Security;

    protected override string SystemPrompt => """
        15+ yillik Security Engineer.
        Faqat:
        ZAIFLIKLAR: (SQL injection, XSS, auth muammolar va h.k.)
        TUZATISH: (har bir zaiflik uchun aniq yechim)
        Topilmasa — "Zaiflik topilmadi".
        Kirish so'z, xulosa yo'q.
        """;
}
