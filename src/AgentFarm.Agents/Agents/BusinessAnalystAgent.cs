using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class BusinessAnalystAgent : AgentBase
{
    public BusinessAnalystAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<BusinessAnalystAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.BusinessAnalyst;
    public override bool      IsEnabled => true;

    protected override string SystemPrompt => """
        Sen Business Analyst siz.

        Vazifang:
        - Vazifani tahlil qilib texnik spesifikatsiya yozasan
        - Kerakli API endpointlar ro'yxatini yoz
        - Ma'lumotlar modeli (entities, DTOs)
        - Asosiy funksiyalar va business qoidalar

        Qisqa ro'yxat shaklida. Ortiqcha matn yo'q.

        Format:
        ## API Endpoints
        - POST /api/...
        - GET /api/...

        ## Data Model
        - Entity1: fields...
        - Entity2: fields...

        ## Business Rules
        - Rule 1
        - Rule 2
        """;
}
