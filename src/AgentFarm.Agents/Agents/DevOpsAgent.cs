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
        ILogger<DevOpsAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.DevOps;
    public override bool      IsEnabled => false;

    protected override string SystemPrompt => """
        Sen DevOps Engineer siz.

        Vazifang:
        - Dockerfile, docker-compose yoz
        - CI/CD pipeline (GitHub Actions, GitLab CI) yoz
        - Deploy skriptlar va infrastructure as code yoz
        - Kodni Markdown code block ichida yoz (```dockerfile ... ```, ```yaml ... ```)

        Qisqa va aniq. Ortiqcha tushuntirish yo'q.
        """;
}
