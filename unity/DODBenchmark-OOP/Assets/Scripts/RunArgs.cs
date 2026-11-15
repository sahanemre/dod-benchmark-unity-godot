using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Gozetimsiz (unattended) kosu argumanlari.
/// Tek sorumluluk: komut satirini okumak ve dogrulamak.
///
/// Ornek: DODBenchmark-OOP.exe --n 50000 --run-index 3 --out C:/sonuc/x.csv
/// ("--n=50000" bicimi de kabul edilir.)
///
/// --n verilmezse Active=false olur ve uygulama eskisi gibi HUD ile
/// manuel calisir (yayinlanan olcum akisi degismez).
/// </summary>
public class RunArgs
{
    public bool Active;        // --n verildi mi?
    public int N;              // entity sayisi
    public int RunIndex;       // tekrar numarasi (1..R)
    public string OutPath;     // tek satirlik CSV'nin yazilacagi dosya
    public float IdleDelay = 2f; // testi baslatmadan once bosta bekleme (s)
    public string Error;       // null degilse argumanlar gecersiz

    public static RunArgs Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (!a.StartsWith("--")) continue;

            string key = a.Substring(2);
            string val = "";
            int eq = key.IndexOf('=');
            if (eq >= 0)
            {
                val = key.Substring(eq + 1);
                key = key.Substring(0, eq);
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                val = args[i + 1];
                i++;
            }
            map[key] = val;
        }

        var r = new RunArgs();
        if (!map.ContainsKey("n")) return r;

        r.Active = true;
        var ci = CultureInfo.InvariantCulture;

        if (!int.TryParse(map["n"], NumberStyles.Integer, ci, out r.N) || r.N <= 0)
            r.Error = $"Gecersiz --n degeri: '{map["n"]}'";
        else if (!map.TryGetValue("run-index", out string ri) ||
                 !int.TryParse(ri, NumberStyles.Integer, ci, out r.RunIndex) || r.RunIndex <= 0)
            r.Error = "--run-index eksik veya gecersiz (1 veya daha buyuk tam sayi olmali)";
        else if (!map.TryGetValue("out", out r.OutPath) || string.IsNullOrEmpty(r.OutPath))
            r.Error = "--out eksik (sonuc CSV dosyasinin yolu)";
        else if (map.TryGetValue("idle-delay", out string idle) &&
                 (!float.TryParse(idle, NumberStyles.Float, ci, out r.IdleDelay) || r.IdleDelay < 0f))
            r.Error = $"Gecersiz --idle-delay degeri: '{idle}'";

        return r;
    }
}
