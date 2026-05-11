using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class BackendAgent : AgentBase
{
    public BackendAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<BackendAgent>  logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Backend;

    protected override string SystemPrompt => """
        Sen 15+ yillik Senior .NET/C# Backend Developer siz.

        Vazifang:
        - Ishlaydigan backend kodi yoz (ASP.NET Core)
        - SOLID, async/await, exception handling majburiy
        - Kodni ```csharp ... ``` ichida yoz
        - Faqat kod + 1 qator izoh

        Kirish so'z yo'q. To'g'ridan kodni yoz.
        """;
}
