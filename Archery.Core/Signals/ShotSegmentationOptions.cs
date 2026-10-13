namespace Archery.Core.Signals;

/// <summary>
/// Atış segmentasyon algoritmasının parametrik eşikleri.
/// Sabit kodlama yerine bu değerler üzerinden ayarlanır.
/// </summary>
public class ShotSegmentationOptions
{
    /// <summary>
    /// Minimum çekiş süresi (saniye). Bu süreden kısa "atışlar" yanlış pozitif sayılıp elenir.
    /// Varsayılan: 0.4 saniye.
    /// </summary>
    public double MinDrawDurationSec { get; init; } = 0.4;

    /// <summary>
    /// Minimum toplam açı değişimi (derece). Çekiş fazında açının en az bu kadar değişmesi gerekir.
    /// Küçük dalgalanmalar (gürültü) atış olarak sayılmaz.
    /// Varsayılan: 40 derece.
    /// </summary>
    public double MinAngleChangeDegrees { get; init; } = 40.0;

    /// <summary>
    /// Çekme kolunun "açık" kabul edileceği minimum açı (derece).
    /// Atışın başlangıcı, açının bu değerden yüksek olduğu nokta olarak tanımlanır.
    /// Varsayılan: 120 derece.
    /// </summary>
    public double DrawOpenAngleThreshold { get; init; } = 120.0;

    /// <summary>
    /// Anchor (tam çekiş) noktasının "kapalı" kabul edileceği maksimum açı (derece).
    /// Atışın anchor'ı, açının bu değerin altına indiği nokta olarak tanımlanır.
    /// Varsayılan: 70 derece.
    /// </summary>
    public double AnchorClosedAngleThreshold { get; init; } = 70.0;

    /// <summary>
    /// Anchor platosu minimum süresi (saniye). Minimum noktaya ulaştıktan sonra açının
    /// kısa bir süre o civarda kalması gerekir. Bu, gerçek "nişan alma" anını gürültüden ayırır.
    /// Varsayılan: 0.15 saniye.
    /// </summary>
    public double AnchorPlateauMinSec { get; init; } = 0.15;

    /// <summary>
    /// Anchor platosu toleransı (derece). Minimum noktaya ulaştıktan sonra açının
    /// bu tolerans içinde kalması gerekir.
    /// Varsayılan: 15 derece.
    /// </summary>
    public double AnchorPlateauToleranceDeg { get; init; } = 15.0;

    /// <summary>
    /// Tam çekişte bekleme (anchor → bırakma) için minimum süre (saniye).
    /// Ok takma, yayı kaldırma veya kadraja girerken dirsek kısa süreliğine kapanır ama el yüzde beklemez;
    /// bu hareketler atış sayılmamalıdır. Örnek videolarda sahte atışların beklemesi ≤ 0.9 sn,
    /// gerçek atışlarınki ≥ 1.8 sn ölçülmüştür. Bu bir algılama eşiğidir, okçuluk için hedef değer değildir.
    /// Varsayılan: 1.0 saniye.
    /// </summary>
    public double MinHoldDurationSec { get; init; } = 1.0;

    /// <summary>
    /// Beklemenin (tam çekiş → bırakma) en az bu kadarı gerçek ölçümle (poz bulunan karelerle) kapsanmalıdır (saniye).
    /// Okçu kadrajdan çıkınca oluşan uzun ölçümsüz boşluklar bekleme sayılmaz. Çekme eli yüz önünde kaldığı için
    /// gerçek atışlarda da beklemenin bir kısmı ölçülemeyebilir; bu yüzden eşik düşük tutulur.
    /// Varsayılan: 0.5 saniye.
    /// </summary>
    public double MinMeasuredHoldSec { get; init; } = 0.5;

    /// <summary>
    /// Bırakma sonrası toparlanma minimum açı değişimi (derece).
    /// Anchor'dan sonra açının belirgin şekilde tekrar açılması gerekir.
    /// Varsayılan: 30 derece.
    /// </summary>
    public double RecoveryMinAngleChangeDeg { get; init; } = 30.0;

    /// <summary>
    /// Bırakma sonrası açılmanın kalıcı sayılması için süre (saniye). Açı bu süre boyunca
    /// toparlanma eşiğinin üstünde kalmalıdır; tek karelik poz sıçramaları bırakma sayılmaz.
    /// Varsayılan: 0.25 saniye.
    /// </summary>
    public double RecoveryConfirmSec { get; init; } = 0.25;

