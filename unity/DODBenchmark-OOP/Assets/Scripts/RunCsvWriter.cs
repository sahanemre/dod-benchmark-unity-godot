using System.Globalization;
using System.IO;

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
        "engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes";

    public static void Write(string path, string engine, string paradigm, string scenario,
        int n, int runIndex, float meanFrameTimeMs, float meanFps, long privateBytes)
    {
        string full = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string row = string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5:F4},{6:F2},{7}",
            engine, paradigm, scenario, n, runIndex, meanFrameTimeMs, meanFps, privateBytes);

        string tmp = full + ".tmp";
        File.WriteAllText(tmp, Header + "\n" + row + "\n");
        if (File.Exists(full)) File.Delete(full);
        File.Move(tmp, full);
    }
}
