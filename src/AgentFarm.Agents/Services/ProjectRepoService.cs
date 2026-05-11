using AgentFarm.Agents.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace AgentFarm.Agents.Services;

/// <summary>
/// ClaudeFarm project repo'lari bilan ishlash
/// </summary>
public sealed class ProjectRepoService
{
    private readonly GitHubClient _client;
    private readonly string _owner;
    private readonly ILogger<ProjectRepoService> _logger;

    public ProjectRepoService(IOptions<GitHubOptions> options, ILogger<ProjectRepoService> logger)
    {
        var opts = options.Value;
        _owner = opts.Owner;
        _logger = logger;

        _client = new GitHubClient(new ProductHeaderValue("ClaudeFarm"))
        {
            Credentials = new Credentials(opts.Token)
        };
    }

    /// <summary>
    /// Yangi project repo yaratish
    /// </summary>
    public async Task<string> CreateProjectRepoAsync(string projectName, CancellationToken ct = default)
    {
        var repoName = GenerateRepoName(projectName);

        try
        {
            // Repo mavjudligini tekshirish
            try
            {
                await _client.Repository.Get(_owner, repoName);
                _logger.LogInformation("Repo allaqachon mavjud: {RepoName}", repoName);
                return repoName;
            }
            catch (NotFoundException)
            {
                // Repo mavjud emas, yangi yaratamiz
            }

            var newRepo = new NewRepository(repoName)
            {
                Description = $"ClaudeFarm generated project: {projectName}",
                AutoInit = true,
                Private = false,
                LicenseTemplate = "mit"
            };

            var repo = await _client.Repository.Create(newRepo);
            _logger.LogInformation("✅ Yangi repo yaratildi: {RepoName} - {Url}", repoName, repo.HtmlUrl);
            return repoName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Repo yaratishda xato: {RepoName}", repoName);
            throw;
        }
    }

    /// <summary>
    /// Repo nomi generatsiya qilish: claudefarm-{projectName}-{date}
    /// </summary>
    private static string GenerateRepoName(string projectName)
    {
        var sanitized = SanitizeProjectName(projectName);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return $"claudefarm-{sanitized}-{date}";
    }

    /// <summary>
    /// Project nomini GitHub repo nomi uchun tozalash
    /// </summary>
    private static string SanitizeProjectName(string projectName)
    {
        // GitHub repo nomi: faqat alphanumeric, dash, underscore
        var sanitized = projectName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");

        // Faqat alphanumeric va dash qoldirish
        sanitized = new string(sanitized
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        // Ortiqcha dashlarni olib tashlash
        while (sanitized.Contains("--"))
            sanitized = sanitized.Replace("--", "-");

        sanitized = sanitized.Trim('-');

        // Bo'sh bo'lsa yoki juda qisqa bo'lsa
        if (string.IsNullOrWhiteSpace(sanitized) || sanitized.Length < 3)
            sanitized = "project";

        // GitHub repo nomi maksimal 100 ta belgi
        if (sanitized.Length > 50)
            sanitized = sanitized[..50];

        return sanitized;
    }
}