    /// <summary>
    /// Gerçek bir tam çekişte (full draw) dirsek açısı çok daralmalıdır. 
    /// Anchor'daki minimum açı bu değerden büyükse (örn. 65°), yarım çekiş sayılır ve reddedilir.
    /// </summary>
    public double MaxAnchorAngleDegrees { get; init; } = 65.0;

    /// <summary>
    /// Atışın geçerli olması için ok bırakma hızının yeterince yüksek olması (süresinin kısa olması) gerekir.
    /// Anchor'dan çıkıp +30 dereceye ulaşma süresi bu değerden uzunsa bu bir atış değil, yayı indirme (let-down) hareketidir.
    /// Varsayılan: 0.5 saniye (Çok yavaş bırakmalar reddedilir).
    /// </summary>
    public double MaxReleaseDurationSec { get; init; } = 0.8;

    /// <summary>
    /// İki ardışık atış arasında minimum süre (saniye).
    /// Art arda gürültüden çift sayım olmasını engeller.
    /// Varsayılan: 1.5 saniye.
    /// </summary>
    public double RefractoryPeriodSec { get; init; } = 1.5;

    /// <summary>
    /// Savitzky-Golay filtresi pencere boyutu (örnek sayısı, tek sayı).
    /// Varsayılan: 7 örnek.
    /// </summary>
    public int SmoothingWindowSamples { get; init; } = 7;

    /// <summary>
    /// Savitzky-Golay filtresi polinom derecesi.
    /// Varsayılan: 2.
    /// </summary>
    public int SmoothingPolynomialOrder { get; init; } = 2;

    /// <summary>
    /// Tam çekiş (anchor) bekleme bölgesi: açının tepe değerinden minimuma düşüşünün son bu kadarlık kısmı.
    /// Geniş tutulur; bırakma öncesi kısa oynamalar ve beklemedeki kayma bu bandın içinde kalır.
    /// Varsayılan: 0.4 (%40).
    /// </summary>
    public double AnchorLevelFraction { get; init; } = 0.4;

    /// <summary>
    /// Tam çekiş bandından bu süreden kısa taşmalar poz gürültüsü sayılır ve bekleme kesilmez (saniye).
    /// Varsayılan: 0.25.
    /// </summary>
    public double AnchorMaxExcursionSec { get; init; } = 0.25;

    /// <summary>
    /// Çekme elinin yüze varışı: dirsek açısının kapanma hızı bu değerin altına indiğinde el durmuş sayılır
    /// (derece/saniye). Varsayılan: 20.
    /// </summary>
    public double AnchorArrivalSpeedDegPerSec { get; init; } = 20.0;

    /// <summary>
    /// Kapanma hızı bu süre aralığındaki açı değişiminden hesaplanır (saniye). Varsayılan: 0.1.
    /// </summary>
    public double AnchorArrivalWindowSec { get; init; } = 0.1;

    /// <summary>
    /// Yayı indirme (let-down) tespiti: bırakma anından önceki <see cref="BowLetDownWindowSec"/> içinde yay kolu (BowArm)
    /// dirsek açısı bu değerin altına inerse kişi atıştan hemen sonra yayı indirmeye başlamıştır (derece). Varsayılan: 140.
    /// </summary>
    public double BowLetDownAngleDeg { get; init; } = 140.0;

    /// <summary>
    /// Bekleme (tam çekiş → bırakma) boyunca yay kolu dirsek açısının ortalaması bu değerin altındaysa
    /// kol gergin değildir; hareket atış sayılmaz (ör. yayı yere indirirken kolun bükülmesi) (derece). Varsayılan: 140.
    /// </summary>
    public double MinBowArmHoldAngleDeg { get; init; } = 140.0;

    /// <summary>
    /// Yayı indirme tespiti için bırakma anından geriye bakılan pencere (saniye). Varsayılan: 0.5.
    /// </summary>
    public double BowLetDownWindowSec { get; init; } = 0.5;

    /// <summary>
    /// Yayı indirilen atışta bırakma, çekme kolunun bekleme platosunun altına bu kadar derece indiği ana çekilir (derece).
    /// Varsayılan: 20.
    /// </summary>
    public double ReleaseCollapseDeg { get; init; } = 20.0;

    /// <summary>
    /// Bırakma anı en az bu kadar öne çekilebiliyorsa uygulanır (saniye). Varsayılan: 0.3.
    /// </summary>
    public double ReleaseCollapseMinAdvanceSec { get; init; } = 0.3;
}
