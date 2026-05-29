/// <summary>
/// Tek bir test kosusunun sonucu. Sadece veri tasir, davranis yok.
/// </summary>
public struct BenchmarkResult
{
    public int EntityCount;
    public float AvgFrameTime;
    public float MinFrameTime;
    public float MaxFrameTime;
    public float StdDev;
    public float AvgFPS;
    public float MinFPS;
    public float MemoryMB;
    public int TotalFrames;
    public float Duration;
}
