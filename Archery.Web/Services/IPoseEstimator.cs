using Archery.Core.Landmarks;

namespace Archery.Web.Services;

/// <summary>
/// Videodan poz (iskelet) tahmini yapan servisin arayüzü.
/// </summary>
public interface IPoseEstimator
{
    /// <summary>
    /// Video dosyasından kareleri, landmark noktalarını ve gerçek FPS değerini çıkarır.
    /// </summary>
    /// <param name="videoPath">Videonun fiziksel yolu.</param>
    /// <returns>Landmark kareleri listesi ve videonun gerçek FPS değeri.</returns>
    Task<(List<LandmarkFrame> Frames, double Fps)> ExtractFramesAsync(string videoPath);
}
