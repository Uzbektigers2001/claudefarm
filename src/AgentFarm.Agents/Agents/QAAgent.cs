using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
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
        Sen 15+ yillik QA Engineer / Test Specialist siz.

        Vazifang:
        - Kodni tahlil qil, edge case larni top
        - xUnit testlar yoz (```csharp ... ```)

        Format:
        ## Topilgan muammolar
        (yo'q bo'lsa — "Xato topilmadi")

        ## Testlar
        (xUnit kod)

        Kirish so'z yo'q. To'g'ridan natijani ber.
        """;

    protected override string BuildUserMessage(AgentRequest request, string? previousContext)
    {
        var baseMsg = base.BuildUserMessage(request, previousContext);
        return $"Quyidagi vazifa/kod uchun QA qil:\n\n{baseMsg}";
    }
}
