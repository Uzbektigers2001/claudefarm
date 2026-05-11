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

    // Git integration
    public string? RepoName { get; set; }
    public string? BranchName { get; set; }
    public int? PullRequestNumber { get; set; }
    public string? PullRequestUrl { get; set; }
}
