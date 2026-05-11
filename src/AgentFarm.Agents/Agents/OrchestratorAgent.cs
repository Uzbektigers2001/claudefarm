using System.Diagnostics;
using System.Text.Json;
using AgentFarm.Agents.Base;
using AgentFarm.Agents.Services;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Enums;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Agents;

public sealed class OrchestratorAgent : AgentBase
{
    public OrchestratorAgent(
        ClaudeApiClient            apiClient,
        ITelegramMessageSender     sender,
        ILogger<OrchestratorAgent> logger)
        : base(apiClient, sender, logger) { }

    public override AgentRole Role => AgentRole.Orchestrator;

    // Orchestrator faqat JSON qaytaradi — 400 token yetarli
    protected override int? MaxTokensOverride => 400;

    // Orchestrator xabarsiz ishlaydi (Telegram ga yuborilmaydi)
    public override async Task<AgentResponse> RunAsync(AgentRequest request, string? previousContext = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var userMessage = BuildUserMessage(request, previousContext);
            var (content, tokens) = await ApiClient.CompleteAsync(SystemPrompt, userMessage, MaxTokensOverride, ct);
            sw.Stop();

            // Orchestrator natijasi foydalanuvchiga ko'rsatilmaydi (Telegram xabar YO'Q)
            return AgentResponse.Success(request.TaskId, Role, content, tokens, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "{Role} agent xatosi. TaskId={TaskId}", Role, request.TaskId);
            return AgentResponse.Failure(request.TaskId, Role, ex.Message, sw.Elapsed);
        }
    }

    private const string DecideSystemPrompt = """
        Sen 15+ yillik CTO siz.
        Tugagan agent natijasini tahlil qilib qaror qabul qil.
        FAQAT JSON qaytart, boshqa hech narsa yo'q:
        {
          "decision": "continue",
          "targetRole": "Backend",
          "reason": "...",
          "instructions": "..."
        }

        QAROR QOIDALARI:
        continue       — javob to'liq va sifatli (100+ belgi, .NET kod bor)
        retry_current  — javob bo'sh, .NET emas, yoki sifatsiz
        retry_previous — arxitektura yoki talab noto'g'ri chiqdi
        escalate       — 3+ marta retry dan keyin hal bo'lmadi

        targetRole: qaysi agent keyingi ishlaydi
        """;

    public async Task<OrchestratorDecision> DecideAsync(
        AgentRole completedRole,
        string agentOutput,
        ProjectSession session,
        CancellationToken ct = default)
    {
        var userMessage =
            $"Tugagan agent: {completedRole}\n" +
            $"Agent natijasi (birinchi 500 belgi):\n" +
            $"{agentOutput[..Math.Min(500, agentOutput.Length)]}\n\n" +
            $"Session holati:\n" +
            $"- Iteratsiyalar: {session.CurrentIteration}\n" +
            $"- Yozilgan fayllar: {session.Files.Count} ta\n" +
            $"- Oxirgi build: {(session.BuildErrors.Any() ? "XATO" : "OK")}";

        try
        {
            var (json, _) = await ApiClient.CompleteAsync(DecideSystemPrompt, userMessage, 300, ct);
            return ParseDecision(json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "DecideAsync xatosi, default Continue qaytarilmoqda");
            return new OrchestratorDecision { Decision = "continue", Reason = "parse xatosi" };
        }
    }

    private static OrchestratorDecision ParseDecision(string raw)
    {
        try
        {
            var cleaned = raw.Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```json\s*", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```\s*", "");
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            var result = JsonSerializer.Deserialize<OrchestratorDecision>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? new OrchestratorDecision { Decision = "continue", Reason = "null result" };
        }
        catch
        {
            return new OrchestratorDecision { Decision = "continue", Reason = "parse xatosi" };
        }
    }

    protected override string SystemPrompt => """
        Sen 15+ yillik CTO siz. Vazifani tahlil qilib eng optimal agentlar va ularning sonini aniqla.

        BACKEND QOIDALARI (.NET/C#):
        Har Backend agent bitta mustaqil qatlamga mas'ul:

        AJRATISH MEZONI:
        - 1 ta endpoint guruhi = 1 ta Backend agent (masalan: /api/auth → 1 agent, /api/products → 1 agent)
        - Shared infratuzilma (DbContext, base classes) = alohida 1 agent
        - Har modul o'z Controller + Service + Repository ga ega

        BACKEND SONI QOIDASI:
        - 1 modul (faqat auth) → 1 Backend
        - 2-3 bog'liq modul → 2 Backend (1: Domain/Entities/DbContext, 2: Controllers/Services)
        - 4+ modul → 3 Backend (maksimal) (1: Infrastructure, 2: Core modules, 3: Additional modules)
        - Hech qachon 3 dan oshmasin

        HAR BACKEND AGENT DESCRIPTION FORMATI:
        "[texnologiya stack]: [aniq fayl/sinf nomlari] - [nima qiladi]"
        Misol: "ASP.NET Core: AuthController, AuthService, LoginDto, RegisterDto - JWT bilan login/register endpoint lar"
        Misol: "EF Core: AppDbContext, User entity, migration - database setup va User jadvali"

        TEXNOLOGIYA STACK (.NET/C#):
        ASP.NET Core Web API, EF Core, JWT Bearer, FluentValidation, Serilog, PostgreSQL

        BOSHQA ROLLAR:
        - Frontend: UI bo'lsa kerak, yo'q bo'lsa olma
        - DevOps: deploy/docker/CI-CD so'ralsa kerak, aks holda olma
        - DatabaseAdmin: schema/migration so'ralsa kerak
        - BusinessAnalyst: murakkab biznes logika bo'lsa kerak
        - Security: auth/payment/sensitive data bo'lsa kerak
        - QA: HAR DOIM 1 ta
        - Reviewer: HAR DOIM 1 ta, oxirgi

        SUBTASK YOZISH QOIDALARI:
        - Har subtask aniq va mustaqil bo'lsin
        - Boshqa agent ishiga bog'liq bo'lmasin
        - Description formati to'g'ri bo'lsin

        FAQAT JSON. Hech qanday matn, markdown, kod bloki yo'q. Aynan:
        {"subtasks":[
          {"id":1,"description":"ASP.NET Core: AuthController, AuthService - JWT login/register","role":"Backend","instance":1},
          {"id":2,"description":"Testlar yoz","role":"QA","instance":1},
          {"id":3,"description":"Kodni review qil","role":"Reviewer","instance":1}
        ]}
        """;
}
