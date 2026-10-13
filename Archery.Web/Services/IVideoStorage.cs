namespace Archery.Web.Services;

/// <summary>
/// Video dosyası depolama arayüzü.
/// Implementasyon değiştirilebilir (yerel dosya sistemi, blob storage vb.).
/// </summary>
public interface IVideoStorage
{
    /// <summary>
    /// Video dosyasını kaydeder.
    /// </summary>
    /// <param name="sessionId">İlişkili oturum ID'si.</param>
    /// <param name="videoStream">Video verisi.</param>
    /// <param name="fileName">Orijinal dosya adı.</param>
    /// <returns>Kaydedilen dosyanın yolu.</returns>
    Task<string> SaveAsync(Guid sessionId, Stream videoStream, string fileName);

    /// <summary>
    /// Belirtilen oturumun videosunu getirir.
    /// </summary>
    /// <param name="sessionId">Oturum ID'si.</param>
    /// <returns>Video akışı veya bulunamazsa <c>null</c>.</returns>
    Task<Stream?> GetAsync(Guid sessionId);

    /// <summary>
    /// Videonun disk üzerindeki fiziksel yolunu döndürür.
    /// (Python MediaPipe entegrasyonu gibi harici süreçler için gereklidir).
    /// </summary>
    Task<string?> GetFilePathAsync(Guid sessionId);
}
