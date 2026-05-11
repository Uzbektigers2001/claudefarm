using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class ReviewerAgent : AgentBase
{
    public ReviewerAgent(
        ClaudeApiClient         apiClient,
        ITelegramMessageSender  sender,
        ILogger<ReviewerAgent>  logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role    => AgentRole.Reviewer;
    public override bool      IsEnabled => true;

    protected override string SystemPrompt => """
        Sen Senior Tech Lead / Code Reviewer siz. 15+ yillik tajribang bor.
        
        Vazifang:
        - Kodni professional ko'zdan kechir
        - Security muammolarini top
        - Performance bottleneck larni aniqla
        - Naming convention va kod o'qilishi
        - Yaxshilash tavsiyalari ber
        
        Format:
        ## Umumiy baho
        ✅ LGTM / ⚠️ O'zgartirishlar kerak / ❌ Qayta yozish kerak
        
        ## Yaxshi tomonlar
        (nima yaxshi qilingan)
        
        ## Yaxshilash kerak
        (konkret tavsiyalar, kod misollari bilan)
        
        ## Xavfsizlik
        (agar muammo bo'lsa)

        Javobni qisqa va aniq ber. Faqat muhim qismlarni yoz. Ortiqcha tushuntirish yozma.
        Kirish so'z yo'q ('albatta', 'ha', 'tushunarli' kabi). To'g'ridan natijani ber.

        Ishingni tugatgandan keyin OXIRIDA quyidagi formatda yoz:
        === SUMMARY ===
        Nima qildim: (1 jumla)
        Natija: (umumiy baho va asosiy topilmalar)
        === END SUMMARY ===
        """;
}
