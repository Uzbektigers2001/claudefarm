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
/// Production-ready pipeline:
/// 1. dotnet CLI tekshirish
/// 2. Architect — loyiha strukturasini yaratadi (JSON)
/// 3. ProjectBuilderService — real .NET project yaratadi
/// 4. Agents — har biri o'z faylini yozadi
/// 5. Build validation — har agent keyin
/// 6. QA — test fayllar
/// 7. Reviewer — final review
/// 8. Final build — muvaffaqiyatli bo'lishi shart
/// 9. Git push + PR
/// </summary>
public sealed class OrchestratorPipelineRunner : IAgentPipelineRunner
{
    private readonly ArchitectAgent                      _architect;
    private readonly BackendAgent                        _backend;
    private readonly FrontendAgent                       _frontend;
    private readonly DevOpsAgent                         _devops;
    private readonly BusinessAnalystAgent                _businessAnalyst;
    private readonly SecurityAgent                       _security;
    private readonly DatabaseAdminAgent                  _databaseAdmin;
    private readonly QAAgent                             _qa;
    private readonly ReviewerAgent                       _reviewer;
    private readonly ProjectBuilderService               _projectBuilder;
    private readonly CodeWriterService                   _codeWriter;
    private readonly InMemorySessionStore                _sessionStore;
    private readonly ITelegramMessageSender              _sender;
    private readonly GitHubService                       _gitHubService;
    private readonly ProjectRepoService                  _projectRepoService;
    private readonly ILogger<OrchestratorPipelineRunner> _logger;

    public OrchestratorPipelineRunner(
        ArchitectAgent          architect,
        BackendAgent            backend,
        FrontendAgent           frontend,
        DevOpsAgent             devops,
        BusinessAnalystAgent    businessAnalyst,
        SecurityAgent           security,
        DatabaseAdminAgent      databaseAdmin,
        QAAgent                 qa,
        ReviewerAgent           reviewer,
        ProjectBuilderService   projectBuilder,
        CodeWriterService       codeWriter,
        InMemorySessionStore    sessionStore,
        ITelegramMessageSender  sender,
        GitHubService           gitHubService,
        ProjectRepoService      projectRepoService,
        ILogger<OrchestratorPipelineRunner> logger)
    {
        _architect         = architect;
        _backend           = backend;
        _frontend          = frontend;
        _devops            = devops;
        _businessAnalyst   = businessAnalyst;
        _security          = security;
        _databaseAdmin     = databaseAdmin;
        _qa                = qa;
        _reviewer          = reviewer;
        _projectBuilder    = projectBuilder;
        _codeWriter        = codeWriter;
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

        _logger.LogInformation("🚀 Production-ready pipeline boshlandi. TaskId={TaskId}", request.TaskId);

        var session = _sessionStore.CreateSession(request.Prompt, request.ChatId);
        string? workDir = null;
        ArchitectPlan? plan = null;

        try
        {
            // 0️⃣ dotnet CLI tekshirish
            if (!await _projectBuilder.IsDotnetInstalledAsync())
            {
                await _sender.SendTextAsync(request.ChatId, "❌ dotnet CLI topilmadi", useMarkdown: false, ct);
                return BuildFailedResult(request, responses, sw.Elapsed);
            }

            // 1️⃣ Architect — loyiha strukturasini yaratadi
            session.Status = SessionStatus.Planning;
            _sessionStore.UpdateSession(session);

            var architectResponse = await _architect.RunAsync(request, previousContext: null, ct);
            responses.Add(architectResponse);

            if (architectResponse.Status != AgentStatus.Completed)
            {
                await _sender.SendTextAsync(request.ChatId, "❌ Architect: plan yaratib bo'lmadi", useMarkdown: false, ct);
                return BuildFailedResult(request, responses, sw.Elapsed);
            }

            // 2️⃣ JSON parse (ArchitectPlan)
            plan = await ParseArchitectPlanAsync(architectResponse.Content, request, ct);
            if (plan == null)
            {
                await _sender.SendTextAsync(request.ChatId, "❌ Architect: JSON parse xatosi", useMarkdown: false, ct);
                return BuildFailedResult(request, responses, sw.Elapsed);
            }

            _logger.LogInformation("✅ Architect plan: {ProjectName}, {ProjectCount} project, {FileCount} files",
                plan.ProjectName, plan.Projects.Count, plan.Files.Count);

            // 3️⃣ ProjectBuilderService — real .NET project yaratish
            workDir = Path.Combine("/tmp/claudefarm", session.SessionId.ToString("N"));
            await _projectBuilder.BuildProjectAsync(plan, workDir, ct);

            _logger.LogInformation("✅ Real .NET project yaratildi: {WorkDir}", workDir);

            // 4️⃣ SubTask'lar yaratish (ArchitectPlan.Files dan)
            var projectContext = BuildProjectContext(plan);
            foreach (var file in plan.Files)
            {
                var subtask = new SubTask
                {
                    SessionId = session.SessionId,
                    AssignedTo = ParseRole(file.AssignTo),
                    Instance = file.Instance,
                    Description = file.Description,
                    FilePath = file.Path,
                    Namespace = ExtractNamespace(file.Path, plan.ProjectName),
                    ProjectContext = projectContext,
                    Status = SubTaskStatus.Pending
                };
                session.SubTasks.Add(subtask);
            }
            _sessionStore.UpdateSession(session);

            // 5️⃣ Git setup
            await SetupGitRepositoryAsync(session, request, ct);

            // 6️⃣ Parallel development agents
            session.Status = SessionStatus.Developing;
            _sessionStore.UpdateSession(session);

            var developmentTasks = session.SubTasks
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

                    // Agent ishlaydi
                    var response = await agent.RunAsync(subRequest, previousContext: null, ct);

                    if (response.Status == AgentStatus.Completed)
                    {
                        // Kodni ajratib olish
                        var code = _codeWriter.ExtractCode(response.Content);
                        subtask.Code = code;

                        // Faylga yozish
                        await _codeWriter.WriteFileAsync(workDir!, subtask.FilePath!, code, ct);

                        // Build tekshirish (max 2 retry)
                        var buildSuccess = await RetryBuildAsync(workDir!, plan!, subtask, agent, request, ct);

                        if (buildSuccess)
                        {
                            subtask.Status = SubTaskStatus.Done;

                            // Git: commit qilish
                            await CommitAgentResultAsync(session, subtask, ct);

                            await _sender.SendTextAsync(request.ChatId, $"[{subtask.DisplayName}] ✅", useMarkdown: false, ct);
                        }
                        else
                        {
                            subtask.Status = SubTaskStatus.Failed;
                            await _sender.SendTextAsync(request.ChatId, $"[{subtask.DisplayName}] ❌ build xatosi", useMarkdown: false, ct);
                        }
                    }
                    else
                    {
                        subtask.Status = SubTaskStatus.Failed;
                        await _sender.SendTextAsync(request.ChatId, $"[{subtask.DisplayName}] ❌ xato", useMarkdown: false, ct);
                    }

                    _sessionStore.UpdateSession(session);
                    return response;
                }).ToList();

