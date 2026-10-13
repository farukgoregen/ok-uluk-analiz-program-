namespace Archery.Core.Signals;

/// <summary>
/// Zaman damgalı bir ölçüm noktası (açı serisi elemanı).
/// <see cref="Value"/> null ise o anda ölçüm alınamadı demektir
/// (eksik veya güvensiz landmark).
/// </summary>
/// <param name="Timestamp">Video zaman damgası (PTS).</param>
/// <param name="Value">Ölçülen değer (derece) veya null.</param>
public record TimedSample(TimeSpan Timestamp, double? Value);
