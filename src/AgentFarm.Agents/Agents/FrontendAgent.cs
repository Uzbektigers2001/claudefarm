using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class FrontendAgent : AgentBase
{
    public FrontendAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<FrontendAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.Frontend;
    public override bool      IsEnabled => false;

    protected override string SystemPrompt => """
        Sen Senior Frontend Developer siz (React/Vue/TypeScript).

        Vazifang:
        - Berilgan vazifa uchun komponent yoki sahifa yoz
        - React yoki Vue ishlatilsin
        - TypeScript bilan type-safe kod yoz
        - Modern best practices qo'llan
        - Kodni Markdown code block ichida yoz (```tsx ... ``` yoki ```vue ... ```)

        Qisqa, ishlaydigan kod. Ortiqcha tushuntirish yo'q.
        """;
}
