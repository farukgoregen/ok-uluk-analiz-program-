using Archery.Core.Angles;
using Archery.Core.Geometry;
using Archery.Core.Landmarks;

namespace Archery.Core.Tests.Angles;

public class FrameAngleComputerTests
{
    private const double Tolerance = 0.0001;

    /// <summary>
    /// Yardımcı: verilen açıyla (derece) bir dirsek konfigürasyonu oluşturur.
    /// Omuz (0,0), dirsek (1,0), bilek'i belirtilen açıya göre yerleştirir.
    /// </summary>
    private static LandmarkFrame CreateElbowFrame(double elbowAngleDegrees, double confidence = 0.9)
    {
        // Omuz (0,0), Dirsek (1,0) sabit.
        // Bilek, dirsekten elbowAngleDegrees açıyla yerleşir.
        double radians = elbowAngleDegrees * (Math.PI / 180.0);
        // Omuz yönü: dirsekten omza (sola) → (-1, 0)
        // Bilek yönü: dirsekten belirtilen açıda
        double wristX = 1.0 + Math.Cos(Math.PI - radians); // dirsek (1,0) merkezli
        double wristY = Math.Sin(Math.PI - radians);

        var points = new Dictionary<string, LandmarkPoint>
        {
            ["BowShoulder"] = new(new Point2D(0, 0), confidence),
            ["BowElbow"] = new(new Point2D(1, 0), confidence),
            ["BowWrist"] = new(new Point2D(wristX, wristY), confidence),
        };

        return new LandmarkFrame(TimeSpan.FromSeconds(1), points);
    }

    [Fact]
    public void AllLandmarksHighConfidence_ReturnsCorrectAngle()
    {
        // 90° dirsek açısı
        var frame = CreateElbowFrame(90);
        var definitions = new[] { WellKnownAngles.BowArmElbow };

        var results = FrameAngleComputer.ComputeAngles(frame, definitions);

        Assert.Single(results);
        Assert.NotNull(results[0].Degrees);
        Assert.Equal(90.0, results[0].Degrees!.Value, Tolerance);
        Assert.Equal(AngleConfidence.High, results[0].Confidence);
    }

    [Fact]
    public void MissingLandmark_ReturnsNullWithInsufficientConfidence()
    {
        // Bir landmark eksik (BowWrist yok)
        var points = new Dictionary<string, LandmarkPoint>
        {
            ["BowShoulder"] = new(new Point2D(0, 0), 0.9),
            ["BowElbow"] = new(new Point2D(1, 0), 0.9),
            // BowWrist eksik
        };
        var frame = new LandmarkFrame(TimeSpan.FromSeconds(1), points);

        var results = FrameAngleComputer.ComputeAngles(frame, new[] { WellKnownAngles.BowArmElbow });

        Assert.Single(results);
        Assert.Null(results[0].Degrees);
        Assert.Equal(AngleConfidence.Insufficient, results[0].Confidence);
    }

    [Fact]
    public void LowConfidenceLandmark_ReturnsNullWithLowConfidence()
    {
        // Bir landmark düşük güvenli
        var points = new Dictionary<string, LandmarkPoint>
        {
            ["BowShoulder"] = new(new Point2D(0, 0), 0.9),
            ["BowElbow"] = new(new Point2D(1, 0), 0.9),
            ["BowWrist"] = new(new Point2D(2, 1), 0.1), // düşük güven
        };
        var frame = new LandmarkFrame(TimeSpan.FromSeconds(1), points);

        var results = FrameAngleComputer.ComputeAngles(frame, new[] { WellKnownAngles.BowArmElbow });

        Assert.Single(results);
        Assert.Null(results[0].Degrees);
        Assert.Equal(AngleConfidence.Low, results[0].Confidence);
    }

    [Fact]
    public void MultipleDefinitions_ReturnsResultForEach()
    {
        // İki tanım, ama sadece BowArm landmark'ları var
        var points = new Dictionary<string, LandmarkPoint>
        {
            ["BowShoulder"] = new(new Point2D(0, 0), 0.9),
            ["BowElbow"] = new(new Point2D(1, 0), 0.9),
            ["BowWrist"] = new(new Point2D(2, 0), 0.9),
        };
        var frame = new LandmarkFrame(TimeSpan.FromSeconds(2.5), points);

        var results = FrameAngleComputer.ComputeAngles(frame, WellKnownAngles.All);

        Assert.Equal(2, results.Count);

        // BowArmElbow — 180° (düz kol)
        Assert.NotNull(results[0].Degrees);
        Assert.Equal(180.0, results[0].Degrees!.Value, Tolerance);

        // DrawArmElbow — landmark'lar eksik
        Assert.Null(results[1].Degrees);
        Assert.Equal(AngleConfidence.Insufficient, results[1].Confidence);
    }

    [Fact]
    public void Timestamp_IsPreservedFromFrame()
    {
        // Zaman damgasının korunduğunu doğrula (frame PTS)
        var ts = TimeSpan.FromMilliseconds(1234.567);
        var frame = new LandmarkFrame(ts, new Dictionary<string, LandmarkPoint>());

        Assert.Equal(ts, frame.Timestamp);
    }
}
