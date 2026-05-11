using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentFarm.Agents.Agents;
using AgentFarm.Agents.Base;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Bot.Services;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Orchestrator arxitekturasi (dynamic role selection):
/// 1. Orchestrator vazifani tahlil qilib kerakli rollarni tanlaydi
/// 2. Parallel: Backend/Frontend/DevOps/BusinessAnalyst/Security/DatabaseAdmin ishlaydi
/// 3. QA barcha developer kodlarini ko'radi
/// 4. Reviewer barcha natijalarni ko'radi (har doim oxirgi)
/// </summary>
public sealed class OrchestratorPipelineRunner : IAgentPipelineRunner
{
    private readonly OrchestratorAgent                   _orchestrator;
    private readonly BackendAgent                        _backend;
    private readonly FrontendAgent                       _frontend;
    private readonly DevOpsAgent                         _devops;
    private readonly BusinessAnalystAgent                _businessAnalyst;
    private readonly SecurityAgent                       _security;
    private readonly DatabaseAdminAgent                  _databaseAdmin;
    private readonly QAAgent                             _qa;
    private readonly ReviewerAgent                       _reviewer;
    private readonly InMemorySessionStore                _sessionStore;
    private readonly ITelegramMessageSender              _sender;
    private readonly GitHubService                       _gitHubService;
    private readonly ProjectRepoService                  _projectRepoService;
    private readonly ILogger<OrchestratorPipelineRunner> _logger;

    public OrchestratorPipelineRunner(
        OrchestratorAgent     orchestrator,
        BackendAgent          backend,
        FrontendAgent         frontend,
        DevOpsAgent           devops,
        BusinessAnalystAgent  businessAnalyst,
        SecurityAgent         security,
        DatabaseAdminAgent    databaseAdmin,
        QAAgent               qa,
        ReviewerAgent         reviewer,
        InMemorySessionStore  sessionStore,
        ITelegramMessageSender sender,
        GitHubService         gitHubService,
        ProjectRepoService    projectRepoService,
        ILogger<OrchestratorPipelineRunner> logger)
    {
        _orchestrator      = orchestrator;
        _backend           = backend;
        _frontend          = frontend;
        _devops            = devops;
        _businessAnalyst   = businessAnalyst;
        _security          = security;
        _databaseAdmin     = databaseAdmin;
        _qa                = qa;
        _reviewer          = reviewer;
        _sessionStore      = sessionStore;
        _sender            = sender;
        _gitHubService     = gitHubService;
        _projectRepoService = projectRepoService;
        _logger            = logger;
    }

    public async Task<PipelineResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var responses = new List<AgentResponse>();

        _logger.LogInformation("🚀 Orchestrator pipeline boshlandi. TaskId={TaskId}", request.TaskId);

        // Session yaratish
        var session = _sessionStore.CreateSession(request.Prompt, request.ChatId);

