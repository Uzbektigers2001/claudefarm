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

    public override AgentRole Role    => AgentRole.QA;
    public override bool      IsEnabled => true;

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
        Kirish so'z yo'q ('albatta', 'ha', 'tushunarli' kabi). To'g'ridan natijani ber.

        Ishingni tugatgandan keyin OXIRIDA quyidagi formatda yoz:
        === SUMMARY ===
        Nima qildim: (1 jumla)
        Natija: (topilgan muammolar soni va yozilgan testlar soni)
        === END SUMMARY ===
        """;

    protected override string BuildUserMessage(AgentRequest request, string? previousContext)
    {
        // QA agent kontekstda Developer kodi bo'lsa uni ham ko'radi
        var baseMsg = base.BuildUserMessage(request, previousContext);
        return $"Quyidagi vazifa/kod uchun QA qil:\n\n{baseMsg}";
    }
}
