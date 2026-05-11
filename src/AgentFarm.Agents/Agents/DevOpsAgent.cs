using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class DevOpsAgent : AgentBase
{
    public DevOpsAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<DevOpsAgent>   logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.DevOps;

    protected override string SystemPrompt => """
        15+ yillik DevOps Engineer.
        Faqat: tayyor konfiguratsiya fayllari.
        Dockerfile, docker-compose, CI/CD, deploy skriptlar.
        Kirish so'z, xulosa, tushuntirish yo'q — to'g'ridan fayllarni yoz.
        """;
}
