using System.Globalization;
using System.IO;

/// <summary>Bir S1 kosusunun sonuc satiri. Yalnizca veri tasir.</summary>
public class RunRecord
{
    public string Engine;            // unity | godot
    public string Paradigm;          // oop | dod
    public string Scenario = "S1";
    public int N;
    public int RunIndex;
    public float MeanFrameTimeMs;
    public float MeanFps;            // 1000 / MeanFrameTimeMs (yayinlanan AvgFPS ile ayni tanim)
    public long PrivateBytes;        // warmup sonunda process private bytes
    public string WindowMode;        // exclusive | borderless | windowed | ...
    public int RenderWidth;
    public int RenderHeight;
    public string DotsColor;         // on | off | na
    public string Profile;           // published | parity
    public string DotsPipeline;      // original | lean | na
    public bool FocusLost;           // kosu sirasinda pencere odagi kaybedildi mi
    public string GraphicsApi;       // gercekte kullanilan API: d3d11 | d3d12 | ...
    public double? CollisionPairsMean; // yalnizca S2: olcum penceresinde kare basina ortalama kesisen cift
}

/// <summary>
/// Gozetimsiz kosunun sonucunu tek satirlik CSV olarak yazar.
/// Tek sorumluluk: kosu basina sonuc dosyasi.
///
/// Her kosu kendi dosyasina yazilir; bir kosu cokerse yalnizca o dosya
/// eksik kalir. Once .tmp'ye yazilip sonra yeniden adlandirilir, boylece
/// yarim kalmis bir dosya "tamamlanmis" sanilmaz.
/// InvariantCulture: Turkce locale'de ondalik ayirici virgul olmasin.
/// </summary>
public static class RunCsvWriter
{
    public const string Header =
        "engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes," +
        "window_mode,render_width,render_height,dots_color,profile,dots_pipeline,focus_lost,graphics_api";

    /// <summary>S2 ek sutunu; S1 satirlarinda yoktur (S1 ciktisi degismez).</summary>
    public const string S2Column = "collision_pairs_mean";

    public static string HeaderFor(RunRecord r) =>
        r.CollisionPairsMean.HasValue ? Header + "," + S2Column : Header;

    public static string FormatRow(RunRecord r)
    {
        string row = string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5:F4},{6:F2},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
            r.Engine, r.Paradigm, r.Scenario, r.N, r.RunIndex, r.MeanFrameTimeMs, r.MeanFps,
            r.PrivateBytes, r.WindowMode, r.RenderWidth, r.RenderHeight, r.DotsColor,
            r.Profile, r.DotsPipeline, r.FocusLost ? 1 : 0, r.GraphicsApi);
        if (r.CollisionPairsMean.HasValue)
            row += string.Format(CultureInfo.InvariantCulture, ",{0:F2}", r.CollisionPairsMean.Value);
        return row;
    }

    public static void Write(string path, RunRecord r)
    {
        string full = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string tmp = full + ".tmp";
        File.WriteAllText(tmp, HeaderFor(r) + "\n" + FormatRow(r) + "\n");
        if (File.Exists(full)) File.Delete(full);
        File.Move(tmp, full);
    }
}
