using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Benchmark sonuclarini CSV formatina cevirir ve dosyaya yazar.
/// Tek sorumluluk: serilestirme + dosya IO.
/// InvariantCulture kullanir; aksi halde Turkce locale'de ondalik
/// ayirici virgul cikar ve CSV sutunlari bozulur.
/// </summary>
public class CsvExporter
{
    private const string Header =
        "EntityCount,AvgFrameTime_ms,MinFrameTime_ms,MaxFrameTime_ms,StdDev_ms," +
        "AvgFPS,MinFPS,MemoryUsed_MB,TotalFrames,Duration_s";

    private readonly StringBuilder sb = new StringBuilder();

    public CsvExporter() => Reset();

    public void Reset()
    {
        sb.Clear();
        sb.AppendLine(Header);
    }

    public void AddRow(BenchmarkResult r)
    {
        var ci = CultureInfo.InvariantCulture;
        sb.AppendLine(string.Format(ci,
            "{0},{1:F3},{2:F3},{3:F3},{4:F3},{5:F1},{6:F1},{7:F2},{8},{9:F0}",
            r.EntityCount, r.AvgFrameTime, r.MinFrameTime, r.MaxFrameTime, r.StdDev,
            r.AvgFPS, r.MinFPS, r.MemoryMB, r.TotalFrames, r.Duration));
    }

    /// <summary>
    /// CSV'yi results/benchmarks/ klasorune kaydeder, dosya adini dondurur.
    /// </summary>
    public string Save(string approachTag)
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"benchmark_{approachTag}_{timestamp}.csv";
        string path = Path.Combine(Application.dataPath, "..", "..", "..", "results", "benchmarks", filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Benchmark] CSV kaydedildi: {path}");
        return filename;
    }
}
