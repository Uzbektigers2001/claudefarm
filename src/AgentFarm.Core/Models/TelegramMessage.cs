using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

/// <summary>
/// Telegram ga yuboriladigan xabar — har agent o'z xabarini yuboradi.
/// </summary>
public class TelegramMessage
{
    public long ChatId { get; init; }
    public string Text { get; init; } = string.Empty;

    /// <summary>Qaysi agent xabar yubormoqda (prefiks uchun).</summary>
    public AgentRole? SenderRole { get; init; }

    /// <summary>Markdown parse mode ishlatilsinmi.</summary>
    public bool UseMarkdown { get; init; } = true;

    /// <summary>Telegram xabari prefiks bilan to'liq matn.</summary>
    public string FormattedText => SenderRole.HasValue
        ? $"*\\[{RoleLabel(SenderRole.Value)}\\]*\n\n{Text}"
        : Text;

    private static string RoleLabel(AgentRole role) => role switch
    {
        AgentRole.Backend         => "Backend",
        AgentRole.Frontend        => "Frontend",
        AgentRole.DevOps          => "DevOps",
        AgentRole.QA              => "QA",
        AgentRole.Reviewer        => "Reviewer",
        AgentRole.BusinessAnalyst => "Business Analyst",
        AgentRole.Security        => "Security",
        AgentRole.DatabaseAdmin   => "Database Admin",
        AgentRole.Orchestrator    => "AgentFarm",
        _                         => role.ToString()
    };
}
