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

        if (!isTesting) return;

        testTimer += Time.unscaledDeltaTime;

        // Ilk 1 saniye warmup — veriye dahil etme
        if (testTimer > 3f)
            stats.AddSample(currentFrameTime);

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

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        memoryAtStart = System.GC.GetTotalMemory(true);

        factory.Spawn(entityCounts[countIndex]);
        Debug.Log($"[DOTS Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
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
