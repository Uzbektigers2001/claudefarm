using System.Collections.Concurrent;
using AgentFarm.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentFarm.Agents.Services;

/// <summary>
/// In-memory session store for managing ProjectSessions.
/// Singleton sifatida DI ga register qilinadi.
/// </summary>
public sealed class InMemorySessionStore
{
    private readonly ConcurrentDictionary<Guid, ProjectSession> _sessions = new();
    private readonly ILogger<InMemorySessionStore> _logger;

    public InMemorySessionStore(ILogger<InMemorySessionStore> logger)
    {
        _logger = logger;
    }

    public ProjectSession CreateSession(string originalTask, long chatId)
    {
        var session = new ProjectSession
        {
            OriginalTask = originalTask,
            ChatId = chatId
        };

        if (_sessions.TryAdd(session.SessionId, session))
        {
            _logger.LogInformation("Session yaratildi: {SessionId}", session.SessionId);
            return session;
        }

        throw new InvalidOperationException($"Session {session.SessionId} allaqachon mavjud");
    }

    public ProjectSession? GetSession(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void UpdateSession(ProjectSession session)
    {
        if (_sessions.TryGetValue(session.SessionId, out _))
        {
            _sessions[session.SessionId] = session;
            _logger.LogDebug("Session yangilandi: {SessionId}, Status={Status}",
                session.SessionId, session.Status);
        }
        else
        {
            _logger.LogWarning("Session topilmadi: {SessionId}", session.SessionId);
        }
    }

    public void DeleteSession(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Session o'chirildi: {SessionId}", sessionId);
        }
    }

    public int Count => _sessions.Count;
}
