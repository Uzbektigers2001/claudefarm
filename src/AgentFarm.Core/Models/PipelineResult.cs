using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

/// <summary>
/// Barcha agentlar tugagandan keyin yig'ilgan yakuniy natija.
/// </summary>
public class PipelineResult
{
    public Guid TaskId { get; init; }
    public long ChatId { get; init; }
    public string OriginalPrompt { get; init; } = string.Empty;

    /// <summary>Har bir agentning javobi.</summary>
    public IReadOnlyList<AgentResponse> AgentResponses { get; init; } = [];

    /// <summary>Umumiy sarflangan tokenlar.</summary>
    public int TotalTokensUsed => AgentResponses.Sum(r => r.TokensUsed);

    /// <summary>Umumiy vaqt.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Hamma agent muvaffaqiyatli tugadimi.</summary>
    public bool IsFullySuccessful =>
        AgentResponses.All(r => r.Status == AgentStatus.Completed);

    /// <summary>Muayyan rol javobi.</summary>
    public AgentResponse? GetResponse(AgentRole role) =>
        AgentResponses.FirstOrDefault(r => r.Role == role);
}