        try
        {
            // 1️⃣ Orchestrator - vazifani bo'ladi
            session.Status = SessionStatus.Planning;
            _sessionStore.UpdateSession(session);

            var orchestratorResponse = await _orchestrator.RunAsync(request, previousContext: null, ct);
            responses.Add(orchestratorResponse);

            if (orchestratorResponse.Status != AgentStatus.Completed)
            {
                await _sender.SendTextAsync(request.ChatId,
                    "❌ Orchestrator vazifani bo'la olmadi", useMarkdown: false, ct);
                session.Status = SessionStatus.Failed;
                _sessionStore.UpdateSession(session);
                return BuildFailedResult(request, responses, sw.Elapsed);
            }

            // JSON parse (max 3 retry)
            var subtasks = await ParseSubtasksWithRetry(orchestratorResponse.Content, request, ct);
            if (subtasks == null || subtasks.Count == 0)
            {
                await _sender.SendTextAsync(request.ChatId,
                    "❌ Orchestrator JSON formatda javob bermadi", useMarkdown: false, ct);
                session.Status = SessionStatus.Failed;
                _sessionStore.UpdateSession(session);
                return BuildFailedResult(request, responses, sw.Elapsed);
            }

            // SubTasklarni session ga qo'shish
            foreach (var st in subtasks)
            {
                session.SubTasks.Add(st);
            }
            _sessionStore.UpdateSession(session);

            // 🔧 Git Integration: Repo va branch yaratish (jim)
            await SetupGitRepositoryAsync(session, request, ct);

            // 2️⃣ Parallel: Barcha rollar parallel ishlaydi (QA va Reviewer bundan mustasno)
            session.Status = SessionStatus.Developing;
            _sessionStore.UpdateSession(session);

            var developmentTasks = subtasks
                .Where(st => st.AssignedTo != AgentRole.QA && st.AssignedTo != AgentRole.Reviewer)
                .Select(async subtask =>
                {
                    var agent = GetAgentForRole(subtask.AssignedTo);
                    if (agent == null)
                    {
                        _logger.LogWarning("Agent topilmadi: {Role}", subtask.AssignedTo);
                        subtask.Status = SubTaskStatus.Failed;
                        return null;
                    }

                    var subRequest = CreateSubRequest(request, subtask);

                    subtask.Status = SubTaskStatus.InProgress;
                    _sessionStore.UpdateSession(session);

                    var response = await agent.RunAsync(subRequest, previousContext: null, ct);

                    if (response.Status == AgentStatus.Completed)
                    {
                        subtask.Code = response.Content;
                        subtask.Status = SubTaskStatus.Done;

                        // Git: Commit qilish
                        await CommitAgentResultAsync(session, subtask, ct);

                        // Telegram: qisqa xabar
                        await _sender.SendTextAsync(request.ChatId,
                            $"[{subtask.DisplayName}] ✅", useMarkdown: false, ct);
                    }
                    else
                    {
                        subtask.Status = SubTaskStatus.Failed;
                        await _sender.SendTextAsync(request.ChatId,
                            $"[{subtask.DisplayName}] ❌ xato", useMarkdown: false, ct);
                    }

                    _sessionStore.UpdateSession(session);
                    return response;
                }).ToList();

            var developmentResponses = (await Task.WhenAll(developmentTasks))
                .Where(r => r != null)
                .Cast<AgentResponse>()
                .ToList();
            responses.AddRange(developmentResponses);

            // Git: PR yaratish
            await CreatePullRequestAsync(session, request, ct);

            // 3️⃣ QA - barcha developer kodlarini ko'radi
            session.Status = SessionStatus.QA;
            _sessionStore.UpdateSession(session);

            var allDeveloperCode = BuildAllDeveloperCode(session.SubTasks);
            var qaResponse = await _qa.RunAsync(request, previousContext: allDeveloperCode, ct);
            responses.Add(qaResponse);

            // Git: QA natijasini commit qilish
            if (qaResponse.Status == AgentStatus.Completed)
            {
                var qaSubtask = session.SubTasks.FirstOrDefault(st => st.AssignedTo == AgentRole.QA);
                if (qaSubtask != null)
                {
                    qaSubtask.Code = qaResponse.Content;
                    qaSubtask.Status = SubTaskStatus.Done;
                    await CommitAgentResultAsync(session, qaSubtask, ct);
                }

                // Telegram: qisqa xabar
                await _sender.SendTextAsync(request.ChatId,
                    "[QA] ✅", useMarkdown: false, ct);
            }
            else
            {
                await _sender.SendTextAsync(request.ChatId,
                    "[QA] ❌ xato", useMarkdown: false, ct);
            }

            // 4️⃣ Reviewer - Developer kodlari + QA natijasini ko'radi
            session.Status = SessionStatus.Reviewing;
            _sessionStore.UpdateSession(session);

            var reviewerContext = BuildReviewerContext(allDeveloperCode, qaResponse.Content);
            var reviewerResponse = await _reviewer.RunAsync(request, previousContext: reviewerContext, ct);
            responses.Add(reviewerResponse);

            // Git: Reviewer natijasini commit qilish
            if (reviewerResponse.Status == AgentStatus.Completed)
            {
                var reviewerSubtask = session.SubTasks.FirstOrDefault(st => st.AssignedTo == AgentRole.Reviewer);
                if (reviewerSubtask != null)
                {
                    reviewerSubtask.Code = reviewerResponse.Content;
                    reviewerSubtask.Status = SubTaskStatus.Done;
                    await CommitAgentResultAsync(session, reviewerSubtask, ct);
                }

                // Telegram: qisqa xabar
                await _sender.SendTextAsync(request.ChatId,
                    "[Reviewer] ✅", useMarkdown: false, ct);

                // Git: PR ni merge qilish yoki comment qoldirish
                await HandleReviewerDecisionAsync(session, reviewerResponse.Content, ct);
            }
            else
            {
                await _sender.SendTextAsync(request.ChatId,
                    "[Reviewer] ❌ xato", useMarkdown: false, ct);
            }

            // Yakuniy natija
            session.FinalResult = reviewerResponse.Content;
            session.Status = SessionStatus.Done;
            _sessionStore.UpdateSession(session);

            sw.Stop();

            var successful = responses.Count(r => r.Status == AgentStatus.Completed);
            var total = responses.Count;

            // Yakuniy xabar: PR link bilan
            var finalMessage = $"✅ Tugadi: {successful} agent | {(int)sw.Elapsed.TotalSeconds}s";
            if (!string.IsNullOrWhiteSpace(session.PullRequestUrl))
            {
                finalMessage += $" | PR: {session.PullRequestUrl}";
            }

            await _sender.SendTextAsync(request.ChatId, finalMessage, useMarkdown: false, ct);

            return new PipelineResult
            {
                TaskId         = request.TaskId,
                ChatId         = request.ChatId,
                OriginalPrompt = request.Prompt,
                AgentResponses = responses,
                TotalDuration  = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline xatosi. TaskId={TaskId}", request.TaskId);
            session.Status = SessionStatus.Failed;
            _sessionStore.UpdateSession(session);

            await _sender.SendTextAsync(request.ChatId,
                $"❌ Pipeline xatosi: {ex.Message}", useMarkdown: false, ct);

            return BuildFailedResult(request, responses, sw.Elapsed);
        }
    }

