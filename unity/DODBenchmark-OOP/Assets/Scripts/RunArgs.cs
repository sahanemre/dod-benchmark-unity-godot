using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// S1 komut satiri argumanlari.
/// Tek sorumluluk: komut satirini okumak ve dogrulamak.
///
/// Ornek: DODBenchmark-OOP.exe --profile=parity --n 50000 --run-index 3 --out C:/sonuc/x.csv
/// ("--k=v" ve "--k v" bicimlerinin ikisi de kabul edilir.)
///
/// --profile  published | parity   (varsayilan parity)
///   published: makaledeki kosullarin aynisi
///   parity   : dort konfigurasyonda esit goruntu/render kosullari
/// --dots-pipeline original | lean (yalnizca Unity DOTS; varsayilan profile gore:
///   published -> original, parity -> lean)
/// --graphics-api d3d11 | d3d12 (beklenen grafik API'si; Unity'de -force-d3d11/12
///   ile birlikte verilir). Varsayilan: parity -> d3d12, published -> kontrol yok.
///
/// --n verilmezse Unattended=false olur ve uygulama HUD ile manuel calisir.
/// </summary>
public class RunArgs
{
    /// <summary>parity profilinde seed = BaseSeed + run_index (dort konfigurasyonda ayni).</summary>
    public const int BaseSeed = 1000;

    public bool Unattended;          // --n verildi mi?
    public int N;                    // entity sayisi
    public int RunIndex;             // tekrar numarasi (1..R); manuel modda 0
    public string OutPath;           // tek satirlik CSV'nin yazilacagi dosya
    public float IdleDelay = 2f;     // testi baslatmadan once bosta bekleme (s)
    public string Profile = "parity";
    public string DotsPipelineArg;   // null = profilin varsayilani
    public string GraphicsApiArg;    // null = profilin varsayilani
    public string Error;             // null degilse argumanlar gecersiz

    public bool IsParity => Profile == "parity";
    public int Seed => BaseSeed + RunIndex;
    public string DotsPipeline => DotsPipelineArg ?? (IsParity ? "lean" : "original");
    /// <summary>Beklenen grafik API'si (d3d11 | d3d12) ya da null (published: kontrol yok).</summary>
    public string ExpectedGraphicsApi => GraphicsApiArg ?? (IsParity ? "d3d12" : null);

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
        var ci = CultureInfo.InvariantCulture;

        // Profil ve DOTS pipeline manuel modda da gecerli
        if (map.TryGetValue("profile", out string prof))
        {
            prof = prof.ToLowerInvariant();
            if (prof != "published" && prof != "parity")
            {
                r.Error = $"Gecersiz --profile degeri: '{prof}' (published | parity)";
                return r;
            }
            r.Profile = prof;
        }
        if (map.TryGetValue("dots-pipeline", out string pipe))
        {
            pipe = pipe.ToLowerInvariant();
            if (pipe != "original" && pipe != "lean")
            {
                r.Error = $"Gecersiz --dots-pipeline degeri: '{pipe}' (original | lean)";
                return r;
            }
            r.DotsPipelineArg = pipe;
        }
        if (map.TryGetValue("graphics-api", out string api))
        {
            api = api.ToLowerInvariant();
            if (api != "d3d11" && api != "d3d12")
            {
                r.Error = $"Gecersiz --graphics-api degeri: '{api}' (d3d11 | d3d12)";
                return r;
            }
            r.GraphicsApiArg = api;
        }

        if (!map.ContainsKey("n")) return r;
        r.Unattended = true;

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
