using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// S1 gozetimsiz kosu akisi (Unity OOP ve Unity DOTS'ta birebir ayni dosya).
/// Tek sorumluluk: kosunun olcum disi kismini yonetmek: argumanlar ve profil,
/// derleme/grafik API kontrolu, bosta bekleme, warmup sonu bellek + goruntu
/// kaydi, odak takibi ve tek satirlik CSV.
///
/// Olcum davranisina (frame time ornekleme, 3 s warmup + 10 s olcum, hareket)
/// dokunmaz; bunlar yoneticide (BenchmarkManager / DOTSBenchmarkSpawner) kalir.
///
/// Cikis kodlari:
///   0 basarili, 2 gecersiz arguman, 3 ornek yok, 4 bellek okunamadi,
///   5 yazma hatasi, 6 motor kurulumu basarisiz, 8 goruntu modu/cozunurluk
///   beklenenden farkli, 9 gerekli shader yok, 10 build/API profille uyusmuyor.
/// </summary>
public class S1RunSession
{
    public const int ExitOk = 0;
    public const int ExitBadArgs = 2;
    public const int ExitNoSamples = 3;
    public const int ExitNoMemory = 4;
    public const int ExitWriteFailed = 5;
    public const int ExitSetupFailed = 6;
    public const int ExitDisplayMismatch = 8;
    public const int ExitShaderMissing = 9;
    public const int ExitBuildMismatch = 10;

    private const float WarmupSeconds = 3f;

    public readonly RunArgs Args;
    private readonly string engine;
    private readonly string paradigm;
    private readonly string logTag;

    public int UnattendedIndex { get; private set; } = -1;
    public bool Started { get; private set; }
    private float idleTimer;

    // Warmup sonunda bir kez kaydedilenler
    private bool snapshotTaken;
    private long privateBytes = -1;
    private string windowMode = "unknown";
    private int renderWidth;
    private int renderHeight;
    private bool displayOk;

    private bool focusLost;

    public S1RunSession(string engine, string paradigm, string logTag, string[] commandLine)
    {
        this.engine = engine;
        this.paradigm = paradigm;
        this.logTag = logTag;
        Args = RunArgs.Parse(commandLine);
    }

    public bool Parity => Args.IsParity;

    /// <summary>HUD: published profilinde ve elle kullanimda acik; parity gozetimsiz kosuda kapali.</summary>
    public bool HudEnabled => !(Args.Unattended && Args.IsParity);

    /// <summary>Gozetimsiz kosu bekleniyor ama test henuz baslamadi.</summary>
    public bool WaitingToStart => UnattendedIndex >= 0 && !Started;

    /// <summary>
    /// Start'ta cagrilir. Gecersiz durumda uygulamayi hata koduyla kapatir ve
    /// false doner. Elle acilista (--n yok) yalnizca profil bilgisini loglar.
    /// </summary>
    public bool Setup(int[] entityCounts)
    {
        Debug.Log($"{logTag} profile={Args.Profile} display={DisplayProfile.Describe(Args.IsParity)} " +
                  $"graphics={SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceVersion}) " +
                  $"developmentBuild={Debug.isDebugBuild} runInBackground={Application.runInBackground}");

        if (Args.Error != null) return Fail(ExitBadArgs, Args.Error);
        if (!Args.Unattended) return true;

        if (Args.IsParity)
        {
            if (Debug.isDebugBuild)
                return Fail(ExitBuildMismatch, "parity: development build olmamali (Build Profiles > Development Build kapali).");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                return Fail(ExitBuildMismatch,
                    $"parity: Direct3D12 bekleniyordu, aktif API {SystemInfo.graphicsDeviceType} (-force-d3d12 verildi mi?).");
        }

        int index = System.Array.IndexOf(entityCounts, Args.N);
        if (index < 0) return Fail(ExitBadArgs, $"N={Args.N} entityCounts listesinde yok.");

        UnattendedIndex = index;
        Debug.Log($"{logTag} Gozetimsiz kosu: N={Args.N:N0}, run={Args.RunIndex}, seed=" +
                  (Args.IsParity ? Args.Seed.ToString() : "orijinal") + $" -> {Args.OutPath}");
        return true;
    }

    /// <summary>Hata loglar, gozetimsiz akisi durdurur ve uygulamayi kapatir.</summary>
    public bool Fail(int exitCode, string message)
    {
        Debug.LogError($"{logTag} {message}");
        UnattendedIndex = -1;
        Application.Quit(exitCode);
        return false;
    }

