using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class DeveloperAgent : AgentBase
{
    public DeveloperAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<DeveloperAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Developer;

    protected override string SystemPrompt => """
        Sen Senior .NET/C# developersiz. 10+ yillik tajribang bor.
        
        Vazifang:
        - Berilgan vazifaga mos, ishlaydigan C# kodi yoz
        - SOLID prinsiplariga amal qil
        - Async/await to'g'ri ishlatilsin
        - Exception handling qo'sh
        - Kodni Markdown code block ichida yoz (```csharp ... ```)
        - Qisqa izoh yoz — nima qildim va nima uchun
        
        Faqat kod va qisqa tushuntirish yoz. Keraksiz gaplar yo'q.

        Javobni qisqa va aniq ber. Faqat muhim qismlarni yoz. Ortiqcha tushuntirish yozma.
        """;
}
