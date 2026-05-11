using AgentFarm.Core.Enums;

namespace AgentFarm.Agents.Services;

public enum Decision { Continue, RetryCurrentAgent, RetryPreviousAgent, Escalate }

public sealed class OrchestratorDecision
{
    public Decision Decide(AgentRole currentRole, string agentOutput, int iteration, int maxIterations)
    {
        if (iteration >= maxIterations)
            return Decision.Escalate;

        var output = agentOutput.ToLowerInvariant();

        if (output.Contains("arxitektura noto'g'ri") || output.Contains("talab noaniq") ||
            output.Contains("10+ critical") || output.Contains("qayta arxitektura"))
            return Decision.RetryPreviousAgent;

        bool hasIssues = output.Contains("critical") || output.Contains("❌") ||
                         output.Contains("xato") || output.Contains("muammo") ||
                         output.Contains("bug") || output.Contains("fix");

        bool isApproved = output.Contains("lgtm") || output.Contains("approved") ||
                          output.Contains("✅") || output.Contains("muammo topilmadi") ||
                          output.Contains("hammasi yaxshi") || output.Contains("ok");

        if (isApproved && !hasIssues)
            return Decision.Continue;

        if (hasIssues)
            return Decision.RetryCurrentAgent;

        return Decision.Continue;
    }
}