    private static string CleanJson(string raw)
    {
        // markdown code block larni olib tashlaymiz
        var cleaned = raw.Trim();
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```json\s*", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```\s*", "");
        // JSON ni topib olamiz
        var start = cleaned.IndexOf('{');
        var end   = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            cleaned = cleaned[start..(end + 1)];
        return cleaned;
    }

    private async Task<List<SubTask>?> ParseSubtasksWithRetry(string json, AgentRequest request, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var cleanedJson = CleanJson(json);
                var doc = JsonDocument.Parse(cleanedJson);
                var subtasksArray = doc.RootElement.GetProperty("subtasks");

                var result = new List<SubTask>();
                foreach (var item in subtasksArray.EnumerateArray())
                {
                    var roleName = item.GetProperty("role").GetString() ?? "Backend";
                    var role = ParseAgentRole(roleName);
                    var instance = 1;
                    if (item.TryGetProperty("instance", out var instanceElement))
                    {
                        instance = instanceElement.GetInt32();
                    }

                    result.Add(new SubTask
                    {
                        AssignedTo = role,
                        Instance = instance,
                        Description = item.GetProperty("description").GetString() ?? "",
                        Status = SubTaskStatus.Pending
                    });
                }

                _logger.LogInformation("✅ JSON parse muvaffaqiyatli. Subtasklar: {Count}", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JSON parse xatosi (attempt {Attempt}/3)", attempt);

                if (attempt < 3)
                {
                    await Task.Delay(1000, ct);

                    // Orchestrator ga qayta so'rash
                    var retryResponse = await _orchestrator.RunAsync(request, previousContext: null, ct);
                    json = retryResponse.Content;
                }
            }
        }

