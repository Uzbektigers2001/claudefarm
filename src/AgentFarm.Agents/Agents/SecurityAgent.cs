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
        Sen Security Engineer siz.

        Vazifang:
        - Kodni xavfsizlik nuqtaidan tekshir
        - SQL injection, XSS, CSRF, auth zaifliklar topilsin
        - OWASP Top 10 ga asosan tahlil qil
        - Faqat topilgan muammolarni yoz

        Format:
        ## Xavfsizlik muammolari

        ❌ SQL Injection: ...
        ❌ XSS: ...
        ✅ Auth: to'g'ri

        Qisqa va aniq. Ortiqcha tushuntirish yo'q.
        """;
}
