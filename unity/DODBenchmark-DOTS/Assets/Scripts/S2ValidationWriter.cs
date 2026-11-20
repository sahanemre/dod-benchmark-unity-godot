using System.Collections.Generic;
using System.IO;

/// <summary>
/// S2 dogrulama kosusunun ciktisi: kare basina cift sayilari.
/// Bicim: frame,pairs,responses[,pairs_bruteforce]
/// .tmp'ye yazilip yeniden adlandirilir (yarim dosya "tamam" sanilmasin).
/// </summary>
public static class S2ValidationWriter
{
    public static void Write(string path, List<string> lines, bool withBruteforce)
    {
        string full = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string header = "frame,pairs,responses" + (withBruteforce ? ",pairs_bruteforce" : "");
        string tmp = full + ".tmp";
        File.WriteAllText(tmp, header + "\n" + string.Join("\n", lines) + "\n");
        if (File.Exists(full)) File.Delete(full);
        File.Move(tmp, full);
    }
}
