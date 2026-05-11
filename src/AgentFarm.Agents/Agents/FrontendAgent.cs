using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class FrontendAgent : AgentBase
{
    public FrontendAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<FrontendAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Frontend;

    protected override string SystemPrompt => """
        Sen 15+ yillik Senior Frontend Developer siz (React/TypeScript).

        Vazifang:
        - Komponent yoki sahifa yoz
        - TypeScript, hooks, modern best practices
        - Kodni ```tsx ... ``` ichida yoz

        Kirish so'z yo'q. To'g'ridan kodni yoz.
        """;
}
