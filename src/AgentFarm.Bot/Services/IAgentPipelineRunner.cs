using AgentFarm.Core.Models;

namespace AgentFarm.Bot.Services;

/// <summary>
/// Agent pipeline ni ishga tushirish uchun abstraktsiya.
/// AgentFarm.Bot → AgentFarm.Agents bog'liqligini oldini oladi.
/// </summary>
public interface IAgentPipelineRunner
{
    Task<PipelineResult> RunAsync(AgentRequest request, CancellationToken ct = default);
}
