using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

public class AgentMessage
{
    public AgentRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Guid? SubTaskId { get; init; }
}
