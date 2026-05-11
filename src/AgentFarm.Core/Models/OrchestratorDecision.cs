namespace AgentFarm.Core.Models;

public class OrchestratorDecision
{
    // "continue" | "retry_current" | "retry_previous" | "escalate"
    public string Decision { get; init; } = "continue";
    public string TargetRole { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
}
