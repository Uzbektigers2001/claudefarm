using AgentFarm.Agents.Agents;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Bot.Services;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AgentFarm.Agents.Services;

/// <summary>
/// Orchestrator — agentlarni parallel ishlatadi,
/// har biri o'z natijasini Telegram ga yuboradi.
/// </summary>
public sealed class AgentPipelineRunner : IAgentPipelineRunner
{
    private readonly DeveloperAgent             _developer;
    private readonly QAAgent                    _qa;
    private readonly ReviewerAgent              _reviewer;
    private readonly ITelegramMessageSender     _sender;
    private readonly ILogger<AgentPipelineRunner> _logger;

    public AgentPipelineRunner(
        DeveloperAgent              developer,
        QAAgent                     qa,
        ReviewerAgent               reviewer,
        ITelegramMessageSender      sender,
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
        _logger.LogInformation("Pipeline boshlandi. TaskId={TaskId}", request.TaskId);

        // Faqat so'ralgan rollarni parallel ishlatamiz
        var tasks = request.RequestedRoles
            .Select(role => RunAgentAsync(role, request, ct))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        sw.Stop();

        // Yakuniy xulosa yuboramiz
        var successful = responses.Count(r => r.Status == AgentStatus.Completed);
        await _sender.SendTextAsync(
            request.ChatId,
            $"✅ Barcha agentlar tugadi \\— {successful}/{responses.Length} muvaffaqiyatli\\. " +
            $"Jami: {(int)sw.Elapsed.TotalSeconds}s",
            useMarkdown: true,
            ct);

        return new PipelineResult
        {
            TaskId          = request.TaskId,
            ChatId          = request.ChatId,
            OriginalPrompt  = request.Prompt,
            AgentResponses  = responses,
            TotalDuration   = sw.Elapsed
        };
    }

    private Task<AgentResponse> RunAgentAsync(AgentRole role, AgentRequest request, CancellationToken ct) =>
        role switch
        {
            AgentRole.Developer => _developer.RunAsync(request, ct),
            AgentRole.QA        => _qa.RunAsync(request, ct),
            AgentRole.Reviewer  => _reviewer.RunAsync(request, ct),
            _                   => Task.FromResult(AgentResponse.Failure(
                                       request.TaskId, role, $"Noma'lum rol: {role}", TimeSpan.Zero))
        };
}
