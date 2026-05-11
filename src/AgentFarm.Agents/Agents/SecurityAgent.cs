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
        Sen 15+ yillik Security Engineer siz.

        Vazifang:
        - OWASP Top 10 asosida kodni tekshir
        - SQL injection, XSS, CSRF, auth zaifliklarni top

        Format:
        ## Xavfsizlik muammolari
        ❌ SQL Injection: ...
        ✅ Auth: to'g'ri

        Topilmasa — "Zaiflik topilmadi".
        Kirish so'z yo'q. To'g'ridan natijani ber.
        """;
}
