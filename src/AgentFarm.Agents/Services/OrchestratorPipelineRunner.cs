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

public sealed class OrchestratorPipelineRunner : IAgentPipelineRunner
{
    private readonly PlannerAgent                         _planner;
    private readonly AnalystAgent                         _analyst;
    private readonly ArchitectAgent                       _architect;
    private readonly BackendAgent                         _backend;
    private readonly FrontendAgent                        _frontend;
    private readonly DevOpsAgent                          _devops;
    private readonly BusinessAnalystAgent                 _businessAnalyst;
    private readonly SecurityAgent                        _security;
    private readonly DatabaseAdminAgent                   _databaseAdmin;
    private readonly QAAgent                              _qa;
    private readonly ReviewerAgent                        _reviewer;
    private readonly ProjectBuilderService                _projectBuilder;
    private readonly CodeWriterService                    _codeWriter;
    private readonly InMemorySessionStore                 _sessionStore;
    private readonly ITelegramMessageSender               _sender;
    private readonly GitHubService                        _gitHubService;
    private readonly ProjectRepoService                   _projectRepoService;
    private readonly OrchestratorDecision                 _decision;
    private readonly ILogger<OrchestratorPipelineRunner>  _logger;

    private const int MaxPlannerIterations   = 2;
    private const int MaxAnalystIterations   = 2;
    private const int MaxArchitectIterations = 2;
    private const int MaxDeveloperIterations = 5;
    private const int MaxReviewerIterations  = 3;
    private const int MaxQaIterations        = 3;

    public OrchestratorPipelineRunner(
        PlannerAgent            planner,
        AnalystAgent            analyst,
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
        OrchestratorDecision    decision,
        ILogger<OrchestratorPipelineRunner> logger)
    {
        _planner            = planner;
        _analyst            = analyst;
        _architect          = architect;
        _backend            = backend;
        _frontend           = frontend;
        _devops             = devops;
        _businessAnalyst    = businessAnalyst;
        _security           = security;
        _databaseAdmin      = databaseAdmin;
        _qa                 = qa;
        _reviewer           = reviewer;
        _projectBuilder     = projectBuilder;
        _codeWriter         = codeWriter;
        _sessionStore       = sessionStore;
        _sender             = sender;
        _gitHubService      = gitHubService;
        _projectRepoService = projectRepoService;
        _decision           = decision;
        _logger             = logger;
    }

    public async Task<PipelineResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sw        = Stopwatch.StartNew();
        var responses = new List<AgentResponse>();
        var session   = _sessionStore.CreateSession(request.Prompt, request.ChatId);
        string? workDir = null;
        ArchitectPlan? plan = null;

        await Send(request.ChatId, "⚙️ Boshlanmoqda...", ct);

