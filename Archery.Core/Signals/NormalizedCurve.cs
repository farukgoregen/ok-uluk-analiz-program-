namespace Archery.Core.Signals;

/// <summary>
/// Zaman ekseni normalleştirilmiş (0-1 aralığına çekilmiş) ve sabit sayıda (örn. 50)
/// örnekten oluşan açı eğrisi. 
/// Bu format sayesinde farklı sürelerdeki atışlar birbirleriyle karşılaştırılabilir.
/// </summary>
/// <param name="Samples">Eğriyi oluşturan değerler.</param>
public record NormalizedCurve(IReadOnlyList<double> Samples);