    /// <summary>Bosta bekler; testi baslatma zamani geldiginde bir kez true doner.</summary>
    public bool TickIdle(float dt)
    {
        if (!WaitingToStart) return false;
        idleTimer += dt;
        if (idleTimer < Args.IdleDelay) return false;
        Started = true;
        return true;
    }

    /// <summary>Test baslarken (spawn'dan once) cagrilir.</summary>
    public void OnTestStarted()
    {
        snapshotTaken = false;
        privateBytes = -1;
        focusLost = !Application.isFocused;
    }

    public void OnFocusChanged(bool hasFocus, bool testing)
    {
        if (!hasFocus && testing && Started) focusLost = true;
    }

    /// <summary>
    /// Her frame, test zamanlayicisi ilerletildikten sonra cagrilir.
    /// Bellek ve goruntu bilgisi warmup'in son ~100 ms'sinde (en az ~1,5 frame
    /// kala) bir kez okunur. Frame time ornekleri kesintisiz oldugu icin bir
    /// frame'de yapilan is bir SONRAKI frame'in suresine yansir; okumayi warmup
    /// bitmeden yapmak bu maliyetin atilan bir warmup frame'ine dusmesini saglar.
    /// Hicbir frame bu araliga dusmezse (cok yavas frame'ler) olcum penceresinin
    /// ilk frame'inde okunur ve uyari loglanir.
    /// </summary>
    public void AfterTimerAdvance(float testTimer, float dt)
    {
        if (snapshotTaken || !Started) return;

        float window = Mathf.Max(0.1f, 3f * dt);
        float margin = 1.5f * dt;
        bool inSlot = testTimer >= WarmupSeconds - window && testTimer <= WarmupSeconds - margin;
        bool late = testTimer > WarmupSeconds;
        if (!inSlot && !late) return;

        if (late)
            Debug.LogWarning($"{logTag} Bellek warmup icinde okunamadi; olcum penceresinin ilk frame'inde okundu.");

        privateBytes = ProcessMemory.GetPrivateBytes();
        windowMode = DisplayProfile.ModeName(Screen.fullScreenMode);
        renderWidth = Screen.width;
        renderHeight = Screen.height;
        displayOk = DisplayProfile.Matches(Args.IsParity);
        snapshotTaken = true;
    }

    /// <summary>Kosunun sonu: tek satiri yazar ve uygulamayi kapatir.</summary>
    public void Finish(FrameStatistics stats, float testDuration, string dotsColor, string dotsPipeline)
    {
        int exitCode = ExitOk;

        if (stats.SampleCount == 0)
        {
            Debug.LogError($"{logTag} Olcum penceresinde hic ornek yok.");
            exitCode = ExitNoSamples;
        }
        else if (privateBytes < 0)
        {
            Debug.LogError($"{logTag} Process private bytes okunamadi.");
            exitCode = ExitNoMemory;
        }
        else if (!displayOk)
        {
            Debug.LogError($"{logTag} Goruntu kosulu tutmuyor: beklenen {DisplayProfile.Describe(Args.IsParity)}, " +
                           $"olculen {windowMode} {renderWidth}x{renderHeight}.");
            exitCode = ExitDisplayMismatch;
        }
        else
        {
            BenchmarkResult result = stats.Compute(Args.N, 0f, testDuration);
            var record = new RunRecord
            {
                Engine = engine,
                Paradigm = paradigm,
                N = Args.N,
                RunIndex = Args.RunIndex,
                MeanFrameTimeMs = result.AvgFrameTime,
                MeanFps = result.AvgFPS,
                PrivateBytes = privateBytes,
                WindowMode = windowMode,
                RenderWidth = renderWidth,
                RenderHeight = renderHeight,
                DotsColor = dotsColor,
                Profile = Args.Profile,
                DotsPipeline = dotsPipeline,
                FocusLost = focusLost,
            };
            try
            {
                RunCsvWriter.Write(Args.OutPath, record);
                Debug.Log($"{logTag} Kosu tamamlandi: {RunCsvWriter.FormatRow(record)}");
                if (focusLost) Debug.LogWarning($"{logTag} Kosu sirasinda pencere odagi kaybedildi (focus_lost=1).");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{logTag} Sonuc yazilamadi: {e}");
                exitCode = ExitWriteFailed;
            }
        }

        Application.Quit(exitCode);
    }
}
