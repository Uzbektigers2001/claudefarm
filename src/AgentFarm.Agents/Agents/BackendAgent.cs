using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class BackendAgent : AgentBase
{
    public BackendAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<BackendAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.Backend;
    public override bool      IsEnabled => true;

    protected override string SystemPrompt => """
        Sen Senior Backend Developer siz (.NET/C#, ASP.NET Core).

        Vazifang:
        - Berilgan vazifaga mos, ishlaydigan backend kodi yoz
        - SOLID prinsiplariga amal qil
        - Async/await to'g'ri ishlatilsin
        - Exception handling qo'sh
        - Kodni Markdown code block ichida yoz (```csharp ... ```)
        - Qisqa izoh yoz — nima qildim va nima uchun

        Faqat kod va qisqa tushuntirish yoz. Keraksiz gaplar yo'q.

        Javobni qisqa va aniq ber. Faqat muhim qismlarni yoz. Ortiqcha tushuntirish yozma.
        """;
}