        // Fallback: Backend + QA + Reviewer
        _logger.LogWarning("JSON parse muvaffaqiyatsiz, fallback subtasklar yaratildi");
        return new List<SubTask>
        {
            new() { AssignedTo = AgentRole.Backend, Instance = 1, Description = request.Prompt, Status = SubTaskStatus.Pending },
            new() { AssignedTo = AgentRole.QA, Instance = 1, Description = "Kodni test qilish", Status = SubTaskStatus.Pending },
            new() { AssignedTo = AgentRole.Reviewer, Instance = 1, Description = "Kodni review qilish", Status = SubTaskStatus.Pending }
        };
    }

    private static AgentRole ParseAgentRole(string roleName)
    {
        return roleName switch
        {
            "Backend" => AgentRole.Backend,
            "Frontend" => AgentRole.Frontend,
            "DevOps" => AgentRole.DevOps,
            "QA" => AgentRole.QA,
            "Reviewer" => AgentRole.Reviewer,
            "BusinessAnalyst" => AgentRole.BusinessAnalyst,
            "Security" => AgentRole.Security,
            "DatabaseAdmin" => AgentRole.DatabaseAdmin,
            _ => AgentRole.Backend
        };
    }

    private AgentBase? GetAgentForRole(AgentRole role) => role switch
    {
        AgentRole.Backend => _backend,
        AgentRole.Frontend => _frontend,
        AgentRole.DevOps => _devops,
        AgentRole.QA => _qa,
        AgentRole.Reviewer => _reviewer,
        AgentRole.BusinessAnalyst => _businessAnalyst,
        AgentRole.Security => _security,
        AgentRole.DatabaseAdmin => _databaseAdmin,
        _ => null
    };

    private static AgentRequest CreateSubRequest(AgentRequest original, SubTask subtask)
    {
        return new AgentRequest
        {
            ChatId = original.ChatId,
            Prompt = subtask.Description,
            Context = $"Bu katta vazifaning bir qismi: {original.Prompt}",
            RequestedRoles = new[] { subtask.AssignedTo }
        };
    }

    private static string BuildAllDeveloperCode(List<SubTask> subtasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Barcha developerlar yozgan kodlar");
        sb.AppendLine();

        foreach (var subtask in subtasks)
        {
            if (subtask.Status == SubTaskStatus.Done && !string.IsNullOrWhiteSpace(subtask.Code))
            {
                sb.AppendLine($"### [{subtask.DisplayName}] {subtask.Description}");
                sb.AppendLine(subtask.Code);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string BuildReviewerContext(string developerCode, string qaFindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Developer kodlari");
        sb.AppendLine(developerCode);
        sb.AppendLine();
        sb.AppendLine("### QA topilmalari");
        sb.AppendLine(qaFindings);
        return sb.ToString();
    }

    private static PipelineResult BuildFailedResult(AgentRequest request, List<AgentResponse> responses, TimeSpan duration)
    {
        return new PipelineResult
        {
            TaskId         = request.TaskId,
            ChatId         = request.ChatId,
            OriginalPrompt = request.Prompt,
            AgentResponses = responses,
            TotalDuration  = duration
        };
    }

    // ==================== Git Integration Helper Methods ====================

    /// <summary>
    /// Repo va branch yaratish
    /// </summary>
    private async Task SetupGitRepositoryAsync(ProjectSession session, AgentRequest request, CancellationToken ct)
    {
        try
        {
            // Project nomi: task'ning birinchi 30 belgisi
            var projectName = session.OriginalTask.Length > 30
                ? session.OriginalTask[..30]
                : session.OriginalTask;

            // Repo yaratish/tekshirish
            var repoName = await _projectRepoService.CreateProjectRepoAsync(projectName, ct);
            session.RepoName = repoName;

            // Branch yaratish
            var branchName = $"task/{session.SessionId:N}";
            await _gitHubService.CreateBranchAsync(repoName, branchName, ct);
            session.BranchName = branchName;

            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git repository setup xatosi");
            // Git xatosi bo'lsa ham pipeline davom etadi
        }
    }

    /// <summary>
    /// Agent natijasini commit qilish
    /// </summary>
    private async Task CommitAgentResultAsync(ProjectSession session, SubTask subtask, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RepoName) || string.IsNullOrWhiteSpace(session.BranchName))
            return;

        try
        {
            var filePath = GetFilePathForAgent(subtask);
            var commitMessage = $"[{subtask.DisplayName}] {subtask.Description}";

            await _gitHubService.CommitFileAsync(
                session.RepoName,
                session.BranchName,
                filePath,
                subtask.Code ?? "",
                commitMessage,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git commit xatosi: {SubTask}", subtask.DisplayName);
        }
    }

    /// <summary>
    /// PR yaratish
    /// </summary>
    private async Task CreatePullRequestAsync(ProjectSession session, AgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RepoName) || string.IsNullOrWhiteSpace(session.BranchName))
            return;

        try
        {
            var prTitle = $"Task: {session.OriginalTask}";
            var prBody = $"""
                ## Task
                {session.OriginalTask}

                ## Subtasks
                {string.Join("\n", session.SubTasks.Select(st => $"- [{st.DisplayName}] {st.Description}"))}

                ---
                🤖 Generated by ClaudeFarm
                """;

            var pr = await _gitHubService.CreatePullRequestAsync(
                session.RepoName,
                session.BranchName,
                prTitle,
                prBody,
                ct);

            session.PullRequestNumber = pr.Number;
            session.PullRequestUrl = pr.HtmlUrl;
            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PR yaratishda xato");
        }
    }

    /// <summary>
    /// Reviewer qarorini qayta ishlash (LGTM bo'lsa merge, aks holda comment)
    /// </summary>
    private async Task HandleReviewerDecisionAsync(ProjectSession session, string reviewerContent, CancellationToken ct)
    {
        if (!session.PullRequestNumber.HasValue ||
            string.IsNullOrWhiteSpace(session.RepoName) ||
            string.IsNullOrWhiteSpace(session.PullRequestUrl))
            return;

        try
        {
            // LGTM yoki approval yozuvlari bor-yo'qligini tekshirish
            var contentLower = reviewerContent.ToLowerInvariant();
            var isApproved = contentLower.Contains("lgtm") ||
                           contentLower.Contains("approved") ||
                           contentLower.Contains("looks good") ||
                           contentLower.Contains("✅");

            if (isApproved)
            {
                // PR ni merge qilish
                var merged = await _gitHubService.MergePullRequestAsync(
                    session.RepoName,
                    session.PullRequestNumber.Value,
                    $"Merged: {session.OriginalTask}",
                    ct);

                if (merged)
                {
                    await _sender.SendTextAsync(session.ChatId,
                        $"✅ PR merge qilindi: {session.PullRequestUrl}", useMarkdown: false, ct);
                }
                else
                {
                    await _sender.SendTextAsync(session.ChatId,
                        $"⚠️ PR merge qilishda xato: {session.PullRequestUrl}", useMarkdown: false, ct);
                }
            }
            else
            {
                // Reviewer o'zgarish talab qilgan
                await _sender.SendTextAsync(session.ChatId,
                    $"⚠️ PR review kerak: {session.PullRequestUrl}\n\nReviewer sharhi:\n{reviewerContent}",
                    useMarkdown: false, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PR handle qilishda xato");
        }
    }

    /// <summary>
    /// Agent uchun fayl yo'lini olish
    /// </summary>
    private static string GetFilePathForAgent(SubTask subtask)
    {
        var fileName = $"{subtask.SubTaskId:N}";

        return subtask.AssignedTo switch
        {
            AgentRole.Backend => $"src/backend/{fileName}.cs",
            AgentRole.Frontend => $"src/frontend/{fileName}.tsx",
            AgentRole.DevOps => $"devops/{fileName}.yml",
            AgentRole.DatabaseAdmin => $"db/{fileName}.sql",
            AgentRole.Security => $"docs/security-{fileName}.md",
            AgentRole.BusinessAnalyst => $"docs/ba-{fileName}.md",
            AgentRole.QA => $"tests/{fileName}.cs",
            AgentRole.Reviewer => $"docs/review-{fileName}.md",
            _ => $"misc/{fileName}.txt"
        };
    }
}
