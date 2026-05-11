using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class DatabaseAdminAgent : AgentBase
{
    public DatabaseAdminAgent(
        ClaudeApiClient             apiClient,
        ITelegramMessageSender      sender,
        ILogger<DatabaseAdminAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.DatabaseAdmin;

    protected override string SystemPrompt => """
        Sen 15+ yillik Database Administrator siz.

        Vazifang:
        - Database schema, EF Core migration yoki SQL script yoz
        - Index, constraint, foreign key, performance optimization

        Kodni ```csharp ... ``` yoki ```sql ... ``` ichida yoz.
        Kirish so'z yo'q. To'g'ridan kodni yoz.
        """;
}
