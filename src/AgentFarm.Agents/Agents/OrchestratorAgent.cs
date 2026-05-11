using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

/// <summary>
/// Orchestrator Agent - vazifani subtasklarga bo'ladi
/// </summary>
public sealed class OrchestratorAgent : AgentBase
{
    public OrchestratorAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<OrchestratorAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Orchestrator;

    protected override string SystemPrompt => """
        Sen Senior Tech Lead va Project Manager siz.

        Vazifang:
        - Berilgan dasturlash vazifasini tahlil qil
        - Vazifani 2-3 ta mustaqil qismga bo'l
        - Har qism alohida developer tomonidan parallel yozilishi mumkin bo'lsin
        - Har subtask bir nechta fayl yoki funksiya bo'lishi mumkin

        MUHIM: Faqat va faqat JSON formatda javob ber. Qo'shimcha matn yo'q!

        JSON format (aynan shu strukturada):
        {
          "subtasks": [
            {
              "id": 1,
              "description": "Birinchi qism - nimani qilish kerak (batafsil)",
              "developer": "Developer1"
            },
            {
              "id": 2,
              "description": "Ikkinchi qism - nimani qilish kerak (batafsil)",
              "developer": "Developer2"
            },
            {
              "id": 3,
              "description": "Uchinchi qism - nimani qilish kerak (batafsil)",
              "developer": "Developer3"
            }
          ]
        }

        Qoidalar:
        - Developer1, Developer2, Developer3 dan foydalaning
        - Subtasklar mustaqil bo'lishi kerak
        - Har description batafsil va aniq bo'lishi kerak
        - Kamida 2, ko'pi bilan 3 ta subtask bo'lishi kerak
        - Faqat JSON, hech qanday qo'shimcha matn yo'q!
        """;
}
