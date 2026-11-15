using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Benchmark orkestratoru (Unity DOTS / hybrid).
/// Tek sorumluluk: test akisini yonetmek. Entity olusturma EntityFactory'de,
/// hareket MovementSystem'de, istatistik/CSV/UI ayri siniflarda.
///
/// "Hybrid" yaklasim: MonoBehaviour (UI + benchmark yonetimi) + ECS (workload).
/// DOD prensibi olculen is yukunde (MovementSystem + data component'lar) yasar.
/// </summary>
public class DOTSBenchmarkSpawner : MonoBehaviour
{
    [Header("Benchmark Ayarlari")]
    public int[] entityCounts = { 1000, 5000, 10000, 50000, 100000 };
    public float testDuration = 10f;
    public float moveSpeed = 3f;

    [Header("Render Ayarlari")]
    public Mesh entityMesh;
    public Material entityMaterial;

    // Ayrik sorumluluklar
    private EntityFactory factory;
    private readonly FrameStatistics stats = new FrameStatistics();
    private readonly CsvExporter csv = new CsvExporter();
    private readonly BenchmarkHUD hud = new BenchmarkHUD();

    // Test durumu
    private bool isTesting = false;
    private float testTimer = 0f;
    private int currentTestIndex = -1;
    private bool allTestsMode = false;

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

        // HUD'u EN ONCE kur: render/DOTS kurulumu hata verse bile panel ve
        // butonlar her zaman gorunur. (Build'de Shader.Find null donerse eski
        // kodda Start() yarida kesiliyor ve butonlar hic kurulmuyordu.)
        hud.Title = "DOD Benchmark — Unity DOTS";
        hud.HighlightColor = Color.cyan;
        hud.EntityCounts = entityCounts;
        hud.OnSelect = i => hud.SelectedIndex = i;
        hud.OnStartSingle = () => { csv.Reset(); StartTest(hud.SelectedIndex); };
        hud.OnStartAll = () => { allTestsMode = true; csv.Reset(); StartTest(0); };

        // Arguman kontrolu render/DOTS kurulumundan once: kurulum yarida
        // kalsa bile gozetimsiz kosu hata koduyla cikabilsin.
        SetupUnattended();

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        Camera cam = Camera.main;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        float2 screenMin = new float2(-width + 0.5f, -height + 0.5f);
        float2 screenMax = new float2(width - 0.5f, height - 0.5f);

        if (entityMesh == null)
            entityMesh = EntityFactory.CreateQuadMesh();

        if (entityMaterial == null)
            entityMaterial = CreateDefaultMaterial();

        if (entityMaterial == null)
        {
            statusText = "HATA: Uygun shader bulunamadi.\n" +
                         "Inspector'da Entity Material ata VEYA URP/Unlit'i\n" +
                         "Project Settings > Graphics > Always Included Shaders'a ekle.";
            Debug.LogError("[DOTS Benchmark] " + statusText);
            return;
        }

        factory = new EntityFactory(entityManager, entityMesh, entityMaterial, moveSpeed, screenMin, screenMax);
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
            Debug.LogError($"[DOTS Benchmark] {runArgs.Error}");
            Application.Quit(2);
            return;
        }

        unattendedIndex = System.Array.IndexOf(entityCounts, runArgs.N);
        if (unattendedIndex < 0)
        {
            Debug.LogError($"[DOTS Benchmark] N={runArgs.N} entityCounts listesinde yok.");
            Application.Quit(2);
            return;
        }

        statusText = $"Gozetimsiz kosu: N={runArgs.N:N0}, run={runArgs.RunIndex}";
        Debug.Log($"[DOTS Benchmark] {statusText} -> {runArgs.OutPath}");
    }

    /// <summary>
    /// Build'de URP shader'lari strip edilebilir; bu yuzden birden fazla shader
    /// denenir. Hicbiri bulunamazsa null doner (Start crash etmek yerine
    /// kullaniciya talimat gosterir). Not: DOTS/Entities Graphics ile entity
    /// cizimi icin URP shader'i sart — fallback'ler yalnizca crash'i onler.
    /// </summary>
    static Material CreateDefaultMaterial()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
            "Unlit/Color"
        };

        foreach (string name in candidates)
        {
            Shader shader = Shader.Find(name);
            if (shader != null)
            {
                var mat = new Material(shader) { color = Color.white };
                return mat;
            }
        }
        return null;
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
                if (factory == null)
                {
                    // Render/DOTS kurulumu basarisiz (orn. shader bulunamadi)
                    Debug.LogError("[DOTS Benchmark] EntityFactory kurulamadi; kosu iptal.");
                    Application.Quit(6);
                    return;
                }
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

        factory.Spawn(entityCounts[countIndex]);
        Debug.Log($"[DOTS Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
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
            Debug.Log($"[DOTS Benchmark] {statusText}");
        }

        if (allTestsMode && currentTestIndex < entityCounts.Length - 1)
        {
            StartTest(currentTestIndex + 1);
        }
        else
        {
            allTestsMode = false;
            factory.DestroyAll();
            string file = csv.Save("unity_dots");
            statusText += $"\nCSV: {file}";
        }
    }

    /// <summary>
    /// Gozetimsiz kosunun sonu: tek satiri yazar ve uygulamayi kapatir.
    /// Cikis kodlari: 0 basarili, 2 gecersiz arguman, 3 ornek yok,
    /// 4 bellek okunamadi, 5 yazma hatasi, 6 DOTS/render kurulumu basarisiz.
    /// </summary>
    void FinishUnattended()
    {
        int exitCode = 0;

        if (stats.SampleCount == 0)
        {
            Debug.LogError("[DOTS Benchmark] Olcum penceresinde hic ornek yok.");
            exitCode = 3;
        }
        else if (privateBytesAfterWarmup < 0)
        {
            Debug.LogError("[DOTS Benchmark] Process private bytes okunamadi.");
            exitCode = 4;
        }
        else
        {
            BenchmarkResult result = stats.Compute(runArgs.N, 0f, testDuration);
            try
            {
                RunCsvWriter.Write(runArgs.OutPath, "unity", "dod", "S1", runArgs.N, runArgs.RunIndex,
                    result.AvgFrameTime, result.AvgFPS, privateBytesAfterWarmup);
                Debug.Log($"[DOTS Benchmark] Kosu tamamlandi: N={runArgs.N:N0} run={runArgs.RunIndex} | " +
                          $"Ort: {result.AvgFrameTime:F3}ms ({result.AvgFPS:F1} FPS) | " +
                          $"Private bytes: {privateBytesAfterWarmup:N0}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS Benchmark] Sonuc yazilamadi: {e}");
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
        hud.ActiveCount = factory != null ? factory.SpawnedCount : 0;
        hud.StatusText = statusText;
        hud.Draw();
    }
}
