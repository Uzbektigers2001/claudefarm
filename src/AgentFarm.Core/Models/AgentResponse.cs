using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

/// <summary>
/// Bitta agentning bajargan ishi natijasi.
/// </summary>
public class AgentResponse
{
    /// <summary>Qaysi vazifaga tegishli.</summary>
    public Guid TaskId { get; init; }

    /// <summary>Javob bergan agent roli.</summary>
    public AgentRole Role { get; init; }

    /// <summary>Agentning yakuniy javobi (Markdown formatida).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Bajarilish holati.</summary>
    public AgentStatus Status { get; init; } = AgentStatus.Completed;

    /// <summary>Xato bo'lsa xabar.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Necha token sarflandi.</summary>
    public int TokensUsed { get; init; }

    /// <summary>Qancha vaqt ketdi.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Javob tayyor bo'lgan vaqt.</summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    // --- Qulay factory metodlar ---

    public static AgentResponse Success(
        Guid taskId,
        AgentRole role,
        string content,
        int tokensUsed,
        TimeSpan duration) => new()
    {
        TaskId     = taskId,
        Role       = role,
        Content    = content,
        Status     = AgentStatus.Completed,
        TokensUsed = tokensUsed,
        Duration   = duration
    };

    public static AgentResponse Failure(
        Guid taskId,
        AgentRole role,
        string errorMessage,
        TimeSpan duration) => new()
    {
        TaskId       = taskId,
        Role         = role,
        Status       = AgentStatus.Failed,
        ErrorMessage = errorMessage,
        Duration     = duration
    };
}
