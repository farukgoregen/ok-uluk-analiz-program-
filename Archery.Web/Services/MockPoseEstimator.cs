using Archery.Core.Geometry;
using Archery.Core.Landmarks;

namespace Archery.Web.Services;

/// <summary>
/// UI ve veri akışını test etmek için sahte (mock) veri üreten poz tahmin motoru.
/// Gerçek video analizi yapmaz; 30 saniyelik, 3 atış içeren simüle bir hareket üretir.
///
/// Kinematik model:
/// - DrawArm dirsek açısı: ~160° (açık) → ~35° (tam çekiş/anchor) → ~160° (bırakma) şeklinde her atışta tekrar eder.
/// - BowArm dirsek açısı: ~160-170° aralığında sabit (kol gergin, yay tutulmuş).
/// </summary>
public class MockPoseEstimator : IPoseEstimator
{
    public async Task<(List<LandmarkFrame> Frames, double Fps)> ExtractFramesAsync(string videoPath)
    {
        await Task.Delay(1500);

        var frames = new List<LandmarkFrame>();
        double fps = 30.0;
        double durationSec = 30.0; // 3 atış için yeterli süre
        int totalFrames = (int)(fps * durationSec);

        // BowArm anatomisi: omuz sabit, dirsek ve bilek sabit bir düz kol pozisyonunda
        var bowShoulder = new Point2D(300, 200);
        var bowElbow    = new Point2D(450, 205);
        var bowWrist    = new Point2D(600, 210);

        // DrawArm anatomisi: omuz sabit, dirsek hareket ediyor
        var drawShoulder = new Point2D(300, 200);
        // DrawArm dirsek ve bileği açı değerine göre hesaplanacak

        for (int i = 0; i < totalFrames; i++)
        {
            double t = i / fps;

            // BowArm açısı: sabit ~163° (gergin yay kolu), hafif gürültüyle
            double bowAngleDeg = 163.0 + Math.Sin(t * 0.5) * 1.5;

            // DrawArm açısı: her ~9 saniyede bir atış döngüsü (3 atış / 30 saniye)
            // Atış döngüsü: 0-2s bekleme (160°), 2-4s çekiş (160°→35°), 4-5s anchor (35°), 5-5.3s bırakma (35°→160°), 5.3-9s bekleme
            double cycleSec = 9.0;
            double cycleT = t % cycleSec;

            double drawAngleDeg;
            if (cycleT < 2.0)
            {
                // Bekleme: açık kol
                drawAngleDeg = 160.0 + Math.Sin(cycleT * 2.0) * 2.0;
            }
            else if (cycleT < 4.0)
            {
                // Çekiş fazı: 160° → 35°
                double progress = (cycleT - 2.0) / 2.0;
                // Yumuşak geçiş için sinüs eğrisi (smoothstep)
                double ease = progress * progress * (3 - 2 * progress);
                drawAngleDeg = 160.0 - ease * 125.0;
            }
            else if (cycleT < 5.0)
            {
                // Anchor platosu: ~35° civarında tutulmuş
                drawAngleDeg = 35.0 + Math.Sin(cycleT * 8.0) * 2.0;
            }
            else if (cycleT < 5.35)
            {
                // Bırakma: 35° → 160° (hızlı)
                double progress = (cycleT - 5.0) / 0.35;
                double ease = progress * progress * (3 - 2 * progress);
                drawAngleDeg = 35.0 + ease * 125.0;
            }
            else
            {
                // Toparlanma: 160° civarında bekleme
                drawAngleDeg = 160.0 + Math.Sin(cycleT * 1.5) * 2.5;
            }

            // BowArm keypoint'lerini hesapla (omuz-dirsek-bilek düz çizgide, sabit)
            var bowElbowPos = ComputeElbow(bowShoulder, bowAngleDeg, armLength: 150);
            var bowWristPos = ComputeWrist(bowShoulder, bowElbowPos, armLength: 150);

            // DrawArm keypoint'lerini hesapla (açıya göre dirsek ve bilek pozisyonu)
            var drawElbowPos = ComputeElbow(drawShoulder, drawAngleDeg, armLength: 150);
            var drawWristPos = ComputeWrist(drawShoulder, drawElbowPos, armLength: 150);

            var points = new Dictionary<string, LandmarkPoint>
            {
                ["BowShoulder"]  = new LandmarkPoint(bowShoulder,  0.95),
                ["BowElbow"]     = new LandmarkPoint(bowElbowPos,  0.90),
                ["BowWrist"]     = new LandmarkPoint(bowWristPos,  0.88),
                ["DrawShoulder"] = new LandmarkPoint(drawShoulder, 0.95),
                ["DrawElbow"]    = new LandmarkPoint(drawElbowPos, 0.90),
                ["DrawWrist"]    = new LandmarkPoint(drawWristPos, 0.88),
            };

            frames.Add(new LandmarkFrame(TimeSpan.FromSeconds(t), points));
        }

        return (frames, fps);
    }

    /// <summary>
    /// Omuzdan belirli açıda ve uzunlukta dirsek pozisyonunu hesaplar.
    /// </summary>
    private static Point2D ComputeElbow(Point2D shoulder, double angleDeg, double armLength)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return new Point2D(
            shoulder.X + Math.Cos(rad) * armLength,
            shoulder.Y + Math.Sin(rad) * armLength
        );
    }

    /// <summary>
    /// Dirsekten devam eden kol uzantısı ile bilek pozisyonunu hesaplar.
    /// </summary>
    private static Point2D ComputeWrist(Point2D shoulder, Point2D elbow, double armLength)
    {
        double dx = elbow.X - shoulder.X;
        double dy = elbow.Y - shoulder.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return elbow;

        return new Point2D(
            elbow.X + (dx / len) * armLength,
            elbow.Y + (dy / len) * armLength
        );
    }
}
