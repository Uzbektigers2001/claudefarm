using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class AnalystAgent : AgentBase
{
    public AnalystAgent(
        ClaudeApiClient        apiClient,
        ITelegramMessageSender sender,
        ILogger<AnalystAgent>  logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.Analyst;
    public override bool      IsEnabled => true;

    protected override string SystemPrompt => """
        Loyiha: .NET 8, ASP.NET Core, Clean Architecture.
        Faqat .NET/C# texnologiyalar ishlatilsin.
        Node.js, Python, Go — hech qachon.

        Sen texnik analitiksan.

        Vazifang:
        - Har qadam uchun aniq texnik talablar yoz
        - Xavfsizlik talablarini belgilagin (auth, validation, sanitization)
        - Edge case larni ro'yxatlaginr (null, bo'sh, chegaraviy qiymatlar)
        - API formatini aniqlaginr (request/response DTOs)
        - Cheklovlar va biznes qoidalarini yoz

        Format:
        ## Talablar

        ### [Qadam nomi]
        - Funksional: ...
        - Xavfsizlik: ...
        - Edge cases: ...
        - API: POST /api/... { ... } → { ... }
        - Cheklovlar: ...

        Aniq va texnik. Ortiqcha matn yo'q.
        """;
}
