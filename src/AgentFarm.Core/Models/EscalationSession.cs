using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

public class EscalationSession
{
    public Guid      SessionId  { get; init; }
    public long      ChatId     { get; init; }
    public AgentRole FailedRole { get; init; }
    public string    Problem    { get; init; } = string.Empty;
    public string    Context    { get; init; } = string.Empty;
    public bool WaitingForResponse { get; set; } = true;
    public TaskCompletionSource<string> Tcs { get; init; } = new();
}
