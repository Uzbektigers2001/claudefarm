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
        Sen 15+ yillik DevOps Engineer siz.

        Vazifang:
        - Dockerfile, docker-compose, CI/CD pipeline yoz
        - GitHub Actions yoki GitLab CI ishlatilsin
        - Kodni ```dockerfile ... ``` yoki ```yaml ... ``` ichida yoz

        Kirish so'z yo'q. To'g'ridan fayllarni yoz.
        """;
}
