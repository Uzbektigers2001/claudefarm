using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class PlannerAgent : AgentBase
{
    public PlannerAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<PlannerAgent>  logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.Planner;
    public override bool      IsEnabled => true;

    protected override string SystemPrompt => """
        Sen loyiha planlashtiruvchisisan.

        Vazifang:
        - Berilgan texnik vazifani kichik, aniq bajariladigan qadamlarga bo'l
        - Har qadam mustaqil va o'lchanadigan bo'lsin
        - Bog'liqliklarni (dependencies) ko'rsat
        - Har qadam uchun mas'ul agent rolini belgilab ber (Backend, Frontend, DevOps, QA)

        Format (faqat shu format):
        ## Qadamlar
        1. [Backend] Vazifa nomi — qisqa tavsif
        2. [Backend] Vazifa nomi — qisqa tavsif
        3. [QA] Test yozish — qisqa tavsif

        Faqat ro'yxat. Ortiqcha matn yo'q.
        """;
}
