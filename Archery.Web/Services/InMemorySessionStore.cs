using System.Collections.Concurrent;
using Archery.Web.Models;

namespace Archery.Web.Services;

/// <summary>
/// Oturumları bellekte (RAM) saklayan implementasyon.
/// Uygulama kapandığında veriler kaybolur.
/// Geliştirme ve prototipleme amaçlıdır; üretimde veritabanı ile değiştirilecek.
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, ArcherySession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, SessionAnalysis> _analyses = new();

    public Task<ArcherySession> CreateAsync()
    {
        var session = new ArcherySession();
        _sessions[session.Id] = session;
        return Task.FromResult(session);
    }

    public Task<ArcherySession?> GetAsync(Guid id)
    {
        _sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ArcherySession>> GetAllAsync()
    {
        IReadOnlyList<ArcherySession> list = _sessions.Values.ToList().AsReadOnly();
        return Task.FromResult(list);
    }

    public Task UpdateAsync(ArcherySession session)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task SaveAnalysisAsync(SessionAnalysis analysis)
    {
        _analyses[analysis.SessionId] = analysis;
        return Task.CompletedTask;
    }

    public Task<SessionAnalysis?> GetAnalysisAsync(Guid sessionId)
    {
        _analyses.TryGetValue(sessionId, out var analysis);
        return Task.FromResult(analysis);
    }
}
