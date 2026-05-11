using AgentFarm.Agents.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace AgentFarm.Agents.Services;

/// <summary>
/// GitHub REST API bilan ishlash uchun servis
/// </summary>
public sealed class GitHubService
{
    private readonly GitHubClient _client;
    private readonly string _owner;
    private readonly string _defaultBranch;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(IOptions<GitHubOptions> options, ILogger<GitHubService> logger)
    {
        var opts = options.Value;
        _owner = opts.Owner;
        _defaultBranch = opts.DefaultBranch;
        _logger = logger;

        _client = new GitHubClient(new ProductHeaderValue("ClaudeFarm"))
        {
            Credentials = new Credentials(opts.Token)
        };
    }

    /// <summary>
    /// Repo mavjudligini tekshirish
    /// </summary>
    public async Task<bool> RepositoryExistsAsync(string repoName, CancellationToken ct = default)
    {
        try
        {
            await _client.Repository.Get(_owner, repoName);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Yangi branch yaratish
    /// </summary>
    public async Task<string> CreateBranchAsync(string repoName, string branchName, CancellationToken ct = default)
    {
        try
        {
            var repo = await _client.Repository.Get(_owner, repoName);
            var mainBranch = await _client.Repository.Branch.Get(_owner, repoName, _defaultBranch);
            var sha = mainBranch.Commit.Sha;

            var newBranch = await _client.Git.Reference.Create(_owner, repoName,
                new NewReference($"refs/heads/{branchName}", sha));

            _logger.LogInformation("✅ Branch yaratildi: {RepoName}/{BranchName}", repoName, branchName);
            return branchName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Branch yaratishda xato: {RepoName}/{BranchName}", repoName, branchName);
            throw;
        }
    }

    /// <summary>
    /// Faylni commit qilish
    /// </summary>
    public async Task CommitFileAsync(
        string repoName,
        string branch,
        string filePath,
        string content,
        string commitMessage,
        CancellationToken ct = default)
    {
        try
        {
            // Fayl mavjudligini tekshirish
            string? existingSha = null;
            try
            {
                var existingFile = await _client.Repository.Content.GetAllContentsByRef(_owner, repoName, filePath, branch);
                existingSha = existingFile[0].Sha;
            }
            catch (NotFoundException)
            {
                // Fayl mavjud emas
            }

            if (existingSha == null)
            {
                // Yangi fayl yaratish
                var createRequest = new CreateFileRequest(commitMessage, content, branch);
                await _client.Repository.Content.CreateFile(_owner, repoName, filePath, createRequest);
            }
            else
            {
                // Mavjud faylni yangilash
                var updateRequest = new UpdateFileRequest(commitMessage, content, existingSha)
                {
                    Branch = branch
                };
                await _client.Repository.Content.UpdateFile(_owner, repoName, filePath, updateRequest);
            }

            _logger.LogInformation("✅ Commit qilindi: {RepoName}/{Branch}/{FilePath}", repoName, branch, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Commit qilishda xato: {RepoName}/{Branch}/{FilePath}", repoName, branch, filePath);
            throw;
        }
    }

    /// <summary>
    /// Pull Request yaratish
    /// </summary>
    public async Task<PullRequest> CreatePullRequestAsync(
        string repoName,
        string branch,
        string title,
        string body,
        CancellationToken ct = default)
    {
        try
        {
            var newPr = new NewPullRequest(title, branch, _defaultBranch)
            {
                Body = body
            };

            var pr = await _client.PullRequest.Create(_owner, repoName, newPr);
            _logger.LogInformation("✅ PR yaratildi: {RepoName} #{Number} - {Url}", repoName, pr.Number, pr.HtmlUrl);
            return pr;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PR yaratishda xato: {RepoName}/{Branch}", repoName, branch);
            throw;
        }
    }

    /// <summary>
    /// Pull Request ni merge qilish
    /// </summary>
    public async Task<bool> MergePullRequestAsync(
        string repoName,
        int prNumber,
        string commitMessage,
        CancellationToken ct = default)
    {
        try
        {
            var merge = new MergePullRequest
            {
                CommitMessage = commitMessage,
                MergeMethod = PullRequestMergeMethod.Merge
            };

            var result = await _client.PullRequest.Merge(_owner, repoName, prNumber, merge);
            _logger.LogInformation("✅ PR merge qilindi: {RepoName} #{Number}", repoName, prNumber);
            return result.Merged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PR merge qilishda xato: {RepoName} #{Number}", repoName, prNumber);
            return false;
        }
    }

    /// <summary>
    /// Pull Request ni olish
    /// </summary>
    public async Task<PullRequest> GetPullRequestAsync(string repoName, int prNumber, CancellationToken ct = default)
    {
        try
        {
            return await _client.PullRequest.Get(_owner, repoName, prNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PR olishda xato: {RepoName} #{Number}", repoName, prNumber);
            throw;
        }
    }
}