        try
        {
            if (!await _projectBuilder.IsDotnetInstalledAsync())
            {
                await Send(request.ChatId, "❌ dotnet CLI topilmadi", ct);
                return Fail(request, responses, sw.Elapsed);
            }

            // ===== BOSQICH 1: PLANNER =====
            string plannerOutput = string.Empty;
            for (int i = 1; i <= MaxPlannerIterations; i++)
            {
                var resp = await _planner.RunAsync(
                    BuildRequest(request, request.Prompt, previousContext: i > 1 ? plannerOutput : null), null, ct);
                responses.Add(resp);
                AddHistory(session, AgentRole.Planner, resp.Content);

                if (resp.Status != AgentStatus.Completed)
                {
                    await Escalate(request.ChatId, "Planner", i, "Agent javobi xato", ct);
                    return Fail(request, responses, sw.Elapsed);
                }

                var dec = _decision.Decide(AgentRole.Planner, resp.Content, i, MaxPlannerIterations);
                plannerOutput = resp.Content;

                if (dec == Decision.Escalate)
                {
                    await Escalate(request.ChatId, "Planner", i, resp.Content, ct);
                    return Fail(request, responses, sw.Elapsed);
                }
                if (dec == Decision.Continue) break;
            }
            await Send(request.ChatId, "📋 Planner: qadamlar tayyor", ct);

            // ===== BOSQICH 2: ANALYST =====
            string analystOutput = string.Empty;
            for (int i = 1; i <= MaxAnalystIterations; i++)
            {
                var prompt = $"{request.Prompt}\n\nPlanner qadamlari:\n{plannerOutput}";
                var resp   = await _analyst.RunAsync(
                    BuildRequest(request, prompt, previousContext: i > 1 ? analystOutput : null), null, ct);
                responses.Add(resp);
                AddHistory(session, AgentRole.Analyst, resp.Content);

                if (resp.Status != AgentStatus.Completed)
                {
                    await Escalate(request.ChatId, "Analyst", i, "Agent javobi xato", ct);
                    return Fail(request, responses, sw.Elapsed);
                }

                var dec = _decision.Decide(AgentRole.Analyst, resp.Content, i, MaxAnalystIterations);
                analystOutput = resp.Content;

                if (dec == Decision.Escalate)
                {
                    await Escalate(request.ChatId, "Analyst", i, resp.Content, ct);
                    return Fail(request, responses, sw.Elapsed);
                }
                if (dec == Decision.Continue) break;
            }
            session.Requirements = analystOutput;
            _sessionStore.UpdateSession(session);
            await Send(request.ChatId, "📝 Analyst: talablar tayyor", ct);

            // ===== BOSQICH 3: ARCHITECT =====
            for (int i = 1; i <= MaxArchitectIterations; i++)
            {
                var prompt = $"{request.Prompt}\n\nTalablar:\n{analystOutput}";
                var resp   = await _architect.RunAsync(
                    BuildRequest(request, prompt, previousContext: i > 1 ? session.ArchitectPlanJson : null), null, ct);
                responses.Add(resp);
                AddHistory(session, AgentRole.Architect, resp.Content);

                if (resp.Status != AgentStatus.Completed)
                {
                    await Escalate(request.ChatId, "Architect", i, "Agent javobi xato", ct);
                    return Fail(request, responses, sw.Elapsed);
                }

                plan = ParseArchitectPlan(resp.Content);
                if (plan == null)
                {
                    if (i == MaxArchitectIterations)
                    {
                        await Escalate(request.ChatId, "Architect", i, "JSON parse xatosi", ct);
                        return Fail(request, responses, sw.Elapsed);
                    }
                    session.ArchitectPlanJson = resp.Content + "\n\nXATO: JSON parse xatosi. To'g'ri JSON format bilan qayta yoz.";
                    continue;
                }

                session.ArchitectPlanJson = resp.Content;
                _sessionStore.UpdateSession(session);
                break;
            }

            if (plan == null) return Fail(request, responses, sw.Elapsed);
            await Send(request.ChatId, $"🏗️ Architect: {plan.Files.Count} fayl", ct);

            workDir = Path.Combine("/tmp/claudefarm", session.SessionId.ToString("N"));
            await _projectBuilder.BuildProjectAsync(plan, workDir, ct);

            // ===== BOSQICH 4: DEVELOPER LOOP =====
            var devSuccess = false;
            for (int iter = 1; iter <= MaxDeveloperIterations; iter++)
            {
                await Send(request.ChatId, $"🔨 Developer ({iter}/{MaxDeveloperIterations})...", ct);

                foreach (var file in plan.Files)
                {
                    var agent = GetAgentForRole(file.AssignTo) ?? _backend;
                    var filePrompt = BuildFilePrompt(request.Prompt, analystOutput, file, session.BuildErrors);
                    var projectCtx = BuildProjectContext(plan);

                    var fileResp = await agent.RunAsync(
                        BuildRequest(request, filePrompt,
                            filePath: file.Path,
                            ns: ExtractNamespace(file.Path),
                            projectContext: projectCtx),
                        BuildWrittenFilesContext(session),
                        ct);
                    responses.Add(fileResp);
                    AddHistory(session, agent.Role, fileResp.Content);

                    if (fileResp.Status == AgentStatus.Completed)
                    {
                        var code = _codeWriter.ExtractCode(fileResp.Content);
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            await _codeWriter.WriteFileAsync(workDir!, file.Path, code, ct);
                            session.Files[file.Path] = code;
                        }
                    }
                }
                _sessionStore.UpdateSession(session);

                var build = await _codeWriter.BuildAsync(workDir!, plan.SolutionFile, ct);
                session.BuildErrors = build.Errors;
                _sessionStore.UpdateSession(session);

                if (build.Success)
                {
                    devSuccess = true;
                    break;
                }

                _logger.LogWarning("Build xatosi iter={Iter}: {Errors}", iter, string.Join(", ", build.Errors.Take(3)));

                if (iter == MaxDeveloperIterations)
                {
                    await Escalate(request.ChatId, "Developer", iter,
                        string.Join("\n", build.Errors.Take(5)), ct);
                    return Fail(request, responses, sw.Elapsed);
                }
            }

