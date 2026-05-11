using AgentFarm.Agents.Agents;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Bot.Services;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Sequential pipeline: Developer → QA (Developer kodini ko'radi) → Reviewer (ikkalasini ko'radi).
/// </summary>
public sealed class AgentPipelineRunner : IAgentPipelineRunner
{
    private readonly DeveloperAgent              _developer;
    private readonly QAAgent                     _qa;
    private readonly ReviewerAgent               _reviewer;
    private readonly ITelegramMessageSender      _sender;
    private readonly ILogger<AgentPipelineRunner> _logger;

    public AgentPipelineRunner(
        DeveloperAgent               developer,
        QAAgent                      qa,
        ReviewerAgent                reviewer,
        ITelegramMessageSender       sender,
        ILogger<AgentPipelineRunner> logger)
    {
        _developer = developer;
        _qa        = qa;
        _reviewer  = reviewer;
        _sender    = sender;
        _logger    = logger;
    }

    public async Task<PipelineResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var responses = new List<AgentResponse>();

        _logger.LogInformation("Pipeline boshlandi. TaskId={TaskId}, Rollar={Roles}",
            request.TaskId, string.Join(", ", request.RequestedRoles));

        // 1. Developer
        string? devCode = null;
        if (request.RequestedRoles.Contains(AgentRole.Developer))
        {
            var devResponse = await _developer.RunAsync(request, previousContext: null, ct);
            responses.Add(devResponse);
            if (devResponse.Status == AgentStatus.Completed)
                devCode = devResponse.Content;
        }

        // 2. QA — Developer kodini context sifatida oladi
        string? qaFindings = null;
        if (request.RequestedRoles.Contains(AgentRole.QA))
        {
            var qaResponse = await _qa.RunAsync(request, previousContext: devCode, ct);
            responses.Add(qaResponse);
            if (qaResponse.Status == AgentStatus.Completed)
                qaFindings = qaResponse.Content;
        }

        // 3. Reviewer — Developer kodi + QA topilmalarini ko'radi
        if (request.RequestedRoles.Contains(AgentRole.Reviewer))
        {
            var reviewerContext = BuildReviewerContext(devCode, qaFindings);
            var reviewResponse  = await _reviewer.RunAsync(request, previousContext: reviewerContext, ct);
            responses.Add(reviewResponse);
        }

        sw.Stop();

        var successful = responses.Count(r => r.Status == AgentStatus.Completed);
        var total      = responses.Count;

        await _sender.SendTextAsync(
            request.ChatId,
            $"✅ Pipeline tugadi — {successful}/{total} muvaffaqiyatli \\| {(int)sw.Elapsed.TotalSeconds}s",
            useMarkdown: true,
            ct);

        return new PipelineResult
        {
            TaskId         = request.TaskId,
            ChatId         = request.ChatId,
            OriginalPrompt = request.Prompt,
            AgentResponses = responses,
            TotalDuration  = sw.Elapsed
        };
    }

    private static string? BuildReviewerContext(string? devCode, string? qaFindings)
    {
        if (devCode is null && qaFindings is null) return null;

        var sb = new System.Text.StringBuilder();
        if (devCode is not null)
        {
            sb.AppendLine("### Developer yozgan kod");
            sb.AppendLine(devCode);
        }
        if (qaFindings is not null)
        {
            sb.AppendLine();
            sb.AppendLine("### QA topilmalari");
            sb.AppendLine(qaFindings);
        }
        return sb.ToString();
    }
}
