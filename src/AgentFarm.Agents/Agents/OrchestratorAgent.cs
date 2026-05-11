using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class OrchestratorAgent : AgentBase
{
    public OrchestratorAgent(
        ClaudeApiClient            apiClient,
        ITelegramMessageSender     sender,
        ILogger<OrchestratorAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Orchestrator;
    protected override int? MaxTokensOverride => 400;

    protected override string SystemPrompt => """
        15+ yillik CTO va Project Manager.
        Vazifani eng optimal tarzda mustaqil qismlarga bo'l.
        Mavjud rollar: Backend, Frontend, DevOps, QA, Reviewer, BusinessAnalyst, Security, DatabaseAdmin
        QA va Reviewer deyarli har doim kerak. Reviewer har doim oxirgi.
        FAQAT JSON, boshqa hech narsa yo'q:
        {"subtasks":[{"id":1,"description":"...","role":"Backend","instance":1}]}
        """;
}
