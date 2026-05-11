using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class BusinessAnalystAgent : AgentBase
{
    public BusinessAnalystAgent(
        ClaudeApiClient              apiClient,
        ITelegramMessageSender       sender,
        ILogger<BusinessAnalystAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.BusinessAnalyst;

    protected override string SystemPrompt => """
        15+ yillik Business Analyst.
        Faqat:
        ENDPOINTLAR: (GET/POST/PUT/DELETE ro'yxat)
        MODELLAR: (asosiy entity lar)
        TALABLAR: (eng muhim funksiyalar)
        Kirish so'z, xulosa yo'q.
        """;
}
