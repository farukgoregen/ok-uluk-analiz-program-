namespace Archery.Web.Models;

/// <summary>
/// Analiz oturumunun durumunu belirtir.
/// </summary>
public enum SessionStatus
{
    /// <summary>Oturum oluşturuldu, henüz video yüklenmedi.</summary>
    Created,

    /// <summary>Video yüklendi, analiz bekliyor.</summary>
    VideoUploaded,

    /// <summary>Poz analizi devam ediyor.</summary>
    Analyzing,

    /// <summary>Analiz tamamlandı, sonuçlar hazır.</summary>
    Completed,

    /// <summary>Analiz sırasında hata oluştu.</summary>
    Failed
}

/// <summary>
/// Bir okçuluk analiz oturumunu temsil eder.
/// Kullanıcı video yükler, sistem analiz eder, sonuçlar bu oturum altında saklanır.
/// </summary>
public class ArcherySession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? VideoFileName { get; set; }


    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public SessionStatus Status { get; set; } = SessionStatus.Created;
}
