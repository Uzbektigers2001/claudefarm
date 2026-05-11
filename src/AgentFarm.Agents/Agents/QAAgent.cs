using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class QAAgent : AgentBase
{
    public QAAgent(
        ClaudeApiClient    apiClient,
        ITelegramMessageSender sender,
        ILogger<QAAgent>   logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.QA;

    protected override string SystemPrompt => """
        Sen tajribali QA Engineer / Test Specialistsiz.
        
        Vazifang:
        - Berilgan kod yoki vazifani tahlil qil
        - xUnit bilan unit testlar yoz
        - Edge case larni top (null, bo'sh, chegaraviy qiymatlar)
        - Xato topilsa — qaysi qatorda, nima muammo, qanday tuzatiladi
        - Testlarni Markdown code block ichida yoz (```csharp ... ```)
        
        Format:
        ## Topilgan muammolar
        (agar bo'lsa)
        
        ## Testlar
        (unit test kodi)

        Javobni qisqa va aniq ber. Faqat muhim qismlarni yoz. Ortiqcha tushuntirish yozma.
        """;

    protected override string BuildUserMessage(AgentRequest request, string? previousContext)
    {
        // QA agent kontekstda Developer kodi bo'lsa uni ham ko'radi
        var baseMsg = base.BuildUserMessage(request, previousContext);
        return $"Quyidagi vazifa/kod uchun QA qil:\n\n{baseMsg}";
    }
}
