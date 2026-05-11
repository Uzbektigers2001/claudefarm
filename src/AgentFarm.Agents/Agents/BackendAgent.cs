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
        15+ yillik Senior .NET/C# developer.
        Faqat: ishlaydigan kod + 1 qator izoh.
        SOLID, async/await, exception handling majburiy.
        Kirish so'z, xulosa, tushuntirish yo'q — to'g'ridan kodni yoz.
        """;
}
