using System.Diagnostics;
using System.Text;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Base;

public abstract class AgentBase
{
    protected readonly ClaudeApiClient        ApiClient;
    protected readonly ITelegramMessageSender Sender;
    protected readonly ILogger                Logger;

    protected AgentBase(ClaudeApiClient apiClient, ITelegramMessageSender sender, ILogger logger)
    {
        ApiClient = apiClient;
        Sender    = sender;
        Logger    = logger;
    }

    public abstract AgentRole Role { get; }
    protected abstract string SystemPrompt { get; }

    public virtual bool IsEnabled => true;

    /// <summary>Agentga xos token chegarasi. null bo'lsa options dan oladi.</summary>
    protected virtual int? MaxTokensOverride => null;

    public virtual async Task<AgentResponse> RunAsync(AgentRequest request, string? previousContext = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        await Sender.SendTextAsync(request.ChatId, $"⏳ [{RoleLabel}] ishlayapti...", useMarkdown: false, ct);

        try
        {
            var userMessage = BuildUserMessage(request, previousContext);
            var (content, tokens) = await ApiClient.CompleteAsync(SystemPrompt, userMessage, MaxTokensOverride, jsonMode: false, ct);
            sw.Stop();

            var summary = ExtractSummary(content);
            var doneMsg = summary != null ? $"[{RoleLabel}] {summary}" : $"✅ [{RoleLabel}] tugadi";
            await Sender.SendTextAsync(request.ChatId, doneMsg, useMarkdown: false, ct);

            return AgentResponse.Success(request.TaskId, Role, content, tokens, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "{Role} agent xatosi. TaskId={TaskId}", Role, request.TaskId);
            await Sender.SendTextAsync(request.ChatId, $"❌ [{RoleLabel}] xato: {ex.Message}", useMarkdown: false, ct);
            return AgentResponse.Failure(request.TaskId, Role, ex.Message, sw.Elapsed);
        }
    }

    protected virtual string BuildUserMessage(AgentRequest request, string? previousContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine(request.Prompt);

        // File-aware: FilePath, Namespace, ProjectContext
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            sb.AppendLine();
            sb.AppendLine($"## FAYL: {request.FilePath}");
            if (!string.IsNullOrWhiteSpace(request.Namespace))
            {
                sb.AppendLine($"Namespace: {request.Namespace}");
            }
            sb.AppendLine();
            sb.AppendLine("Faqat shu fayl uchun kod yoz. Boshqa hech narsa yo'q.");
            sb.AppendLine("Kod to'g'ri compile bo'lishi shart (using, namespace, class, method).");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectContext))
        {
            sb.AppendLine();
            sb.AppendLine("## Loyiha strukturasi");
            var ctx = request.ProjectContext.Length > 1000
                ? request.ProjectContext[..1000] + "\n...[qisqartirildi]"
                : request.ProjectContext;
            sb.AppendLine(ctx);
        }

        if (!string.IsNullOrWhiteSpace(previousContext))
        {
            sb.AppendLine();
            sb.AppendLine("## Oldingi agent natijasi");
            var ctx = previousContext.Length > 2000
                ? previousContext[..2000] + "\n...[qisqartirildi]"
                : previousContext;
            sb.AppendLine(ctx);
        }

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            sb.AppendLine();
            sb.AppendLine("## Qo'shimcha kontekst");
            sb.AppendLine(request.Context);
        }

        return sb.ToString();
    }

    protected static string? ExtractSummary(string content)
    {
        const string startTag = "=== SUMMARY ===";
        const string endTag   = "=== END SUMMARY ===";
        var start = content.IndexOf(startTag, StringComparison.Ordinal);
        var end   = content.IndexOf(endTag,   StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start) return null;
        return content[(start + startTag.Length)..end].Trim();
    }

    private string RoleLabel => Role switch
    {
        AgentRole.Backend         => "Backend",
        AgentRole.Frontend        => "Frontend",
        AgentRole.DevOps          => "DevOps",
        AgentRole.QA              => "QA",
        AgentRole.Reviewer        => "Reviewer",
        AgentRole.BusinessAnalyst => "Business Analyst",
        AgentRole.Security        => "Security",
        AgentRole.DatabaseAdmin   => "Database Admin",
        AgentRole.Architect       => "Architect",
        AgentRole.Orchestrator    => "Orchestrator",
        _                         => Role.ToString()
    };
}
