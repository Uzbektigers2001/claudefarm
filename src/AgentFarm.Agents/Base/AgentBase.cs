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

    private const int TelegramMaxLength = 4000;

    protected AgentBase(ClaudeApiClient apiClient, ITelegramMessageSender sender, ILogger logger)
    {
        ApiClient = apiClient;
        Sender    = sender;
        Logger    = logger;
    }

    public abstract AgentRole Role { get; }
    protected abstract string SystemPrompt { get; }

    public async Task<AgentResponse> RunAsync(AgentRequest request, string? previousContext = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        await Sender.SendTextAsync(request.ChatId, $"⏳ *\\[{RoleLabel}\\]* ishlayapti\\.\\.\\.", useMarkdown: true, ct);

        try
        {
            var userMessage = BuildUserMessage(request, previousContext);
            var (content, tokens) = await ApiClient.CompleteAsync(SystemPrompt, userMessage, ct);
            sw.Stop();

            await SendChunkedAsync(request.ChatId, content, ct);

            return AgentResponse.Success(request.TaskId, Role, content, tokens, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "{Role} agent xatosi. TaskId={TaskId}", Role, request.TaskId);
            await Sender.SendTextAsync(request.ChatId, $"❌ *\\[{RoleLabel}\\]* xato: {EscapeMd(ex.Message)}", useMarkdown: true, ct);
            return AgentResponse.Failure(request.TaskId, Role, ex.Message, sw.Elapsed);
        }
    }

    protected virtual string BuildUserMessage(AgentRequest request, string? previousContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine(request.Prompt);

        if (!string.IsNullOrWhiteSpace(previousContext))
        {
            sb.AppendLine();
            sb.AppendLine("## Oldingi agent natijasi");
            sb.AppendLine(previousContext);
        }

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            sb.AppendLine();
            sb.AppendLine("## Qo'shimcha kontekst");
            sb.AppendLine(request.Context);
        }

        return sb.ToString();
    }

    private async Task SendChunkedAsync(long chatId, string content, CancellationToken ct)
    {
        if (content.Length <= TelegramMaxLength)
        {
            await Sender.SendMessageAsync(new TelegramMessage { ChatId = chatId, Text = content, SenderRole = Role, UseMarkdown = true }, ct);
            return;
        }

        var chunks = SplitIntoChunks(content, TelegramMaxLength);
        for (var i = 0; i < chunks.Count; i++)
        {
            var isFirst = i == 0;
            await Sender.SendMessageAsync(new TelegramMessage
            {
                ChatId      = chatId,
                Text        = isFirst ? chunks[i] : $"({i + 1}/{chunks.Count})\n{chunks[i]}",
                SenderRole  = isFirst ? Role : null,
                UseMarkdown = false
            }, ct);
            if (i < chunks.Count - 1) await Task.Delay(300, ct);
        }
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var len = Math.Min(chunkSize, text.Length - i);
            if (i + len < text.Length)
            {
                var lastNewline = text.LastIndexOf('\n', i + len, len);
                if (lastNewline > i) len = lastNewline - i;
            }
            chunks.Add(text.Substring(i, len));
            i += len;
        }
        return chunks;
    }

    private string RoleLabel => Role switch
    {
        AgentRole.Developer    => "Developer",
        AgentRole.QA           => "QA",
        AgentRole.Reviewer     => "Reviewer",
        AgentRole.Orchestrator => "Orchestrator",
        _                      => Role.ToString()
    };

    public static string EscapeMd(string text) =>
        text.Replace("\\", "\\\\").Replace("_", "\\_").Replace("*", "\\*")
            .Replace("[", "\\[").Replace("]", "\\]").Replace("(", "\\(")
            .Replace(")", "\\)").Replace("~", "\\~").Replace("`", "\\`")
            .Replace(">", "\\>").Replace("#", "\\#").Replace("+", "\\+")
            .Replace("-", "\\-").Replace("=", "\\=").Replace("|", "\\|")
            .Replace("{", "\\{").Replace("}", "\\}").Replace(".", "\\.")
            .Replace("!", "\\!");
}
