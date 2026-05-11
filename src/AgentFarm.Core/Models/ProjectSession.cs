using AgentFarm.Core.Enums;

namespace AgentFarm.Core.Models;

public class ProjectSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public string OriginalTask { get; init; } = string.Empty;
    public long ChatId { get; init; }
    public List<SubTask> SubTasks { get; set; } = new();
    public List<AgentMessage> History { get; set; } = new();
    public string? FinalResult { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Planning;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // SharedContext
    public Dictionary<string, string> Files { get; set; } = new();
    public string ArchitectPlanJson { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public List<string> BuildErrors { get; set; } = new();
    public int CurrentIteration { get; set; } = 0;

    // Git integration
    public string? RepoName { get; set; }
    public string? BranchName { get; set; }
    public int? PullRequestNumber { get; set; }
    public string? PullRequestUrl { get; set; }
}