            if (!devSuccess) return Fail(request, responses, sw.Elapsed);

            // ===== BOSQICH 5: REVIEWER LOOP =====
            for (int iter = 1; iter <= MaxReviewerIterations; iter++)
            {
                await Send(request.ChatId, $"👀 Reviewer ({iter}/{MaxReviewerIterations})...", ct);

                var allCode  = BuildAllFilesContext(session);
                var revResp  = await _reviewer.RunAsync(
                    BuildRequest(request, request.Prompt), allCode, ct);
                responses.Add(revResp);
                AddHistory(session, AgentRole.Reviewer, revResp.Content);

                if (revResp.Status != AgentStatus.Completed)
                {
                    if (iter == MaxReviewerIterations)
                        await Escalate(request.ChatId, "Reviewer", iter, "Agent javobi xato", ct);
                    continue;
                }

                var dec = _decision.Decide(AgentRole.Reviewer, revResp.Content, iter, MaxReviewerIterations);

                if (dec == Decision.Continue) break;

                if (dec == Decision.RetryPreviousAgent)
                {
                    await Escalate(request.ChatId, "Reviewer", iter, "Arxitektura qayta ko'rib chiqish kerak", ct);
                    break;
                }

                if (dec == Decision.Escalate)
                {
                    await Escalate(request.ChatId, "Reviewer", iter, revResp.Content, ct);
                    break;
                }

                // RetryCurrentAgent → fix via Developer
                if (iter < MaxReviewerIterations)
                {
                    await Send(request.ChatId, $"🔨 Developer tuzatmoqda...", ct);
                    foreach (var file in plan.Files)
                    {
                        var agent = GetAgentForRole(file.AssignTo) ?? _backend;
                        var fixPrompt = $"Reviewer topgan muammolarni tuzat:\n{revResp.Content}\n\nFayl: {file.Path}\n{file.Description}";
                        var fixResp   = await agent.RunAsync(
                            BuildRequest(request, fixPrompt, filePath: file.Path,
                                projectContext: BuildProjectContext(plan)),
                            allCode, ct);
                        responses.Add(fixResp);
                        AddHistory(session, agent.Role, fixResp.Content);

                        if (fixResp.Status == AgentStatus.Completed)
                        {
                            var code = _codeWriter.ExtractCode(fixResp.Content);
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                await _codeWriter.WriteFileAsync(workDir!, file.Path, code, ct);
                                session.Files[file.Path] = code;
                            }
                        }
                    }
                    _sessionStore.UpdateSession(session);
                }
            }

            // ===== BOSQICH 6: QA LOOP =====
            for (int iter = 1; iter <= MaxQaIterations; iter++)
            {
                await Send(request.ChatId, $"🧪 QA ({iter}/{MaxQaIterations})...", ct);

                var allCode = BuildAllFilesContext(session);
                var qaResp  = await _qa.RunAsync(
                    BuildRequest(request, request.Prompt), allCode, ct);
                responses.Add(qaResp);
                AddHistory(session, AgentRole.QA, qaResp.Content);

                if (qaResp.Status != AgentStatus.Completed)
                {
                    if (iter == MaxQaIterations)
                        await Escalate(request.ChatId, "QA", iter, "Agent javobi xato", ct);
                    continue;
                }

                var dec = _decision.Decide(AgentRole.QA, qaResp.Content, iter, MaxQaIterations);

                if (dec == Decision.Continue) break;

                if (dec == Decision.Escalate)
                {
                    await Escalate(request.ChatId, "QA", iter, qaResp.Content, ct);
                    break;
                }

                // RetryCurrentAgent → fix via Developer
                if (iter < MaxQaIterations)
                {
                    await Send(request.ChatId, $"🔨 Developer QA baglarini tuzatmoqda...", ct);
                    foreach (var file in plan.Files)
                    {
                        var agent = GetAgentForRole(file.AssignTo) ?? _backend;
                        var fixPrompt = $"QA topgan baglarni tuzat:\n{qaResp.Content}\n\nFayl: {file.Path}\n{file.Description}";
                        var fixResp   = await agent.RunAsync(
                            BuildRequest(request, fixPrompt, filePath: file.Path,
                                projectContext: BuildProjectContext(plan)),
                            allCode, ct);
                        responses.Add(fixResp);
                        AddHistory(session, agent.Role, fixResp.Content);

                        if (fixResp.Status == AgentStatus.Completed)
                        {
                            var code = _codeWriter.ExtractCode(fixResp.Content);
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                await _codeWriter.WriteFileAsync(workDir!, file.Path, code, ct);
                                session.Files[file.Path] = code;
                            }
                        }
                    }
                    _sessionStore.UpdateSession(session);
                }
            }

            // ===== BOSQICH 7: GIT =====
            await SetupGitRepositoryAsync(session, ct);
            await CommitAllFilesAsync(session, ct);
            await CreatePullRequestAsync(session, request.Prompt, ct);

            session.Status = SessionStatus.Done;
            _sessionStore.UpdateSession(session);
            sw.Stop();

            var finalMsg = string.IsNullOrWhiteSpace(session.PullRequestUrl)
                ? "✅ Tugadi"
                : $"✅ Tugadi | PR: {session.PullRequestUrl}";
            await Send(request.ChatId, finalMsg, ct);

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
            await Send(request.ChatId, $"❌ Xato: {ex.Message}", ct);
            return Fail(request, responses, sw.Elapsed);
        }
    }

    // ===== Helpers =====

    private Task Send(long chatId, string text, CancellationToken ct) =>
        _sender.SendTextAsync(chatId, text, useMarkdown: false, ct);

    private async Task Escalate(long chatId, string role, int iteration, string description, CancellationToken ct)
    {
        var trimmed = description.Length > 200 ? description[..200] : description;
        await Send(chatId,
            $"⚠️ Eskalatsiya: {role} {iteration} urinishdan keyin hal qila olmadi.\nMuammo: {trimmed}\nQo'lda hal qilish kerak.",
            ct);
        _logger.LogWarning("Eskalatsiya: {Role} iter={Iter}", role, iteration);
    }

    private static void AddHistory(ProjectSession session, AgentRole role, string content) =>
        session.History.Add(new AgentMessage { Role = role, Content = content });

    private static AgentRequest BuildRequest(
        AgentRequest original,
        string prompt,
        string? previousContext = null,
        string? filePath = null,
        string? ns = null,
        string? projectContext = null) =>
        new()
        {
            ChatId         = original.ChatId,
            Prompt         = prompt,
            Context        = previousContext,
            FilePath       = filePath,
            Namespace      = ns,
            ProjectContext = projectContext
        };

    private AgentBase? GetAgentForRole(string assignTo)
    {
        AgentBase? agent = assignTo.ToLower() switch
        {
            "backend"         => _backend,
            "frontend"        => _frontend,
            "devops"          => _devops,
            "qa"              => _qa,
            "reviewer"        => _reviewer,
            "security"        => _security,
            "databaseadmin"   => _databaseAdmin,
            "businessanalyst" => _businessAnalyst,
            _                 => _backend
        };
        return agent is { IsEnabled: true } ? agent : null;
    }

    private static ArchitectPlan? ParseArchitectPlan(string json)
    {
        try
        {
            var cleaned = json.Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```json\s*", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```\s*", "");
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            return JsonSerializer.Deserialize<ArchitectPlan>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
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

    private static string BuildFilePrompt(
        string originalPrompt,
        string requirements,
        ArchitectFile file,
        List<string> buildErrors)
    {
        var sb = new StringBuilder();
        sb.AppendLine(originalPrompt);
        sb.AppendLine();
        sb.AppendLine($"## Yozilishi kerak bo'lgan fayl");
        sb.AppendLine($"Path: {file.Path}");
        sb.AppendLine($"Tavsif: {file.Description}");
        sb.AppendLine();
        sb.AppendLine("## Texnik talablar");
        sb.AppendLine(requirements);

        if (buildErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Build xatolari (tuzatish kerak)");
            foreach (var err in buildErrors.Take(10))
                sb.AppendLine($"- {err}");
        }

        return sb.ToString();
    }

    private static string BuildProjectContext(ArchitectPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Loyiha: {plan.ProjectName}");
        sb.AppendLine("Projects:");
        foreach (var p in plan.Projects)
            sb.AppendLine($"  - {p.Name} ({p.Type})");
        sb.AppendLine("Files:");
        foreach (var f in plan.Files)
            sb.AppendLine($"  - {f.Path} [{f.AssignTo}] — {f.Description}");
        return sb.ToString();
    }

    private static string BuildWrittenFilesContext(ProjectSession session)
    {
        if (session.Files.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## Allaqachon yozilgan fayllar");
        foreach (var (path, code) in session.Files)
        {
            sb.AppendLine($"### {path}");
            sb.AppendLine(code);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildAllFilesContext(ProjectSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Barcha yozilgan fayllar");
        foreach (var (path, code) in session.Files)
        {
            sb.AppendLine($"### {path}");
            sb.AppendLine(code);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string ExtractNamespace(string filePath)
    {
        var parts    = filePath.Replace("\\", "/").Split('/');
        var nsParts  = new List<string>();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] is "src" or "tests") continue;
            nsParts.Add(parts[i]);
        }
        return string.Join(".", nsParts);
    }

    private static PipelineResult Fail(AgentRequest request, List<AgentResponse> responses, TimeSpan duration) =>
        new()
        {
            TaskId         = request.TaskId,
            ChatId         = request.ChatId,
            OriginalPrompt = request.Prompt,
            AgentResponses = responses,
            TotalDuration  = duration
        };

    // ===== Git Helpers =====

    private async Task SetupGitRepositoryAsync(ProjectSession session, CancellationToken ct)
    {
        try
        {
            var repoName = await _projectRepoService.CreateProjectRepoAsync(session.OriginalTask, ct);
            session.RepoName  = repoName;
            session.BranchName = $"task/{session.SessionId:N}";
            await _gitHubService.CreateBranchAsync(repoName, session.BranchName, ct);
            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Git setup xatosi"); }
    }

    private async Task CommitAllFilesAsync(ProjectSession session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RepoName) || string.IsNullOrWhiteSpace(session.BranchName))
            return;

        foreach (var (path, code) in session.Files)
        {
            try
            {
                await _gitHubService.CommitFileAsync(
                    session.RepoName, session.BranchName, path, code, $"feat: {path}", ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Commit xatosi: {Path}", path); }
        }
    }

    private async Task CreatePullRequestAsync(ProjectSession session, string originalTask, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(session.RepoName) || string.IsNullOrWhiteSpace(session.BranchName))
            return;

        try
        {
            var pr = await _gitHubService.CreatePullRequestAsync(
                session.RepoName, session.BranchName,
                $"Task: {originalTask}",
                $"🤖 Generated by ClaudeFarm\n\nTask: {originalTask}",
                ct);
            session.PullRequestNumber = pr.Number;
            session.PullRequestUrl    = pr.HtmlUrl;
            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PR yaratishda xato"); }
    }
}
