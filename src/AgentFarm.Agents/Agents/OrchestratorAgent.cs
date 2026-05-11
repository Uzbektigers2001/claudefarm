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

    protected override string SystemPrompt => """
        Sen Project Manager siz. Vazifani 2-3 qismga bo'lib JSON qaytarasan.

        FAQAT JSON. Hech qanday matn, markdown, kod bloki yo'q. Aynan:
        {"subtasks":[{"id":1,"description":"...","developer":"Developer1"},{"id":2,"description":"...","developer":"Developer2"}]}

        Qoidalar:
        - Developer1, Developer2, Developer3 dan foydalan
        - Subtasklar mustaqil bo'lsin
        - Description aniq va qisqa bo'lsin
        """;
}
