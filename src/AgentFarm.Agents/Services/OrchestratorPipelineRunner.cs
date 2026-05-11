using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentFarm.Agents.Agents;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Bot.Services;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Orchestrator arxitekturasi:
/// 1. Orchestrator vazifani bo'ladi
/// 2. Parallel: Developer1, Developer2, Developer3 ishlaydi
/// 3. QA barcha developer kodlarini ko'radi
/// 4. Reviewer barcha natijalarni ko'radi
/// </summary>
public sealed class OrchestratorPipelineRunner : IAgentPipelineRunner
{
    private readonly OrchestratorAgent                 _orchestrator;
    private readonly DeveloperAgent                    _developer1;
    private readonly DeveloperAgent                    _developer2;
    private readonly DeveloperAgent                    _developer3;
    private readonly QAAgent                           _qa;
    private readonly ReviewerAgent                     _reviewer;
    private readonly InMemorySessionStore              _sessionStore;
    private readonly ITelegramMessageSender            _sender;
    private readonly ILogger<OrchestratorPipelineRunner> _logger;

    public OrchestratorPipelineRunner(
        OrchestratorAgent orchestrator,
        DeveloperAgent    developer1,
        DeveloperAgent    developer2,
        DeveloperAgent    developer3,
        QAAgent           qa,
        ReviewerAgent     reviewer,
        InMemorySessionStore sessionStore,
        ITelegramMessageSender sender,
        ILogger<OrchestratorPipelineRunner> logger)
    {
        _orchestrator  = orchestrator;
        _developer1    = developer1;
        _developer2    = developer2;
        _developer3    = developer3;
        _qa            = qa;
        _reviewer      = reviewer;
        _sessionStore  = sessionStore;
        _sender        = sender;
        _logger        = logger;
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

            // Telegram ga xabar
            await _sender.SendTextAsync(request.ChatId,
                $"📋 Vazifa {subtasks.Count} qismga bo'lindi:\n" +
                string.Join("\n", subtasks.Select((st, i) =>
                    $"{i + 1}. [{st.AssignedTo}] {st.Description.Substring(0, Math.Min(50, st.Description.Length))}...")),
                useMarkdown: false, ct);

            // 2️⃣ Parallel: Developer1, Developer2, Developer3
            session.Status = SessionStatus.Developing;
            _sessionStore.UpdateSession(session);

            var developerTasks = subtasks.Select(async subtask =>
            {
                var agent = GetDeveloperAgent(subtask.AssignedTo);
                var subRequest = CreateSubRequest(request, subtask);

                subtask.Status = SubTaskStatus.InProgress;
                _sessionStore.UpdateSession(session);

                var response = await agent.RunAsync(subRequest, previousContext: null, ct);

                if (response.Status == AgentStatus.Completed)
                {
                    subtask.Code = response.Content;
                    subtask.Status = SubTaskStatus.Done;

                    // Telegram ga darhol xabar
                    await _sender.SendTextAsync(request.ChatId,
                        $"✅ [{subtask.AssignedTo}] vazifani tugatdi", useMarkdown: false, ct);
                }
                else
                {
                    subtask.Status = SubTaskStatus.Failed;
                    await _sender.SendTextAsync(request.ChatId,
                        $"❌ [{subtask.AssignedTo}] xato", useMarkdown: false, ct);
                }

                _sessionStore.UpdateSession(session);
                return response;
            }).ToList();

            var developerResponses = await Task.WhenAll(developerTasks);
            responses.AddRange(developerResponses);

            // 3️⃣ QA - barcha developer kodlarini ko'radi
            session.Status = SessionStatus.QA;
            _sessionStore.UpdateSession(session);

            var allDeveloperCode = BuildAllDeveloperCode(session.SubTasks);
            var qaResponse = await _qa.RunAsync(request, previousContext: allDeveloperCode, ct);
            responses.Add(qaResponse);

            // 4️⃣ Reviewer - Developer kodlari + QA natijasini ko'radi
            session.Status = SessionStatus.Reviewing;
            _sessionStore.UpdateSession(session);

            var reviewerContext = BuildReviewerContext(allDeveloperCode, qaResponse.Content);
            var reviewerResponse = await _reviewer.RunAsync(request, previousContext: reviewerContext, ct);
            responses.Add(reviewerResponse);

            // Yakuniy natija
            session.FinalResult = reviewerResponse.Content;
            session.Status = SessionStatus.Done;
            _sessionStore.UpdateSession(session);

            sw.Stop();

            var successful = responses.Count(r => r.Status == AgentStatus.Completed);
            var total = responses.Count;

            await _sender.SendTextAsync(request.ChatId,
                $"✅ Pipeline tugadi — {successful}/{total} muvaffaqiyatli | {(int)sw.Elapsed.TotalSeconds}s",
                useMarkdown: false, ct);

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

    private async Task<List<SubTask>?> ParseSubtasksWithRetry(string json, AgentRequest request, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var subtasksArray = doc.RootElement.GetProperty("subtasks");

                var result = new List<SubTask>();
                foreach (var item in subtasksArray.EnumerateArray())
                {
                    var developerName = item.GetProperty("developer").GetString() ?? "Developer1";
                    var role = developerName switch
                    {
                        "Developer1" => AgentRole.Developer1,
                        "Developer2" => AgentRole.Developer2,
                        "Developer3" => AgentRole.Developer3,
                        _ => AgentRole.Developer1
                    };

                    result.Add(new SubTask
                    {
                        AssignedTo = role,
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

        return null;
    }

    private DeveloperAgent GetDeveloperAgent(AgentRole role) => role switch
    {
        AgentRole.Developer1 => _developer1,
        AgentRole.Developer2 => _developer2,
        AgentRole.Developer3 => _developer3,
        _ => _developer1
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
                sb.AppendLine($"### [{subtask.AssignedTo}] {subtask.Description}");
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
}
