using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery.Core.Signals;

public static class ShotSegmenterStateMachine
{
    private enum State
    {
        Idle,
        Drawing,
        Anchored,
        Recovering
    }

    public static IReadOnlyList<ShotSegment> DetectShots(
        IReadOnlyList<TimedSample> angleSeries,
        ShotSegmentationOptions options,
        Action<string>? logger = null)
    {
        // Null değerleri HasValue ile filtrele; ?? 0 kullanmak gerçek 0.0 açıları da eleyebilir
        var validSamples = angleSeries
            .Select((s, i) => (OriginalIndex: i, Timestamp: s.Timestamp, Value: s.Value))
            .Where(s => s.Value.HasValue)
            .Select(s => (s.OriginalIndex, s.Timestamp, Value: s.Value!.Value))
            .ToList();

        if (validSamples.Count < options.SmoothingWindowSamples)
            return Array.Empty<ShotSegment>();

        var timedSamples = validSamples.Select(s => new TimedSample(s.Timestamp, s.Value)).ToList();
        var smoothed = SavitzkyGolayFilter.Apply(
            timedSamples,
            options.SmoothingWindowSamples,
            options.SmoothingPolynomialOrder)
            .Select(s => (Timestamp: s.Timestamp, Value: s.Value!.Value))
            .ToList();

        var shots = new List<ShotSegment>();
        int shotIndex = 0;
        
        State state = State.Idle;
        
        // Dinamik referans (kişiye özgü maksimum açık kol açısı)
        // Başlangıçta yeterli veri yokken en azından 100-120 derece gibi bir alt sınır bekleyelim.
        double sessionMaxAngle = 100.0; 
        
        int peakIdx = 0;
        int minIdx = 0;
        int anchorStartIdx = 0;
        
        for (int i = 1; i < smoothed.Count; i++)
        {
            var current = smoothed[i];
            
            if (current.Value > sessionMaxAngle)
            {
                sessionMaxAngle = current.Value;
            }
            
            switch (state)
            {
                case State.Idle:
                    if (current.Value >= smoothed[peakIdx].Value)
                    {
                        peakIdx = i;
                    }
                    
                    if (smoothed[peakIdx].Value - current.Value > 20.0) 
                    {
                        logger?.Invoke($"[Idle -> Drawing] Peak at {smoothed[peakIdx].Timestamp.TotalSeconds:F2} (angle {smoothed[peakIdx].Value:F1}), dropped to {current.Timestamp.TotalSeconds:F2} (angle {current.Value:F1})");
                        state = State.Drawing;
                        minIdx = i;
                    }
                    break;
                    
                case State.Drawing:
                    if (current.Value > smoothed[peakIdx].Value - 10.0)
                    {
                        logger?.Invoke($"[Drawing -> Idle] Noise detected, went back up to {current.Value:F1} at {current.Timestamp.TotalSeconds:F2}");
                        state = State.Idle;
                        peakIdx = i;
                        break;
                    }
                    
                    if (current.Value <= smoothed[minIdx].Value)
                    {
                        minIdx = i;
                    }
                    else 
                    {
                        double currentDrop = smoothed[peakIdx].Value - smoothed[minIdx].Value;
                        if (currentDrop >= options.MinAngleChangeDegrees)
                        {
                            logger?.Invoke($"[Drawing -> Anchored] Anchor started at {smoothed[minIdx].Timestamp.TotalSeconds:F2} (angle {smoothed[minIdx].Value:F1})");
                            anchorStartIdx = minIdx;
                            state = State.Anchored;
                        }
                        else if (current.Value - smoothed[minIdx].Value > 25.0)
                        {
                            logger?.Invoke($"[Drawing -> Idle] Not enough drop ({currentDrop:F1}), bounce back at {current.Timestamp.TotalSeconds:F2}");
                            state = State.Idle;
                            peakIdx = i;
                        }
                    }
                    break;
                    
                case State.Anchored:
                    if (current.Value < smoothed[minIdx].Value)
                    {
                        minIdx = i;
                        // Gürültü sebebiyle yeni minimum gelirse, anchor başlangıcını sıfırlamak yerine korumak daha güvenlidir.
                        // Eğer gerçekten daha aşağı iniyorsa, anchor süresi asıl inmeye başladığı andan itibaren geçerlidir.
                        // Bu yüzden anchorStartIdx = i YAPMIYORUZ.
                    }
                    
                    if (current.Value - smoothed[minIdx].Value > 25.0)
                    {
                        if (smoothed[minIdx].Value > options.MaxAnchorAngleDegrees)
                        {
                            logger?.Invoke($"[Anchored -> Idle] Shot rejected: Anchor angle {smoothed[minIdx].Value:F1} > {options.MaxAnchorAngleDegrees}");
                            state = State.Idle;
                            peakIdx = i;
                            break;
                        }
                        
                        logger?.Invoke($"[Anchored -> Recovering] Recovery started at {current.Timestamp.TotalSeconds:F2}");
                        state = State.Recovering;
                    }
                    break;
                    
                case State.Recovering:
                    if (current.Value - smoothed[minIdx].Value >= options.RecoveryMinAngleChangeDeg
                        && !RecoveryConfirmed(smoothed, i, smoothed[minIdx].Value + options.RecoveryMinAngleChangeDeg, options.RecoveryConfirmSec))
                    {
                        // Tek karelik poz sıçraması: bırakma değil, beklemeye devam
                        logger?.Invoke($"[Recovering] Spike ignored at {current.Timestamp.TotalSeconds:F2} (angle {current.Value:F1})");
                    }
                    else if (current.Value - smoothed[minIdx].Value >= options.RecoveryMinAngleChangeDeg)
                    {
                        double anchorDuration = (current.Timestamp - smoothed[anchorStartIdx].Timestamp).TotalSeconds;
                        
                        int releaseIdx = anchorStartIdx;
                        for (int k = i; k >= anchorStartIdx; k--)
                        {
                            if (smoothed[k].Value <= smoothed[minIdx].Value + 15.0) // 15 dereceye kadar olan kısmı bekleme say
                            {
                                releaseIdx = k;
                                break;
                            }
                        }
                        TimeSpan releaseStart = smoothed[releaseIdx].Timestamp;
                        double releaseDuration = (current.Timestamp - releaseStart).TotalSeconds;
                        
                        if (anchorDuration < options.AnchorPlateauMinSec)
                        {
                            logger?.Invoke($"[Recovering -> Idle] SHOT REJECTED: Plateau too short ({anchorDuration:F2}s)");
                            state = State.Idle;
                            peakIdx = i;
                            break;
                        }
                        
                        if (releaseDuration > options.MaxReleaseDurationSec)
                        {
                            logger?.Invoke($"[Recovering -> Idle] SHOT REJECTED: Let-down detected (release took {releaseDuration:F2}s > {options.MaxReleaseDurationSec}s)");
                            state = State.Idle;
                            peakIdx = i;
                            break;
                        }
                        
                        // Tam çekiş (anchor) anı: çekme elinin yüze geldiği an.
                        // Çekiş ortasında duraksama olabileceği için bırakmadan geriye doğru aranır.
                        int anchorIdx = FindAnchorBackwards(smoothed, peakIdx, minIdx, releaseIdx, options);

                        // Gerçek bir atışta el yüzde bir süre bekler; bekleme çok kısaysa (ok takma, yayı kaldırma,
                        // kadraja girip çıkarken poz modelinin oynaması) atış sayılmaz.
                        double holdDuration = (releaseStart - smoothed[anchorIdx].Timestamp).TotalSeconds;
                        if (holdDuration < Math.Max(options.AnchorPlateauMinSec, options.MinHoldDurationSec))
                        {
                            logger?.Invoke($"[Recovering -> Idle] SHOT REJECTED: Hold too short ({holdDuration:F2}s)");
                            state = State.Idle;
                            peakIdx = i;
                            break;
                        }

                        // Bırakma anı: sıçrama başlamadan önce açının hâlâ anchor bandında olduğu son nokta.
                        // Atış sonu: açının anchor minimumundan RecoveryMinAngleChangeDeg kadar açıldığı nokta.
                        var segment = new ShotSegment(
                            shotIndex,
                            smoothed[peakIdx].Timestamp,
                            smoothed[anchorIdx].Timestamp,
                            releaseStart,
                            smoothed[i].Timestamp
                        );

                        logger?.Invoke($"[Recovering -> Idle] SHOT COMPLETE: Peak={smoothed[peakIdx].Timestamp.TotalSeconds:F2}, Min={smoothed[minIdx].Timestamp.TotalSeconds:F2}, Release={releaseStart.TotalSeconds:F2}, End={current.Timestamp.TotalSeconds:F2}");
                        shots.Add(segment);
                        shotIndex++;
                        
                        double refEndTime = current.Timestamp.TotalSeconds + options.RefractoryPeriodSec;
                        while (i < smoothed.Count && smoothed[i].Timestamp.TotalSeconds < refEndTime)
                        {
                            i++;
                        }
                        
                        state = State.Idle;
                        if (i < smoothed.Count) peakIdx = i;
                    }
                    else if (current.Value < smoothed[minIdx].Value + 15.0) // 5.0 çok sıkıydı, 15.0'a kadar düşerse gürültüdür.
                    {
                        logger?.Invoke($"[Recovering -> Anchored] RECOVER FAILED: angle dropped back. Time={current.Timestamp.TotalSeconds:F2}");
                        state = State.Anchored;
                    }
                    break;
            }
        }

        return shots;
    }

