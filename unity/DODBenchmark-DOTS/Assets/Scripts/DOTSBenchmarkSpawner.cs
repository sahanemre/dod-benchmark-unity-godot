using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Benchmark orkestratoru (Unity DOTS / hybrid).
/// Tek sorumluluk: test akisini yonetmek. Entity olusturma EntityFactory'de,
/// hareket MovementSystem'de, istatistik/CSV/UI ayri siniflarda. Gozetimsiz
/// S1 kosusunun olcum disi kismi S1RunSession'dadir.
///
/// "Hybrid" yaklasim: MonoBehaviour (UI + benchmark yonetimi) + ECS (workload).
/// DOD prensibi olculen is yukunde (MovementSystem + data component'lar) yasar.
///
/// Profiller (bkz. RunArgs):
///   published: makaledeki kosullar — tek beyaz materyal (entity basina renk
///              yok), orijinal URP pipeline (post-processing, skybox, depth +
///              opaque kopyalari, golgeli isik), orijinal RNG.
///   parity   : entity basina renk (URPMaterialPropertyBaseColor, isiksiz
///              URP/Unlit), "lean" pipeline, seed = 1000 + run_index.
///              --dots-pipeline=original ile yalnizca pipeline etkisi ayrilir.
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

    // parity: dort konfigurasyon gibi isiksiz; DOTS instancing ile _BaseColor destekler.
    private const string ParityShader = "Universal Render Pipeline/Unlit";

    // Ayrik sorumluluklar
    private EntityFactory factory;
    private readonly FrameStatistics stats = new FrameStatistics();
    private readonly CsvExporter csv = new CsvExporter();
    private readonly BenchmarkHUD hud = new BenchmarkHUD();
    private S1RunSession session;

    // Test durumu
    private bool isTesting = false;
    private float testTimer = 0f;
    private int currentTestIndex = -1;
    private bool allTestsMode = false;

    // Profil ayarlari
    private bool perEntityColor = false;     // parity: on, published: off
    private string pipelineName = "original";

    // Bellek olcum (eski, motor sayaci; yalnizca HUD/elle kullanimda)
    private long memoryAtStart = 0;
    private long memoryDuringTest = 0;

    // Anlik metrikler
    private float currentFPS = 0f;
    private float currentFrameTime = 0f;
    private string statusText = "Hazir. Test secin ve baslatin.";

    void Start()
    {
        session = new S1RunSession("unity", "dod", "[DOTS Benchmark]", System.Environment.GetCommandLineArgs());

        // Odak kaybinda da calismaya devam et; kosu sirasinda odak kaybedilirse
        // CSV'de focus_lost=1 olarak isaretlenir.
        Application.runInBackground = true;
        DisplayProfile.Apply(session.Parity);

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
        if (session.HudEnabled)
            gameObject.AddComponent<HudDrawer>().Draw = DrawHud;

        // Arguman kontrolu render/DOTS kurulumundan once: kurulum yarida
        // kalsa bile gozetimsiz kosu hata koduyla cikabilsin.
        if (!session.Setup(entityCounts)) return;

        perEntityColor = session.Parity;
        pipelineName = session.Args.DotsPipeline;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        Camera cam = Camera.main;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        float2 screenMin = new float2(-width + 0.5f, -height + 0.5f);
        float2 screenMax = new float2(width - 0.5f, height - 0.5f);

        if (pipelineName == "lean")
            ApplyLeanPipeline(cam);

        if (entityMesh == null)
            entityMesh = EntityFactory.CreateQuadMesh();

        if (entityMaterial == null)
            entityMaterial = session.Parity ? CreateParityMaterial() : CreateDefaultMaterial();

        if (entityMaterial == null)
        {
            statusText = "HATA: Uygun shader bulunamadi.\n" +
                         "Inspector'da Entity Material ata VEYA URP/Unlit'i\n" +
                         "Project Settings > Graphics > Always Included Shaders'a ekle.";
            Debug.LogError("[DOTS Benchmark] " + statusText);
            if (session.WaitingToStart)
                session.Fail(S1RunSession.ExitShaderMissing, "Entity materyali olusturulamadi.");
            return;
        }

        Debug.Log($"[DOTS Benchmark] entity shader: {entityMaterial.shader.name} | " +
                  $"renk: {(perEntityColor ? "entity basina" : "tek beyaz")} | pipeline: {pipelineName}");

        factory = new EntityFactory(entityManager, entityMesh, entityMaterial, moveSpeed, screenMin, screenMax);

        if (session.WaitingToStart)
            statusText = $"Gozetimsiz kosu: N={session.Args.N:N0}, run={session.Args.RunIndex}";
    }

    /// <summary>
    /// published: makaledeki shader secimini yeniden uretir.
    ///
    /// Makaledeki kod sirasiyla URP/Unlit, URP/Lit, Sprites/Default ve
    /// Unlit/Color deniyordu. URP/Unlit o build'de YOKTU: Graphics ayarlari
    /// proje olusturuldugundan (2026-05-17) beri degismedi ve shader'i build'e
    /// alan hicbir referans yoktu. Bu yuzden makalede ilk aday null dondu ve
    /// bir sonraki mevcut shader kullanildi.
    /// Artik URP/Unlit parity icin Always Included Shaders'da oldugundan,
    /// published sirasinda atlanir. Kalan siralama ve secim makaledekiyle
    /// aynidir; secilen shader Start'ta loglanir ("entity shader: ...").
    /// Hicbiri bulunamazsa null doner (Start crash etmek yerine talimat gosterir).
    /// </summary>
    static Material CreateDefaultMaterial()
    {
        string[] candidates =
        {
            // "Universal Render Pipeline/Unlit" — makaledeki build'de yoktu (bkz. yukarisi)
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

    /// <summary>
    /// parity: yalnizca URP/Unlit kabul edilir (isiksiz + _BaseColor icin DOTS
    /// instancing destegi). Build'de yoksa null doner ve kosu hata koduyla biter.
    /// </summary>
    static Material CreateParityMaterial()
    {
        Shader shader = Shader.Find(ParityShader);
        return shader != null ? new Material(shader) { color = Color.white } : null;
    }

    /// <summary>
    /// "lean" pipeline: URP 3D sablonundan gelen, is yukuyle ilgisiz frame basi
    /// maliyetleri kapatir; Unity OOP (URP 2D) kosullarina yaklastirir.
    /// - post-processing kapali (Bloom, Vignette, Tonemapping; Global Volume devre disi)
    /// - skybox yerine duz renk temizleme (Unity OOP ile ayni arka plan rengi)
    /// - depth ve opaque texture kopyalari kapali
    /// - golge kapali (kamera ve isiklar)
    /// Kamera/dunya boyutu degismez.
    /// </summary>
    static void ApplyLeanPipeline(Camera cam)
    {
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);

        var cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.renderShadows = false;

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            light.shadows = LightShadows.None;
        foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            volume.enabled = false;
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
                if (factory == null)
                {
                    session.Fail(S1RunSession.ExitSetupFailed, "EntityFactory kurulamadi; kosu iptal.");
                    return;
                }
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

        // parity: seed = 1000 + run_index (pozisyon, hiz, renk). published: orijinal (saat).
        uint? seed = session.Parity ? (uint)session.Args.Seed : (uint?)null;
        factory.Spawn(entityCounts[countIndex], seed, perEntityColor);
        Debug.Log($"[DOTS Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

        if (session.Started)
        {
            session.Finish(stats, testDuration, perEntityColor ? "on" : "off", pipelineName);
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
        hud.ActiveCount = factory != null ? factory.SpawnedCount : 0;
        hud.StatusText = statusText;
        hud.Draw();
    }
}
