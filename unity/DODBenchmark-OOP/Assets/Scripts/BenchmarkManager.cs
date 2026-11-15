using UnityEngine;

/// <summary>
/// Benchmark orkestratoru (OOP).
/// Tek sorumluluk: test akisini yonetmek. Entity spawn'i, istatistik, CSV
/// ve UI islerini ayri siniflara devreder (EntitySpawner, FrameStatistics,
/// CsvExporter, BenchmarkHUD).
///
/// Kullanim:
/// 1. Sahneye bos GameObject ekle, adini "BenchmarkManager" yap
/// 2. Bu scripti o objeye ekle
/// 3. Play'e bas, sol ustteki panelden test baslat
/// </summary>
public class BenchmarkManager : MonoBehaviour
{
    [Header("Benchmark Ayarlari")]
    public int[] entityCounts = { 1000, 5000, 10000, 50000, 100000 };
    public float testDuration = 10f;
    public float moveSpeed = 3f;

    [Header("Entity Gorunumu")]
    public Sprite entitySprite;

    // Ayrik sorumluluklar
    private EntitySpawner spawner;
    private readonly FrameStatistics stats = new FrameStatistics();
    private readonly CsvExporter csv = new CsvExporter();
    private readonly BenchmarkHUD hud = new BenchmarkHUD();

    // Test durumu
    private int currentTestIndex = -1;
    private bool isTesting = false;
    private bool allTestsMode = false;
    private float testTimer = 0f;

    // Bellek olcum
    private long memoryAtStart = 0;
    private long memoryDuringTest = 0;

    // Anlik metrikler
    private float currentFPS = 0f;
    private float currentFrameTime = 0f;
    private string statusText = "Hazir. Test secin ve baslatin.";

    // OS duzeyi bellek: warmup biter bitmez bir kez okunur (process private bytes)
    private long privateBytesAfterWarmup = -1;
    private bool privateBytesRecorded = false;

    // Gozetimsiz kosu (komut satirindan --n verildiginde)
    private RunArgs runArgs;
    private int unattendedIndex = -1;
    private bool unattendedStarted = false;
    private float idleTimer = 0f;

    void Start()
    {
        // VSync KAPALI + FPS sinirsiz: aksi halde frame time ekran yenileme
        // hizina kilitlenir ve gercek is yuku olculmez. Benchmark icin sart.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        Camera cam = Camera.main;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        Vector2 screenMin = new Vector2(-width + 0.5f, -height + 0.5f);
        Vector2 screenMax = new Vector2(width - 0.5f, height - 0.5f);

        if (entitySprite == null)
            entitySprite = CreateDefaultSprite();

        spawner = new EntitySpawner(entitySprite, moveSpeed, screenMin, screenMax);

        hud.Title = "DOD Benchmark — Unity OOP";
        hud.HighlightColor = Color.green;
        hud.EntityCounts = entityCounts;
        hud.OnSelect = i => hud.SelectedIndex = i;
        hud.OnStartSingle = () => { csv.Reset(); StartTest(hud.SelectedIndex); };
        hud.OnStartAll = () => { allTestsMode = true; csv.Reset(); StartTest(0); };

        SetupUnattended();
    }

    /// <summary>
    /// --n verilmisse gozetimsiz moda gecer. Gecersiz arguman veya listede
    /// olmayan N hata koduyla cikar (calistirma scripti bunu kaydeder).
    /// </summary>
    void SetupUnattended()
    {
        runArgs = RunArgs.Parse(System.Environment.GetCommandLineArgs());
        if (!runArgs.Active) return;

        if (runArgs.Error != null)
        {
            Debug.LogError($"[Benchmark] {runArgs.Error}");
            Application.Quit(2);
            return;
        }

        unattendedIndex = System.Array.IndexOf(entityCounts, runArgs.N);
        if (unattendedIndex < 0)
        {
            Debug.LogError($"[Benchmark] N={runArgs.N} entityCounts listesinde yok.");
            Application.Quit(2);
            return;
        }

        statusText = $"Gozetimsiz kosu: N={runArgs.N:N0}, run={runArgs.RunIndex}";
        Debug.Log($"[Benchmark] {statusText} -> {runArgs.OutPath}");
    }

    static Sprite CreateDefaultSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    void Update()
    {
        currentFrameTime = Time.unscaledDeltaTime * 1000f;
        currentFPS = 1f / Time.unscaledDeltaTime;

        // Gozetimsiz mod: butona basmanin yerine, motor acilisindan sonra kisa
        // bir bekleme ile testi baslatir. Manuel akista StartTest OnGUI'de
        // (Update'ten sonra) cagrilir; ayni zamanlamayi korumak icin burada
        // baslatip bu frame'in Update'ini bitiriyoruz.
        if (unattendedIndex >= 0 && !unattendedStarted)
        {
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer >= runArgs.IdleDelay)
            {
                unattendedStarted = true;
                csv.Reset();
                StartTest(unattendedIndex);
            }
            return;
        }