            var developmentResponses = (await Task.WhenAll(developmentTasks))
                .Where(r => r != null)
                .Cast<AgentResponse>()
                .ToList();
            responses.AddRange(developmentResponses);

            // 7️⃣ Git: PR yaratish
            await CreatePullRequestAsync(session, request, ct);

            // 8️⃣ QA
            session.Status = SessionStatus.QA;
            _sessionStore.UpdateSession(session);

            var allDeveloperCode = BuildAllDeveloperCode(session.SubTasks);
            var qaResponse = await _qa.RunAsync(request, previousContext: allDeveloperCode, ct);
            responses.Add(qaResponse);

            if (qaResponse.Status == AgentStatus.Completed)
            {
                var qaSubtask = session.SubTasks.FirstOrDefault(st => st.AssignedTo == AgentRole.QA);
                if (qaSubtask != null)
                {
                    var qaCode = _codeWriter.ExtractCode(qaResponse.Content);
                    qaSubtask.Code = qaCode;
                    qaSubtask.Status = SubTaskStatus.Done;

                    if (!string.IsNullOrWhiteSpace(qaSubtask.FilePath))
                    {
                        await _codeWriter.WriteFileAsync(workDir!, qaSubtask.FilePath, qaCode, ct);
                    }

                    await CommitAgentResultAsync(session, qaSubtask, ct);
                }

                await _sender.SendTextAsync(request.ChatId, "[QA] ✅", useMarkdown: false, ct);
            }
            else
            {
                await _sender.SendTextAsync(request.ChatId, "[QA] ❌ xato", useMarkdown: false, ct);
            }

            // 9️⃣ Reviewer
            session.Status = SessionStatus.Reviewing;
            _sessionStore.UpdateSession(session);

            var reviewerContext = BuildReviewerContext(allDeveloperCode, qaResponse.Content);
            var reviewerResponse = await _reviewer.RunAsync(request, previousContext: reviewerContext, ct);
            responses.Add(reviewerResponse);

