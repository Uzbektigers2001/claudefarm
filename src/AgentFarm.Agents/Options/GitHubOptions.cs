namespace AgentFarm.Agents.Options;

public sealed class GitHubOptions
{
    public string Token { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string DefaultBranch { get; init; } = "main";
}