    /// <summary>
    /// Toparlanmanın kalıcı olup olmadığını denetler: <paramref name="startIdx"/>'ten sonraki
    /// <paramref name="confirmSec"/> içindeki tüm örnekler <paramref name="threshold"/> üstünde kalmalıdır.
    /// Pencerede örnek yoksa (seri sonu veya ölçüm boşluğu) karar verilemez; toparlanma kabul edilir.
    /// </summary>
    private static bool RecoveryConfirmed(
        List<(TimeSpan Timestamp, double Value)> smoothed,
        int startIdx,
        double threshold,
        double confirmSec)
    {
        var limit = smoothed[startIdx].Timestamp + TimeSpan.FromSeconds(confirmSec);
        for (int k = startIdx + 1; k < smoothed.Count && smoothed[k].Timestamp <= limit; k++)
        {
            if (smoothed[k].Value < threshold)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Tam çekiş (anchor) başlangıcını, yani çekme elinin yüze varıp durduğu anı bulur.
    ///
    /// 1. Bekleme bölgesi: Çekiş, açının tepe değerinden (kol düz) minimuma düşüşüdür. Bırakmadan geriye
    ///    doğru, açı bu düşüşün son <see cref="ShotSegmentationOptions.AnchorLevelFraction"/> kısmında
    ///    kaldıkça gidilir. Banttan kısa taşmalar (<see cref="ShotSegmentationOptions.AnchorMaxExcursionSec"/>)
    ///    poz gürültüsü sayılır; daha uzun taşma (çekişin kendisi veya çekiş ortasındaki duraksama) aramayı durdurur.
    /// 2. Varış: Bölgenin başından ileri gidilir; açının kapanma hızı
    ///    <see cref="ShotSegmentationOptions.AnchorArrivalSpeedDegPerSec"/> altına indiği ilk an tam çekiştir.
    /// Hız, zaman damgalarından hesaplanır (sabit FPS varsayılmaz).
    /// </summary>
    private static int FindAnchorBackwards(
        List<(TimeSpan Timestamp, double Value)> smoothed,
        int peakIdx,
        int minIdx,
        int releaseIdx,
        ShotSegmentationOptions options)
    {
        double minValue = smoothed[minIdx].Value;
        double limit = minValue + options.AnchorLevelFraction * (smoothed[peakIdx].Value - minValue);

        // 1. Bekleme bölgesinin başı
        int bandStartIdx = releaseIdx;
        TimeSpan? excursionEnd = null;
        for (int k = releaseIdx - 1; k > peakIdx; k--)
        {
            if (smoothed[k].Value > limit)
            {
                excursionEnd ??= smoothed[k].Timestamp;
                if ((excursionEnd.Value - smoothed[k].Timestamp).TotalSeconds > options.AnchorMaxExcursionSec)
                    break;
            }
            else
            {
                excursionEnd = null;
                bandStartIdx = k;
            }
        }

        // 2. Açı hâlâ hızla kapanıyorsa el yüze henüz varmamıştır; hız düşene kadar ilerle
        for (int k = bandStartIdx; k < releaseIdx; k++)
        {
            int j = k + 1;
            while (j < releaseIdx && (smoothed[j].Timestamp - smoothed[k].Timestamp).TotalSeconds < options.AnchorArrivalWindowSec)
                j++;

            double dt = (smoothed[j].Timestamp - smoothed[k].Timestamp).TotalSeconds;
            if (dt <= 0)
                return k;

            double closingSpeed = (smoothed[k].Value - smoothed[j].Value) / dt;
            if (closingSpeed < options.AnchorArrivalSpeedDegPerSec)
                return k;
        }

        return bandStartIdx;
    }
}