            if (reviewerResponse.Status == AgentStatus.Completed)
            {
                var reviewerSubtask = session.SubTasks.FirstOrDefault(st => st.AssignedTo == AgentRole.Reviewer);
                if (reviewerSubtask != null)
                {
                    reviewerSubtask.Code = reviewerResponse.Content;
                    reviewerSubtask.Status = SubTaskStatus.Done;

                    if (!string.IsNullOrWhiteSpace(reviewerSubtask.FilePath))
                    {
                        await _codeWriter.WriteFileAsync(workDir!, reviewerSubtask.FilePath, reviewerResponse.Content, ct);
                    }

                    await CommitAgentResultAsync(session, reviewerSubtask, ct);
                }

                await _sender.SendTextAsync(request.ChatId, "[Reviewer] ✅", useMarkdown: false, ct);

                // Git: PR ni merge qilish
                await HandleReviewerDecisionAsync(session, reviewerResponse.Content, ct);
            }
            else
            {
                await _sender.SendTextAsync(request.ChatId, "[Reviewer] ❌ xato", useMarkdown: false, ct);
            }

            // 🔟 Final build
            var finalBuild = await _codeWriter.BuildAsync(workDir!, plan.SolutionFile, ct);
            if (finalBuild.Success)
            {
                _logger.LogInformation("✅ Final build muvaffaqiyatli");
            }
            else
            {
                _logger.LogWarning("❌ Final build xatosi: {Errors}", string.Join(", ", finalBuild.Errors));
                await _sender.SendTextAsync(request.ChatId, $"⚠️ Final build xatosi:\n{string.Join("\n", finalBuild.Errors.Take(3))}", useMarkdown: false, ct);
            }

            // Yakuniy natija
            session.FinalResult = reviewerResponse.Content;
            session.Status = SessionStatus.Done;
            _sessionStore.UpdateSession(session);

            sw.Stop();

            // Yakuniy xabar
            string finalMessage;
            if (finalBuild.Success)
            {
                if (!string.IsNullOrWhiteSpace(session.PullRequestUrl))
                {
                    finalMessage = $"✅ Build o'tdi | PR: {session.PullRequestUrl}";
                }
                else if (!string.IsNullOrWhiteSpace(session.BranchName))
                {
                    finalMessage = $"✅ Build o'tdi | Branch: {session.BranchName}";
                }
                else
                {
                    finalMessage = "✅ Build o'tdi";
                }
            }
            else
            {
                finalMessage = $"❌ Build xatosi: {string.Join(", ", finalBuild.Errors.Take(2))}";
            }

            await _sender.SendTextAsync(request.ChatId, finalMessage, useMarkdown: false, ct);

