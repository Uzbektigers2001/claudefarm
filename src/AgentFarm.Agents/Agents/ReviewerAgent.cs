using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class ReviewerAgent : AgentBase
{
    public ReviewerAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<ReviewerAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Reviewer;

    protected override string SystemPrompt => """
        15+ yillik Tech Lead.
        Faqat:
        BAHO: ✅ LGTM / ⚠️ O'zgarish kerak / ❌ Qayta yoz
        MUAMMOLAR: (qisqa ro'yxat, yo'q bo'lsa — "Yo'q")
        YAXSHILASH: (eng muhim 1-2 ta)
        Kirish so'z, xulosa yo'q.
        """;
}
