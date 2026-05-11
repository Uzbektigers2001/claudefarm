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
    private readonly OrchestratorAgent                    _orchestrator;
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
    private readonly IEscalationStore                     _escalationStore;
    private readonly GitHubService                        _gitHubService;
    private readonly ProjectRepoService                   _projectRepoService;
    private readonly ILogger<OrchestratorPipelineRunner>  _logger;

    private const int MaxPlannerIterations   = 2;
    private const int MaxAnalystIterations   = 2;
    private const int MaxArchitectIterations = 2;
    private const int MaxDeveloperIterations = 5;
    private const int MaxReviewerIterations  = 3;
    private const int MaxQaIterations        = 3;

    public OrchestratorPipelineRunner(
        OrchestratorAgent       orchestrator,
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
        IEscalationStore        escalationStore,
        GitHubService           gitHubService,
        ProjectRepoService      projectRepoService,
        ILogger<OrchestratorPipelineRunner> logger)
    {
        _orchestrator       = orchestrator;
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
        _escalationStore    = escalationStore;
        _gitHubService      = gitHubService;
        _projectRepoService = projectRepoService;
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
            {
                string retryCtx  = string.Empty;
                bool   stageDone = false;

                for (int i = 1; i <= MaxPlannerIterations && !stageDone; i++)
                {
                    session.CurrentIteration = i;
                    var resp = await _planner.RunAsync(
                        Req(request, request.Prompt, i > 1 ? retryCtx : null), null, ct);
                    responses.Add(resp);
                    AddHistory(session, AgentRole.Planner, resp.Content);

                    if (resp.Status != AgentStatus.Completed)
                    {
                        var action = await EscalateAsync(request.ChatId, "Planner", "Agent javobi xato", session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        if (action == "") { plannerOutput = resp.Content; stageDone = true; break; }
                        retryCtx = action;
                        i = MaxPlannerIterations; // next will trigger escalation check
                        continue;
                    }

                    var dec = await _orchestrator.DecideAsync(AgentRole.Planner, resp.Content, session, ct);

                    switch (dec.Decision)
                    {
                        case "continue":
                            plannerOutput = resp.Content;
                            await Send(request.ChatId, "[Orchestrator] Planner OK → Analyst", ct);
                            stageDone = true;
                            break;

                        case "retry_current":
                            if (i >= MaxPlannerIterations)
                                goto planner_escalate;
                            retryCtx = dec.Instructions;
                            await Send(request.ChatId, $"[Orchestrator] Planner qayta ({i}/{MaxPlannerIterations}): {dec.Reason}", ct);
                            break;

                        default:
                            goto planner_escalate;
                    }
                    continue;

                    planner_escalate:
                    {
                        var action = await EscalateAsync(request.ChatId, "Planner", dec.Reason, session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        if (action == "") { plannerOutput = resp.Content; stageDone = true; break; }
                        retryCtx = action;
                        // one more try with user instructions
                        session.CurrentIteration++;
                        var retryResp = await _planner.RunAsync(Req(request, request.Prompt, action), null, ct);
                        responses.Add(retryResp);
                        AddHistory(session, AgentRole.Planner, retryResp.Content);
                        plannerOutput = retryResp.Status == AgentStatus.Completed ? retryResp.Content : resp.Content;
                        stageDone = true;
                    }
                }
                if (!stageDone) return Fail(request, responses, sw.Elapsed);
            }
            await Send(request.ChatId, "📋 Planner: qadamlar tayyor", ct);

            // ===== BOSQICH 2: ANALYST =====
            string analystOutput = string.Empty;
            {
                string retryCtx  = string.Empty;
                bool   stageDone = false;

                for (int i = 1; i <= MaxAnalystIterations && !stageDone; i++)
                {
                    session.CurrentIteration = i;
                    var prompt = $"{request.Prompt}\n\nPlanner qadamlari:\n{plannerOutput}";
                    var resp   = await _analyst.RunAsync(
                        Req(request, prompt, i > 1 ? retryCtx : null), null, ct);
                    responses.Add(resp);
                    AddHistory(session, AgentRole.Analyst, resp.Content);

                    if (resp.Status != AgentStatus.Completed)
                    {
                        var action = await EscalateAsync(request.ChatId, "Analyst", "Agent javobi xato", session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        if (action == "") { analystOutput = resp.Content; stageDone = true; break; }
                        retryCtx = action;
                        i = MaxAnalystIterations;
                        continue;
                    }

                    var dec = await _orchestrator.DecideAsync(AgentRole.Analyst, resp.Content, session, ct);

                    switch (dec.Decision)
                    {
                        case "continue":
                            analystOutput = resp.Content;
                            await Send(request.ChatId, "[Orchestrator] Analyst OK → Architect", ct);
                            stageDone = true;
                            break;

                        case "retry_current":
                            if (i >= MaxAnalystIterations)
                                goto analyst_escalate;
                            retryCtx = dec.Instructions;
                            await Send(request.ChatId, $"[Orchestrator] Analyst qayta ({i}/{MaxAnalystIterations}): {dec.Reason}", ct);
                            break;

                        case "retry_previous":
                            await Send(request.ChatId, $"[Orchestrator] Planner ga qaytildi: {dec.Reason}", ct);
                            goto analyst_escalate;

                        default:
                            goto analyst_escalate;
                    }
                    continue;

                    analyst_escalate:
                    {
                        var action = await EscalateAsync(request.ChatId, "Analyst", dec.Reason, session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        if (action == "") { analystOutput = resp.Content; stageDone = true; break; }
                        session.CurrentIteration++;
                        var retryResp = await _analyst.RunAsync(Req(request, $"{request.Prompt}\n\nPlanner:\n{plannerOutput}", action), null, ct);
                        responses.Add(retryResp);
                        AddHistory(session, AgentRole.Analyst, retryResp.Content);
                        analystOutput = retryResp.Status == AgentStatus.Completed ? retryResp.Content : resp.Content;
                        stageDone = true;
                    }
                }
                if (!stageDone) return Fail(request, responses, sw.Elapsed);
            }
            session.Requirements = analystOutput;
            _sessionStore.UpdateSession(session);
            await Send(request.ChatId, "📝 Analyst: talablar tayyor", ct);

            // ===== BOSQICH 3: ARCHITECT =====
            for (int i = 1; i <= MaxArchitectIterations + 1; i++) // +1 for potential user retry
            {
                session.CurrentIteration = i;
                var prompt = $"{request.Prompt}\n\nTalablar:\n{analystOutput}";
                var resp   = await _architect.RunAsync(
                    Req(request, prompt, i > 1 ? session.ArchitectPlanJson : null), null, ct);
                responses.Add(resp);
                AddHistory(session, AgentRole.Architect, resp.Content);

                if (resp.Status != AgentStatus.Completed)
                {
                    var action = await EscalateAsync(request.ChatId, "Architect", "Agent javobi xato", session, ct);
                    if (action == null) return Fail(request, responses, sw.Elapsed);
                    if (action == "") break; // skip with whatever plan we have
                    session.ArchitectPlanJson += $"\n\nFoydalanuvchi ko'rsatmasi: {action}";
                    continue;
                }

                plan = ParseArchitectPlan(resp.Content);
                if (plan == null)
                {
                    session.ArchitectPlanJson = resp.Content + "\n\nXATO: JSON format noto'g'ri, qayta yoz.";
                    if (i >= MaxArchitectIterations)
                    {
                        var action = await EscalateAsync(request.ChatId, "Architect", "JSON parse xatosi", session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        if (action == "") break;
                        session.ArchitectPlanJson += $"\nFoydalanuvchi: {action}";
                    }
                    await Send(request.ChatId, $"[Orchestrator] Architect qayta ({i}/{MaxArchitectIterations}): JSON parse xatosi", ct);
                    continue;
                }

                session.ArchitectPlanJson = resp.Content;
                _sessionStore.UpdateSession(session);

                var dec = await _orchestrator.DecideAsync(AgentRole.Architect, resp.Content, session, ct);

                switch (dec.Decision)
                {
                    case "continue":
                        await Send(request.ChatId, "[Orchestrator] Architect OK → Developer", ct);
                        goto architect_done;

                    case "retry_current":
                        if (i >= MaxArchitectIterations)
                        {
                            var action = await EscalateAsync(request.ChatId, "Architect", dec.Reason, session, ct);
                            if (action == null) return Fail(request, responses, sw.Elapsed);
                            if (action == "") goto architect_done;
                            session.ArchitectPlanJson += $"\nFoydalanuvchi: {action}";
                        }
                        else
                        {
                            session.ArchitectPlanJson += $"\n\nTuzatish: {dec.Instructions}";
                            await Send(request.ChatId, $"[Orchestrator] Architect qayta ({i}/{MaxArchitectIterations}): {dec.Reason}", ct);
                        }
                        break;

                    case "retry_previous":
                        await Send(request.ChatId, $"[Orchestrator] Analyst ga qaytildi: {dec.Reason}", ct);
                        var esc = await EscalateAsync(request.ChatId, "Architect", dec.Reason, session, ct);
                        if (esc == null) return Fail(request, responses, sw.Elapsed);
                        if (esc == "") goto architect_done;
                        session.ArchitectPlanJson += $"\nFoydalanuvchi: {esc}";
                        break;

                    default:
                        var escDef = await EscalateAsync(request.ChatId, "Architect", dec.Reason, session, ct);
                        if (escDef == null) return Fail(request, responses, sw.Elapsed);
                        if (escDef == "") goto architect_done;
                        session.ArchitectPlanJson += $"\nFoydalanuvchi: {escDef}";
                        break;
                }
            }
            architect_done:

            if (plan == null) return Fail(request, responses, sw.Elapsed);
            await Send(request.ChatId, $"🏗️ Architect: {plan.Files.Count} fayl", ct);

            workDir = Path.Combine("/tmp/claudefarm", session.SessionId.ToString("N"));
            await _projectBuilder.BuildProjectAsync(plan, workDir, ct);

            // ===== BOSQICH 4: DEVELOPER LOOP =====
            {
                string retryCtx  = string.Empty;
                bool   stageDone = false;

                for (int iter = 1; iter <= MaxDeveloperIterations && !stageDone; iter++)
                {
                    session.CurrentIteration = iter;
                    await Send(request.ChatId, $"🔨 Developer ({iter}/{MaxDeveloperIterations})...", ct);

                    AgentResponse? lastResp = null;
                    foreach (var file in plan.Files)
                    {
                        var agent      = GetAgentForRole(file.AssignTo) ?? _backend;
                        var filePrompt = BuildFilePrompt(request.Prompt, analystOutput, file, session.BuildErrors, retryCtx);
                        var fileResp   = await agent.RunAsync(
                            Req(request, filePrompt, null,
                                filePath: file.Path,
                                ns:       ExtractNamespace(file.Path),
                                projCtx:  BuildProjectContext(plan)),
                            BuildWrittenFilesContext(session), ct);
                        responses.Add(fileResp);
                        AddHistory(session, agent.Role, fileResp.Content);
                        lastResp = fileResp;

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

                    var dec = await _orchestrator.DecideAsync(
                        AgentRole.Backend,
                        lastResp?.Content ?? (build.Success ? "Build OK" : string.Join("\n", build.Errors.Take(5))),
                        session, ct);

                    switch (dec.Decision)
                    {
                        case "continue":
                            await Send(request.ChatId, "[Orchestrator] Developer OK → Reviewer", ct);
                            stageDone = true;
                            break;

                        case "retry_current":
                            if (iter >= MaxDeveloperIterations)
                            {
                                var action = await EscalateAsync(request.ChatId, "Developer", dec.Reason, session, ct);
                                if (action == null) return Fail(request, responses, sw.Elapsed);
                                if (action == "") { stageDone = true; break; }
                                retryCtx = action;
                            }
                            else
                            {
                                retryCtx = dec.Instructions;
                                await Send(request.ChatId, $"[Orchestrator] Developer qayta ({iter}/{MaxDeveloperIterations}): {dec.Reason}", ct);
                            }
                            break;

                        case "retry_previous":
                            await Send(request.ChatId, $"[Orchestrator] Architect ga qaytildi: {dec.Reason}", ct);
                            var escDev = await EscalateAsync(request.ChatId, "Developer", dec.Reason, session, ct);
                            if (escDev == null) return Fail(request, responses, sw.Elapsed);
                            if (escDev == "") { stageDone = true; break; }
                            retryCtx = escDev;
                            break;

                        default:
                            var escDefDev = await EscalateAsync(request.ChatId, "Developer", dec.Reason, session, ct);
                            if (escDefDev == null) return Fail(request, responses, sw.Elapsed);
                            if (escDefDev == "") { stageDone = true; break; }
                            retryCtx = escDefDev;
                            break;
                    }
                }
                if (!stageDone) return Fail(request, responses, sw.Elapsed);
            }

            // ===== BOSQICH 5: REVIEWER LOOP =====
            {
                bool stageDone = false;
                for (int iter = 1; iter <= MaxReviewerIterations && !stageDone; iter++)
                {
                    session.CurrentIteration = iter;
                    await Send(request.ChatId, $"👀 Reviewer ({iter}/{MaxReviewerIterations})...", ct);

                    var allCode = BuildAllFilesContext(session);
                    var resp    = await _reviewer.RunAsync(Req(request, request.Prompt), allCode, ct);
                    responses.Add(resp);
                    AddHistory(session, AgentRole.Reviewer, resp.Content);

                    if (resp.Status != AgentStatus.Completed)
                    {
                        if (iter < MaxReviewerIterations) continue;
                        var action = await EscalateAsync(request.ChatId, "Reviewer", "Agent javobi xato", session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        stageDone = true;
                        break;
                    }

                    var dec = await _orchestrator.DecideAsync(AgentRole.Reviewer, resp.Content, session, ct);

                    switch (dec.Decision)
                    {
                        case "continue":
                            await Send(request.ChatId, "[Orchestrator] Reviewer OK → QA", ct);
                            stageDone = true;
                            break;

                        case "retry_current":
                            if (iter >= MaxReviewerIterations)
                            {
                                var action = await EscalateAsync(request.ChatId, "Reviewer", dec.Reason, session, ct);
                                if (action == null) return Fail(request, responses, sw.Elapsed);
                                if (action == "") { stageDone = true; break; }
                                await FixFilesAsync(action, request, plan, session, responses, workDir!, ct);
                            }
                            else
                            {
                                await Send(request.ChatId, $"[Orchestrator] Reviewer qayta ({iter}/{MaxReviewerIterations}): {dec.Reason}", ct);
                                await FixFilesAsync(dec.Instructions, request, plan, session, responses, workDir!, ct);
                            }
                            break;

                        case "retry_previous":
                            await Send(request.ChatId, $"[Orchestrator] Developer ga qaytildi: {dec.Reason}", ct);
                            var escRev = await EscalateAsync(request.ChatId, "Reviewer", dec.Reason, session, ct);
                            if (escRev == null) return Fail(request, responses, sw.Elapsed);
                            if (escRev == "") { stageDone = true; break; }
                            await FixFilesAsync(escRev, request, plan, session, responses, workDir!, ct);
                            break;

                        default:
                            var escDefRev = await EscalateAsync(request.ChatId, "Reviewer", dec.Reason, session, ct);
                            if (escDefRev == null) return Fail(request, responses, sw.Elapsed);
                            if (escDefRev == "") { stageDone = true; break; }
                            await FixFilesAsync(escDefRev, request, plan, session, responses, workDir!, ct);
                            break;
                    }
                }
            }

            // ===== BOSQICH 6: QA LOOP =====
            {
                bool stageDone = false;
                for (int iter = 1; iter <= MaxQaIterations && !stageDone; iter++)
                {
                    session.CurrentIteration = iter;
                    await Send(request.ChatId, $"🧪 QA ({iter}/{MaxQaIterations})...", ct);

                    var allCode = BuildAllFilesContext(session);
                    var resp    = await _qa.RunAsync(Req(request, request.Prompt), allCode, ct);
                    responses.Add(resp);
                    AddHistory(session, AgentRole.QA, resp.Content);

                    if (resp.Status != AgentStatus.Completed)
                    {
                        if (iter < MaxQaIterations) continue;
                        var action = await EscalateAsync(request.ChatId, "QA", "Agent javobi xato", session, ct);
                        if (action == null) return Fail(request, responses, sw.Elapsed);
                        stageDone = true;
                        break;
                    }

                    var dec = await _orchestrator.DecideAsync(AgentRole.QA, resp.Content, session, ct);

                    switch (dec.Decision)
                    {
                        case "continue":
                            await Send(request.ChatId, "[Orchestrator] QA OK → Git", ct);
                            stageDone = true;
                            break;

                        case "retry_current":
                            if (iter >= MaxQaIterations)
                            {
                                var action = await EscalateAsync(request.ChatId, "QA", dec.Reason, session, ct);
                                if (action == null) return Fail(request, responses, sw.Elapsed);
                                if (action == "") { stageDone = true; break; }
                                await FixFilesAsync(action, request, plan, session, responses, workDir!, ct);
                            }
                            else
                            {
                                await Send(request.ChatId, $"[Orchestrator] QA qayta ({iter}/{MaxQaIterations}): {dec.Reason}", ct);
                                await FixFilesAsync(dec.Instructions, request, plan, session, responses, workDir!, ct);
                            }
                            break;

                        case "retry_previous":
                            await Send(request.ChatId, $"[Orchestrator] Developer ga qaytildi: {dec.Reason}", ct);
                            var escQa = await EscalateAsync(request.ChatId, "QA", dec.Reason, session, ct);
                            if (escQa == null) return Fail(request, responses, sw.Elapsed);
                            if (escQa == "") { stageDone = true; break; }
                            await FixFilesAsync(escQa, request, plan, session, responses, workDir!, ct);
                            break;

                        default:
                            var escDefQa = await EscalateAsync(request.ChatId, "QA", dec.Reason, session, ct);
                            if (escDefQa == null) return Fail(request, responses, sw.Elapsed);
                            if (escDefQa == "") { stageDone = true; break; }
                            await FixFilesAsync(escDefQa, request, plan, session, responses, workDir!, ct);
                            break;
                    }
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

    // ===== Escalation =====

    /// <summary>
    /// Foydalanuvchidan javob kutadi.
    /// Returns: null = stop, "" = skip, text = instructions for retry
    /// </summary>
    private async Task<string?> EscalateAsync(
        long chatId, string roleName, string reason, ProjectSession session, CancellationToken ct)
    {
        var msg =
            $"⚠️ Ferma yordam kerak!\n\n" +
            $"Muammo: {reason}\n" +
            $"Agent: {roleName}\n" +
            $"Kontekst: {session.Files.Count} fayl yozilgan, build: {(session.BuildErrors.Any() ? "XATO" : "OK")}\n\n" +
            $"Nima qilishni xohlaysiz?\n" +
            $"/continue — yangi ko'rsatma bering\n" +
            $"/skip — bu qadamni o'tkazib yuboring\n" +
            $"/stop — vazifani to'xtatish";

        await Send(chatId, msg, ct);
        _logger.LogWarning("Eskalatsiya: {Role} — {Reason}", roleName, reason);

        var escalation = new EscalationSession
        {
            SessionId  = session.SessionId,
            ChatId     = chatId,
            FailedRole = AgentRole.Orchestrator,
            Problem    = reason,
            Context    = $"{session.Files.Count} fayl, build: {(session.BuildErrors.Any() ? "XATO" : "OK")}"
        };
        _escalationStore.AddEscalation(escalation);

        var userResponse = await escalation.Tcs.Task;
        _escalationStore.RemoveEscalation(chatId);

        return userResponse == IEscalationStore.StopToken ? null  :
               userResponse == IEscalationStore.SkipToken ? ""    :
               userResponse;
    }

    // ===== Private Helpers =====

    private Task Send(long chatId, string text, CancellationToken ct = default) =>
        _sender.SendTextAsync(chatId, text, useMarkdown: false, ct);

    private static void AddHistory(ProjectSession session, AgentRole role, string content) =>
        session.History.Add(new AgentMessage { Role = role, Content = content });

    private static AgentRequest Req(
        AgentRequest original,
        string prompt,
        string? context  = null,
        string? filePath = null,
        string? ns       = null,
        string? projCtx  = null) =>
        new()
        {
            ChatId         = original.ChatId,
            Prompt         = prompt,
            Context        = context,
            FilePath       = filePath,
            Namespace      = ns,
            ProjectContext = projCtx
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

    private async Task FixFilesAsync(
        string instructions, AgentRequest request, ArchitectPlan plan,
        ProjectSession session, List<AgentResponse> responses, string workDir, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instructions)) return;
        var allCode = BuildAllFilesContext(session);
        foreach (var file in plan.Files)
        {
            var agent     = GetAgentForRole(file.AssignTo) ?? _backend;
            var fixPrompt = $"Muammolarni tuzat:\n{instructions}\n\nFayl: {file.Path}\n{file.Description}";
            var fixResp   = await agent.RunAsync(
                Req(request, fixPrompt, filePath: file.Path, projCtx: BuildProjectContext(plan)),
                allCode, ct);
            responses.Add(fixResp);
            AddHistory(session, agent.Role, fixResp.Content);

            if (fixResp.Status == AgentStatus.Completed)
            {
                var code = _codeWriter.ExtractCode(fixResp.Content);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    await _codeWriter.WriteFileAsync(workDir, file.Path, code, ct);
                    session.Files[file.Path] = code;
                }
            }
        }
        _sessionStore.UpdateSession(session);
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

    private static string BuildFilePrompt(
        string originalPrompt, string requirements, ArchitectFile file,
        List<string> buildErrors, string retryInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(originalPrompt);
        sb.AppendLine();
        sb.AppendLine($"## Fayl: {file.Path}");
        sb.AppendLine($"Tavsif: {file.Description}");
        sb.AppendLine();
        sb.AppendLine("## Texnik talablar");
        sb.AppendLine(requirements);

        if (!string.IsNullOrWhiteSpace(retryInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("## Orchestrator ko'rsatmasi");
            sb.AppendLine(retryInstructions);
        }

        if (buildErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Build xatolari");
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
            sb.AppendLine($"  - {f.Path} [{f.AssignTo}]");
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
        var parts   = filePath.Replace("\\", "/").Split('/');
        var nsParts = new List<string>();
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
            session.RepoName   = repoName;
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
                $"🤖 Generated by ClaudeFarm\n\nTask: {originalTask}", ct);
            session.PullRequestNumber = pr.Number;
            session.PullRequestUrl    = pr.HtmlUrl;
            _sessionStore.UpdateSession(session);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PR yaratishda xato"); }
    }
}
