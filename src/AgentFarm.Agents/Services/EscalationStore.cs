using System.Collections.Concurrent;
using AgentFarm.Bot.Interfaces;
using AgentFarm.Core.Models;

namespace AgentFarm.Agents.Services;

public sealed class EscalationStore : IEscalationStore
{
    private readonly ConcurrentDictionary<long, EscalationSession> _sessions = new();

    public void AddEscalation(EscalationSession session) =>
        _sessions[session.ChatId] = session;

    public EscalationSession? GetEscalation(long chatId) =>
        _sessions.GetValueOrDefault(chatId);

    public void RemoveEscalation(long chatId) =>
        _sessions.TryRemove(chatId, out _);

    public bool HasPendingEscalation(long chatId) =>
        _sessions.ContainsKey(chatId);
}
