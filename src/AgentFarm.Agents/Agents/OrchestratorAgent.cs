using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class OrchestratorAgent : AgentBase
{
    public OrchestratorAgent(
        ClaudeApiClient            apiClient,
        ITelegramMessageSender     sender,
        ILogger<OrchestratorAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Orchestrator;

    // Orchestrator faqat JSON qaytaradi — 400 token yetarli
    protected override int? MaxTokensOverride => 400;

    protected override string SystemPrompt => """
        Sen Project Manager siz. Vazifani tahlil qilib:
        1. Kerakli rollarni va har roldan nechta kerakligini belgilaasan
        2. Har mutaxassis uchun aniq subtask yozasan

        Mavjud rollar: Backend, Frontend, DevOps, QA, Reviewer, BusinessAnalyst, Security, DatabaseAdmin

        FAQAT JSON. Hech qanday matn, markdown, kod bloki yo'q. Aynan:
        {
          "subtasks": [
            {"id":1,"description":"JWT service yoz","role":"Backend","instance":1},
            {"id":2,"description":"Login endpoint yoz","role":"Backend","instance":2},
            {"id":3,"description":"React login form yoz","role":"Frontend","instance":1},
            {"id":4,"description":"Testlar yoz","role":"QA","instance":1},
            {"id":5,"description":"Kodni review qil","role":"Reviewer","instance":1}
          ]
        }

        Qoidalar:
        - Har subtask mustaqil bo'lsin
        - Bir roldan nechta kerak bo'lsa — instance raqami oshadi (1, 2, 3...)
        - QA va Reviewer faqat bittadan bo'ladi (instance:1)
        - Reviewer har doim oxirgi
        - Maksimal 6 ta subtask
        - Faqat zarur rollarni ol
        """;
}
