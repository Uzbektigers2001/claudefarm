using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

public class SubTask
{
    public Guid SubTaskId { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public AgentRole AssignedTo { get; init; }
    public int Instance { get; init; } = 1;
    public string Description { get; init; } = string.Empty;
    public string? Code { get; set; }
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Pending;

    /// <summary>
    /// Display name: "Backend-1", "Frontend-2", etc.
    /// </summary>
    public string DisplayName => $"{AssignedTo}-{Instance}";
}

public enum SubTaskStatus
{
    Pending,
    InProgress,
    Done,
    Failed
}