            return new PipelineResult
            {
                TaskId = request.TaskId,
                ChatId = request.ChatId,
                OriginalPrompt = request.Prompt,
                AgentResponses = responses,
                TotalDuration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline xatosi. TaskId={TaskId}", request.TaskId);
            session.Status = SessionStatus.Failed;
            _sessionStore.UpdateSession(session);

            await _sender.SendTextAsync(request.ChatId, $"❌ Pipeline xatosi: {ex.Message}", useMarkdown: false, ct);

            return BuildFailedResult(request, responses, sw.Elapsed);
        }
    }

    /// <summary>
    /// ArchitectPlan JSON ni parse qilish
    /// </summary>
    private Task<ArchitectPlan?> ParseArchitectPlanAsync(string json, AgentRequest request, CancellationToken ct)
    {
        try
        {
            var cleanedJson = CleanJson(json);
            var plan = JsonSerializer.Deserialize<ArchitectPlan>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Task.FromResult(plan);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArchitectPlan parse xatosi");
            return Task.FromResult<ArchitectPlan?>(null);
        }
    }

    /// <summary>
    /// Markdown code block larni olib tashlash
    /// </summary>
    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```json\s*", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```\s*", "");

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            cleaned = cleaned[start..(end + 1)];

        return cleaned;
    }

    /// <summary>
    /// Loyiha konteksti yaratish (barcha fayllar ro'yxati)
    /// </summary>
    private static string BuildProjectContext(ArchitectPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Loyiha: {plan.ProjectName}");
        sb.AppendLine();
        sb.AppendLine("Projects:");
        foreach (var project in plan.Projects)
        {
            sb.AppendLine($"  - {project.Name} ({project.Type})");
        }
        sb.AppendLine();
        sb.AppendLine("Files:");
        foreach (var file in plan.Files)
        {
            sb.AppendLine($"  - {file.Path}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// FilePath dan namespace chiqarish
    /// </summary>
    private static string ExtractNamespace(string filePath, string projectName)
    {
        // src/HelloWorldApi.API/Controllers/HelloController.cs → HelloWorldApi.API.Controllers
        var parts = filePath.Replace("\\", "/").Split('/');
        var namespaceParts = new List<string>();

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "src" || parts[i] == "tests") continue;
            namespaceParts.Add(parts[i]);
        }

        return string.Join(".", namespaceParts);
    }

    /// <summary>
    /// Build retry logikasi (max 2 marta)
    /// </summary>
    private async Task<bool> RetryBuildAsync(
        string workDir,
        ArchitectPlan plan,
        SubTask subtask,
        AgentBase agent,
        AgentRequest originalRequest,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var buildResult = await _codeWriter.BuildAsync(workDir, plan.SolutionFile, ct);

            if (buildResult.Success)
            {
                _logger.LogInformation("✅ Build muvaffaqiyatli (attempt {Attempt}): {SubTask}", attempt, subtask.DisplayName);
                return true;
            }

            _logger.LogWarning("❌ Build xatosi (attempt {Attempt}): {SubTask}\n{Errors}",
                attempt, subtask.DisplayName, string.Join("\n", buildResult.Errors));

            if (attempt < 2)
            {
                // Agent ga xato bilan qayta yuborish
                var retryRequest = new AgentRequest
                {
                    ChatId = originalRequest.ChatId,
                    Prompt = $"{subtask.Description}\n\nBuild xatosi (tuzatish kerak):\n{string.Join("\n", buildResult.Errors.Take(5))}",
                    FilePath = subtask.FilePath,
                    Namespace = subtask.Namespace,
                    ProjectContext = subtask.ProjectContext
                };

                var retryResponse = await agent.RunAsync(retryRequest, previousContext: null, ct);
                if (retryResponse.Status == AgentStatus.Completed)
                {
                    var newCode = _codeWriter.ExtractCode(retryResponse.Content);
                    subtask.Code = newCode;
                    await _codeWriter.WriteFileAsync(workDir, subtask.FilePath!, newCode, ct);
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static AgentRole ParseRole(string role) => role.ToLower() switch
    {
        "backend"         => AgentRole.Backend,
        "frontend"        => AgentRole.Frontend,
        "devops"          => AgentRole.DevOps,
        "qa"              => AgentRole.QA,
        "reviewer"        => AgentRole.Reviewer,
        "security"        => AgentRole.Security,
        "databaseadmin"   => AgentRole.DatabaseAdmin,
        "businessanalyst" => AgentRole.BusinessAnalyst,
        _                 => AgentRole.Backend
    };

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
            RequestedRoles = new[] { subtask.AssignedTo },
            FilePath = subtask.FilePath,
            Namespace = subtask.Namespace,
            ProjectContext = subtask.ProjectContext
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
                sb.AppendLine($"### [{subtask.DisplayName}] {subtask.FilePath}");
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
            TaskId = request.TaskId,
            ChatId = request.ChatId,
            OriginalPrompt = request.Prompt,
            AgentResponses = responses,
            TotalDuration = duration
        };
    }

    // ==================== Git Integration Helper Methods ====================

    private async Task SetupGitRepositoryAsync(ProjectSession session, AgentRequest request, CancellationToken ct)
    {
        try
        {
            var projectName = session.OriginalTask.Length > 30
                ? session.OriginalTask[..30]
                : session.OriginalTask;

            var repoName = await _projectRepoService.CreateProjectRepoAsync(projectName, ct);
            session.RepoName = repoName;

            var branchName = $"task/{session.SessionId:N}";
            await _gitHubService.CreateBranchAsync(repoName, branchName, ct);
            session.BranchName = branchName;

            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git repository setup xatosi");
        }
    }

    private async Task CommitAgentResultAsync(ProjectSession session, SubTask subtask, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RepoName) || string.IsNullOrWhiteSpace(session.BranchName))
            return;

        try
        {
            var filePath = subtask.FilePath ?? $"misc/{subtask.SubTaskId:N}.txt";
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
                {string.Join("\n", session.SubTasks.Select(st => $"- [{st.DisplayName}] {st.FilePath ?? st.Description}"))}

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

    private async Task HandleReviewerDecisionAsync(ProjectSession session, string reviewerContent, CancellationToken ct)
    {
        if (!session.PullRequestNumber.HasValue ||
            string.IsNullOrWhiteSpace(session.RepoName) ||
            string.IsNullOrWhiteSpace(session.PullRequestUrl))
            return;

        try
        {
            var contentLower = reviewerContent.ToLowerInvariant();
            var isApproved = contentLower.Contains("lgtm") ||
                           contentLower.Contains("approved") ||
                           contentLower.Contains("looks good") ||
                           contentLower.Contains("✅");

            if (isApproved)
            {
                var merged = await _gitHubService.MergePullRequestAsync(
                    session.RepoName,
                    session.PullRequestNumber.Value,
                    $"Merged: {session.OriginalTask}",
                    ct);

                if (merged)
                {
                    await _sender.SendTextAsync(session.ChatId, $"✅ PR merge qilindi: {session.PullRequestUrl}", useMarkdown: false, ct);
                }
            }
            else
            {
                await _sender.SendTextAsync(session.ChatId, $"⚠️ PR review kerak: {session.PullRequestUrl}", useMarkdown: false, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PR handle qilishda xato");
        }
    }
}
