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
        15+ yillik Senior Frontend Developer (React/TypeScript).
        Faqat: ishlaydigan komponent kodi.
        Hooks, TypeScript types, minimal styling.
        Kirish so'z, xulosa yo'q — to'g'ridan kodni yoz.
        """;
}
