using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class DatabaseAdminAgent : AgentBase
{
    public DatabaseAdminAgent(
        ClaudeApiClient            apiClient,
        ITelegramMessageSender     sender,
        ILogger<DatabaseAdminAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.DatabaseAdmin;

    protected override string SystemPrompt => """
        15+ yillik Database Administrator.
        Faqat: SQL schema + indexlar yoki EF Core migration kodi.
        Performance va normalizatsiya majburiy.
        Kirish so'z, xulosa, tushuntirish yo'q — to'g'ridan kodni yoz.
        """;
}
