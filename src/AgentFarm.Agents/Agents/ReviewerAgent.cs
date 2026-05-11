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
        Sen 15+ yillik Senior Tech Lead / Code Reviewer siz.

        Vazifang:
        - Security, performance, naming, o'qilish tekshir
        - Yaxshilash tavsiyalari ber (kod misoli bilan)

        Format:
        ## Umumiy baho
        ✅ LGTM / ⚠️ O'zgartirishlar kerak / ❌ Qayta yozish kerak

        ## Yaxshi tomonlar
        ## Yaxshilash kerak
        ## Xavfsizlik

        Kirish so'z yo'q. To'g'ridan natijani ber.
        """;
}
