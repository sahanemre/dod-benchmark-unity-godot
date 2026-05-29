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

    void Start()
    {
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

        if (!isTesting) return;

        testTimer += Time.unscaledDeltaTime;

        // Ilk 1 saniye warmup — veriye dahil etme
        if (testTimer > 1f)
            stats.AddSample(currentFrameTime);

        // Bellek olcumu (test ortasinda)
        if (testTimer > testDuration / 2f && memoryDuringTest == 0)
            memoryDuringTest = System.GC.GetTotalMemory(false);

        if (testTimer >= testDuration + 1f)
        {
            EndCurrentTest();
        }
        else
        {
            statusText = $"Test: {entityCounts[currentTestIndex]:N0} entity | " +
                         $"FPS: {currentFPS:F0} | Frame: {currentFrameTime:F2}ms | " +
                         $"Kalan: {(testDuration + 1f - testTimer):F0}s";
        }
    }

    void StartTest(int countIndex)
    {
        currentTestIndex = countIndex;
        isTesting = true;
        testTimer = 0f;
        stats.Reset();
        memoryDuringTest = 0;

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        memoryAtStart = System.GC.GetTotalMemory(true);

        spawner.Spawn(entityCounts[countIndex]);
        Debug.Log($"[Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

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
