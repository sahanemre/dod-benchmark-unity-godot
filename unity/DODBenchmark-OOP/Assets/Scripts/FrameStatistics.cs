using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frame time orneklerini toplar ve istatistik hesaplar
/// (ortalama, min, max, standart sapma, FPS).
/// Tek sorumluluk: olcum verisi biriktirme ve ozetleme.
/// </summary>
public class FrameStatistics
{
    private readonly List<float> frameTimes = new List<float>();

    public int SampleCount => frameTimes.Count;

    public void Reset() => frameTimes.Clear();

    public void AddSample(float frameTimeMs) => frameTimes.Add(frameTimeMs);

    public BenchmarkResult Compute(int entityCount, float memoryMB, float duration)
    {
        float sum = 0f, min = float.MaxValue, max = float.MinValue;
        foreach (float ft in frameTimes)
        {
            sum += ft;
            if (ft < min) min = ft;
            if (ft > max) max = ft;
        }
        float avg = sum / frameTimes.Count;

        float variance = 0f;
        foreach (float ft in frameTimes)
        {
            float diff = ft - avg;
            variance += diff * diff;
        }
        float stdDev = Mathf.Sqrt(variance / frameTimes.Count);

        return new BenchmarkResult
        {
            EntityCount = entityCount,
            AvgFrameTime = avg,
            MinFrameTime = min,
            MaxFrameTime = max,
            StdDev = stdDev,
            AvgFPS = 1000f / avg,
            MinFPS = 1000f / max,
            MemoryMB = memoryMB,
            TotalFrames = frameTimes.Count,
            Duration = duration
        };
    }
}
