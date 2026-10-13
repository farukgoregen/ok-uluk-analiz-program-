using Archery.Web.Models;

namespace Archery.Web.Services;

/// <summary>
/// Analiz oturumlarını saklama arayüzü.
/// Implementasyon değiştirilebilir (in-memory, veritabanı vb.).
/// </summary>
public interface ISessionStore
{
    /// <summary>Yeni bir oturum oluşturur ve depoya ekler.</summary>
    Task<ArcherySession> CreateAsync();

    /// <summary>Belirtilen ID'ye sahip oturumu getirir. Bulunamazsa <c>null</c>.</summary>
    Task<ArcherySession?> GetAsync(Guid id);

    /// <summary>Tüm oturumları listeler.</summary>
    Task<IReadOnlyList<ArcherySession>> GetAllAsync();

    /// <summary>Mevcut bir oturumu günceller.</summary>
    Task UpdateAsync(ArcherySession session);

    /// <summary>Oturum için hesaplanan analiz sonuçlarını kaydeder.</summary>
    Task SaveAnalysisAsync(SessionAnalysis analysis);

    /// <summary>Belirtilen oturumun analiz sonuçlarını getirir.</summary>
    Task<SessionAnalysis?> GetAnalysisAsync(Guid sessionId);
}
