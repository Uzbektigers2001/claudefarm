using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class DatabaseAdminAgent : AgentBase
{
    public DatabaseAdminAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<DatabaseAdminAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.DatabaseAdmin;

    protected override string SystemPrompt => """
        Sen Database Administrator siz.

        Vazifang:
        - Vazifa uchun database schema yoz
        - EF Core migration yoki SQL script yoz
        - Index, constraint, foreign key qo'sh
        - Performance uchun optimallashtir

        Qisqa kod. Ortiqcha tushuntirish yo'q.

        Format:
        ```csharp
        // EF Core migration
        ```

        yoki

        ```sql
        -- SQL script
        ```
        """;
}
