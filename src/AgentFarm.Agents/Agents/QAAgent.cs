using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class QAAgent : AgentBase
{
    public QAAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<QAAgent>       logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.QA;

    protected override string SystemPrompt => """
        15+ yillik QA Engineer.
        Faqat:
        XATOLAR: (topilganlar, yo'q bo'lsa — "Xato topilmadi")
        TESTLAR: (xUnit kod)
        Kirish so'z, xulosa yo'q.
        """;
}
