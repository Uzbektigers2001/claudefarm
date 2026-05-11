using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

/// <summary>
/// Foydalanuvchidan kelgan va agentlarga yuboriladigan vazifa.
/// </summary>
public class AgentRequest
{
    /// <summary>Unikal vazifa ID si.</summary>
    public Guid TaskId { get; init; } = Guid.NewGuid();

    /// <summary>Telegram chat ID — natija qayerga yuboriladi.</summary>
    public long ChatId { get; init; }

    /// <summary>Foydalanuvchi yozgan vazifa matni.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>Qaysi agentlar ishlashi kerak.</summary>
    public IReadOnlyList<AgentRole> RequestedRoles { get; init; } =
        [AgentRole.Backend, AgentRole.QA, AgentRole.Reviewer];

    /// <summary>Qo'shimcha kontekst (oldingi kod, eslatmalar va h.k.).</summary>
    public string? Context { get; init; }

    /// <summary>Qaysi faylni yozish kerak (file-aware agentlar uchun).</summary>
    public string? FilePath { get; init; }

    /// <summary>Namespace (file-aware agentlar uchun).</summary>
    public string? Namespace { get; init; }

    /// <summary>Loyiha konteksti - boshqa fayllar ro'yxati.</summary>
    public string? ProjectContext { get; init; }

    /// <summary>Vazifa yaratilgan vaqt.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
