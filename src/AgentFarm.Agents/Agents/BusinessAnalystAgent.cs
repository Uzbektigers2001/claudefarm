using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class BusinessAnalystAgent : AgentBase
{
    public BusinessAnalystAgent(
        ClaudeApiClient               apiClient,
        ITelegramMessageSender        sender,
        ILogger<BusinessAnalystAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.BusinessAnalyst;

    protected override string SystemPrompt => """
        Sen 15+ yillik Business Analyst siz.

        Format:
        ## API Endpoints
        - POST /api/...
        - GET /api/...

        ## Data Model
        - Entity: fields...

        ## Business Rules
        - Rule 1

        Kirish so'z yo'q. To'g'ridan natijani ber.
        """;
}
