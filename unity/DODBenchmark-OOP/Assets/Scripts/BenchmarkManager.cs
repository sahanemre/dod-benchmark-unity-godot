using UnityEngine;

/// <summary>
/// Benchmark orkestratoru (OOP).
/// Tek sorumluluk: test akisini yonetmek. Entity spawn'i, istatistik, CSV
/// ve UI islerini ayri siniflara devreder (EntitySpawner, FrameStatistics,
/// CsvExporter, BenchmarkHUD). Gozetimsiz S1 kosusunun olcum disi kismi
/// S1RunSession'dadir.
///
/// Kullanim:
/// 1. Sahneye bos GameObject ekle, adini "BenchmarkManager" yap
/// 2. Bu scripti o objeye ekle
/// 3. Play'e bas, sol ustteki panelden test baslat
///    (gozetimsiz kosu: tools/run_s1.ps1, bkz. tools/S1_CALISTIRMA.md)
/// </summary>
public class BenchmarkManager : MonoBehaviour
{
    [Header("Benchmark Ayarlari")]
    public int[] entityCounts = { 1000, 5000, 10000, 50000, 100000 };
    public float testDuration = 10f;
    public float moveSpeed = 3f;

    [Header("Entity Gorunumu")]
    public Sprite entitySprite;

    // parity profili: diger uc konfigurasyon gibi isiksiz (unlit) sprite.
    // published: SpriteRenderer'in varsayilan materyali (URP 2D Sprite-Lit-Default).
    private const string UnlitSpriteShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    // Ayrik sorumluluklar
    private EntitySpawner spawner;
    private readonly FrameStatistics stats = new FrameStatistics();
    private readonly CsvExporter csv = new CsvExporter();
    private readonly BenchmarkHUD hud = new BenchmarkHUD();
    private S1RunSession session;

    // Test durumu
    private int currentTestIndex = -1;
    private bool isTesting = false;
    private bool allTestsMode = false;
    private float testTimer = 0f;

    // Bellek olcum (eski, motor sayaci; yalnizca HUD/elle kullanimda)
    private long memoryAtStart = 0;
    private long memoryDuringTest = 0;

    // Anlik metrikler
    private float currentFPS = 0f;
    private float currentFrameTime = 0f;
    private string statusText = "Hazir. Test secin ve baslatin.";

    void Start()
    {
        session = new S1RunSession("unity", "oop", "[Benchmark]", System.Environment.GetCommandLineArgs());

        // Odak kaybinda da calismaya devam et; kosu sirasinda odak kaybedilirse
        // CSV'de focus_lost=1 olarak isaretlenir.
        Application.runInBackground = true;
        DisplayProfile.Apply(session.Parity);

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

        Material spriteMaterial = null;
        if (session.Parity)
        {
            Shader unlit = Shader.Find(UnlitSpriteShader);
            if (unlit != null)
                spriteMaterial = new Material(unlit);
            else
                Debug.LogError($"[Benchmark] '{UnlitSpriteShader}' build'de yok " +
                               "(Project Settings > Graphics > Always Included Shaders).");
        }

        spawner = new EntitySpawner(entitySprite, moveSpeed, screenMin, screenMax, spriteMaterial);

        hud.Title = "DOD Benchmark — Unity OOP";
        hud.HighlightColor = Color.green;
        hud.EntityCounts = entityCounts;
        hud.OnSelect = i => hud.SelectedIndex = i;
        hud.OnStartSingle = () => { csv.Reset(); StartTest(hud.SelectedIndex); };
        hud.OnStartAll = () => { allTestsMode = true; csv.Reset(); StartTest(0); };
        if (session.HudEnabled)
            gameObject.AddComponent<HudDrawer>().Draw = DrawHud;

        if (!session.Setup(entityCounts)) return;
        if (session.WaitingToStart)
        {
            if (session.Parity && spriteMaterial == null)
            {
                session.Fail(S1RunSession.ExitShaderMissing, "parity: isiksiz sprite shader'i olmadan kosulamaz.");
                return;
            }
            statusText = $"Gozetimsiz kosu: N={session.Args.N:N0}, run={session.Args.RunIndex}";
        }
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
        if (session.WaitingToStart)
        {
            if (session.TickIdle(Time.unscaledDeltaTime))
            {
                csv.Reset();
                StartTest(session.UnattendedIndex);
            }
            return;
        }

        if (!isTesting) return;

        testTimer += Time.unscaledDeltaTime;

        // Warmup'in son ~100 ms'sinde OS bellegi + goruntu bilgisi (gozetimsiz kosu)
        session.AfterTimerAdvance(testTimer, Time.unscaledDeltaTime);

        // Ilk 3 saniye warmup — veriye dahil etme
        if (testTimer > 3f)
            stats.AddSample(currentFrameTime);

        // Eski bellek olcumu (motor sayaci, test ortasinda). Gozetimsiz kosuda
        // kapali: olcum penceresinde ornekleme yapilmaz.
        if (!session.Started && testTimer > testDuration / 2f && memoryDuringTest == 0)
            memoryDuringTest = System.GC.GetTotalMemory(false);

        if (testTimer >= testDuration + 3f)
        {
            EndCurrentTest();
        }
        else if (session.HudEnabled)
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
        if (session.Started) session.OnTestStarted();

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        if (!session.Started)
            memoryAtStart = System.GC.GetTotalMemory(true);

        // parity: tekrarlanabilir baslangic (pozisyon, hiz, renk) — seed = 1000 + run_index.
        // published: orijinal davranis (seed verilmez).
        if (session.Parity)
            Random.InitState(session.Args.Seed);

        spawner.Spawn(entityCounts[countIndex]);
        Debug.Log($"[Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

        if (session.Started)
        {
            session.Finish(stats, testDuration, "na", "na");
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

    void OnApplicationFocus(bool hasFocus)
    {
        session?.OnFocusChanged(hasFocus, isTesting);
    }

    /// <summary>HudDrawer.OnGUI tarafindan cagrilir (HUD aciksa).</summary>
    void DrawHud()
    {
        hud.IsTesting = isTesting;
        hud.Fps = currentFPS;
        hud.FrameTimeMs = currentFrameTime;
        hud.ActiveCount = spawner != null ? spawner.ActiveCount : 0;
        hud.StatusText = statusText;
        hud.Draw();
    }
}
