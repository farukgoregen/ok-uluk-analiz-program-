using System.Text.Json.Serialization;
using Archery.Core.Geometry;

namespace Archery.Web.Models;

/// <summary>
/// Tüm oturumun analiz sonuçlarını tutan DTO.
/// </summary>
public record SessionAnalysis(
    Guid SessionId, 
    double Fps, 
    List<FrameData> Frames,
    ShotHistoryDto? History = null);

/// <summary>
/// Tek bir karenin (frame) poz ve açı verileri.
/// </summary>
public record FrameData(
    double TimestampSec, 
    Dictionary<string, Point2D> Keypoints, 
    Dictionary<string, double?> Angles);
