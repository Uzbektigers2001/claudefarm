using System.Diagnostics;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Base;

/// <summary>
/// Barcha agentlar uchun asosiy klass.
/// Har agent: system prompt + Claude API + Telegram xabar yuborish.
/// </summary>
public abstract class AgentBase
{
    protected readonly ClaudeApiClient      ApiClient;
    protected readonly ITelegramMessageSender Sender;
    protected readonly ILogger              Logger;

    protected AgentBase(
        ClaudeApiClient       apiClient,
        ITelegramMessageSender sender,
        ILogger               logger)
    {
        ApiClient = apiClient;
        Sender    = sender;
        Logger    = logger;
    }

    public abstract AgentRole Role { get; }
    protected abstract string SystemPrompt { get; }

    /// <summary>
    /// Agentni ishga tushiradi, natijani Telegram ga yuboradi.
    /// </summary>
    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // "Ishlamoqda..." xabari
        await Sender.SendTextAsync(
            request.ChatId,
            $"⏳ *\\[{RoleLabel}\\]* ishlayapti\\.\\.\\.",
            useMarkdown: true,
            ct);

        try
        {
            var userMessage = BuildUserMessage(request);
            var (content, tokens) = await ApiClient.CompleteAsync(SystemPrompt, userMessage, ct);

            sw.Stop();

            // Natijani Telegram ga yuboramiz
            await Sender.SendMessageAsync(new TelegramMessage
            {
                ChatId      = request.ChatId,
                Text        = content,
                SenderRole  = Role,
                UseMarkdown = true
            }, ct);

            return AgentResponse.Success(request.TaskId, Role, content, tokens, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "{Role} agent xatosi. TaskId={TaskId}", Role, request.TaskId);

            await Sender.SendTextAsync(
                request.ChatId,
                $"❌ *\\[{RoleLabel}\\]* xato: {EscapeMarkdown(ex.Message)}",
                useMarkdown: true,
                ct);

            return AgentResponse.Failure(request.TaskId, Role, ex.Message, sw.Elapsed);
        }
    }

    /// <summary>
    /// Foydalanuvchi xabarini quradi. Subklass override qilishi mumkin.
    /// </summary>
    protected virtual string BuildUserMessage(AgentRequest request)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(request.Prompt);

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            sb.AppendLine();
            sb.AppendLine("## Kontekst");
            sb.AppendLine(request.Context);
        }

        return sb.ToString();
    }

    private string RoleLabel => Role switch
    {
        AgentRole.Developer  => "Developer",
        AgentRole.QA         => "QA",
        AgentRole.Reviewer   => "Reviewer",
        AgentRole.Orchestrator => "Orchestrator",
        _                    => Role.ToString()
    };

    private static string EscapeMarkdown(string text) =>
        text.Replace(".", "\\.").Replace("!", "\\!").Replace("-", "\\-");
}
