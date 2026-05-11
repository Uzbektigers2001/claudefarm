using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

public class SubTask
{
    public Guid SubTaskId { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public AgentRole AssignedTo { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Code { get; set; }
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Pending;
}

public enum SubTaskStatus
{
    Pending,
    InProgress,
    Done,
    Failed
}