        if (!isTesting) return;

        testTimer += Time.unscaledDeltaTime;

        // Ilk 3 saniye warmup — veriye dahil etme
        if (testTimer > 3f)
        {
            stats.AddSample(currentFrameTime);

            // Warmup bitti: OS duzeyi bellegi bir kez oku
            if (!privateBytesRecorded)
            {
                privateBytesAfterWarmup = ProcessMemory.GetPrivateBytes();
                privateBytesRecorded = true;
            }
        }

        // Bellek olcumu (test ortasinda)
        if (testTimer > testDuration / 2f && memoryDuringTest == 0)
            memoryDuringTest = System.GC.GetTotalMemory(false);

        if (testTimer >= testDuration + 3f)
        {
            EndCurrentTest();
        }
        else
        {
            statusText = $"Test: {entityCounts[currentTestIndex]:N0} entity | " +
                         $"FPS: {currentFPS:F0} | Frame: {currentFrameTime:F2}ms | " +
                         $"Kalan: {(testDuration + 3f - testTimer):F0}s";
        }
    }

    void StartTest(int countIndex)
    {
        currentTestIndex = countIndex;
        isTesting = true;
        testTimer = 0f;
        stats.Reset();
        memoryDuringTest = 0;
        privateBytesAfterWarmup = -1;
        privateBytesRecorded = false;

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        memoryAtStart = System.GC.GetTotalMemory(true);

        spawner.Spawn(entityCounts[countIndex]);
        Debug.Log($"[Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

        if (unattendedStarted)
        {
            FinishUnattended();
            return;
        }

        if (stats.SampleCount > 0)
        {
            float memoryMB = (memoryDuringTest - memoryAtStart) / (1024f * 1024f);
            if (memoryMB < 0) memoryMB = memoryDuringTest / (1024f * 1024f);

            BenchmarkResult result = stats.Compute(entityCounts[currentTestIndex], memoryMB, testDuration);
            csv.AddRow(result);

            statusText = $"Tamamlandi: {result.EntityCount:N0} entity | " +
                         $"Ort: {result.AvgFrameTime:F2}ms ({result.AvgFPS:F0} FPS) | " +
                         $"StdDev: {result.StdDev:F2}ms | Bellek: {result.MemoryMB:F1} MB";
            Debug.Log($"[Benchmark] {statusText}");
        }

        // Tum testler modunda sonraki teste gec
        if (allTestsMode && currentTestIndex < entityCounts.Length - 1)
        {
            StartTest(currentTestIndex + 1);
        }
        else
        {
            allTestsMode = false;
            spawner.Clear();
            string file = csv.Save("unity_oop");
            statusText += $"\nCSV: {file}";
        }
    }

    /// <summary>
    /// Gozetimsiz kosunun sonu: tek satiri yazar ve uygulamayi kapatir.
    /// Cikis kodlari: 0 basarili, 2 gecersiz arguman, 3 ornek yok,
    /// 4 bellek okunamadi, 5 yazma hatasi.
    /// </summary>
    void FinishUnattended()
    {
        int exitCode = 0;

        if (stats.SampleCount == 0)
        {
            Debug.LogError("[Benchmark] Olcum penceresinde hic ornek yok.");
            exitCode = 3;
        }
        else if (privateBytesAfterWarmup < 0)
        {
            Debug.LogError("[Benchmark] Process private bytes okunamadi.");
            exitCode = 4;
        }
        else
        {
            BenchmarkResult result = stats.Compute(runArgs.N, 0f, testDuration);
            try
            {
                RunCsvWriter.Write(runArgs.OutPath, "unity", "oop", "S1", runArgs.N, runArgs.RunIndex,
                    result.AvgFrameTime, result.AvgFPS, privateBytesAfterWarmup);
                Debug.Log($"[Benchmark] Kosu tamamlandi: N={runArgs.N:N0} run={runArgs.RunIndex} | " +
                          $"Ort: {result.AvgFrameTime:F3}ms ({result.AvgFPS:F1} FPS) | " +
                          $"Private bytes: {privateBytesAfterWarmup:N0}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Benchmark] Sonuc yazilamadi: {e}");
                exitCode = 5;
            }
        }

        Application.Quit(exitCode);
    }

    void OnGUI()
    {
        hud.IsTesting = isTesting;
        hud.Fps = currentFPS;
        hud.FrameTimeMs = currentFrameTime;
        hud.ActiveCount = spawner != null ? spawner.ActiveCount : 0;
        hud.StatusText = statusText;
        hud.Draw();
    }
}
