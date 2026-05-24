using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Entity spawner — MonoBehaviour olarak sahneye eklenir.
/// ECS entity'lerini olusturur ve benchmark'i yonetir.
/// 
/// Bu script "hybrid" yaklasim kullanir:
/// MonoBehaviour (UI, benchmark yonetimi) + ECS (entity hareketi).
/// Gercek oyunlarda da bu yaklasim yaygindir.
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

    // Dahili
    private EntityManager entityManager;
    private Entity entityPrefab;
    private bool isTesting = false;
    private float testTimer = 0f;
    private int currentTestIndex = -1;
    private int selectedCountIndex = 0;
    private bool allTestsMode = false;
    private int spawnedCount = 0;

    // Performans
    private System.Collections.Generic.List<float> frameTimes = new System.Collections.Generic.List<float>();
    private System.Text.StringBuilder csvBuilder = new System.Text.StringBuilder();
    private string statusText = "Hazir. Test secin ve baslatin.";
    private float currentFPS = 0f;
    private float currentFrameTime = 0f;

    // Bellek
    private long memoryAtStart = 0;
    private long memoryDuringTest = 0;

    // Ekran sinirlari
    private float2 screenMin;
    private float2 screenMax;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Ekran sinirlari
        Camera cam = Camera.main;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        screenMin = new float2(-width + 0.5f, -height + 0.5f);
        screenMax = new float2(width - 0.5f, height - 0.5f);

        // Varsayilan mesh (kucuk quad)
        if (entityMesh == null)
        {
            entityMesh = CreateQuadMesh();
        }

        // Varsayilan material
        if (entityMaterial == null)
        {
            // URP icin varsayilan unlit material
            entityMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (entityMaterial.shader == null)
                entityMaterial = new Material(Shader.Find("Sprites/Default"));
            entityMaterial.color = Color.white;
        }

        InitCSV();
    }

    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.075f, -0.075f, 0),
            new Vector3( 0.075f, -0.075f, 0),
            new Vector3( 0.075f,  0.075f, 0),
            new Vector3(-0.075f,  0.075f, 0),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.RecalculateBounds();
        return mesh;
    }

    void InitCSV()
    {
        csvBuilder.Clear();
        csvBuilder.AppendLine(
            "EntityCount,AvgFrameTime_ms,MinFrameTime_ms,MaxFrameTime_ms," +
            "StdDev_ms,AvgFPS,MinFPS,MemoryUsed_MB,TotalFrames,Duration_s"
        );
    }

    void Update()
    {
        currentFrameTime = Time.unscaledDeltaTime * 1000f;
        currentFPS = 1f / Time.unscaledDeltaTime;

        if (isTesting)
        {
            testTimer += Time.unscaledDeltaTime;

            if (testTimer > 1f)
            {
                frameTimes.Add(currentFrameTime);
            }

            if (testTimer > testDuration / 2f && memoryDuringTest == 0)
            {
                memoryDuringTest = System.GC.GetTotalMemory(false);
            }

            if (testTimer >= testDuration + 1f)
            {
                EndCurrentTest();
            }

            statusText = $"Test: {entityCounts[currentTestIndex]:N0} entity | " +
                         $"FPS: {currentFPS:F0} | Frame: {currentFrameTime:F2}ms | " +
                         $"Kalan: {(testDuration + 1f - testTimer):F0}s";
        }
    }

    void SpawnEntities(int count)
    {
        DestroyAllEntities();

        var renderMeshArray = new RenderMeshArray(
            new Material[] { entityMaterial },
            new Mesh[] { entityMesh }
        );

        var renderMeshDescription = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off,
            receiveShadows: false
        );

        var random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

        for (int i = 0; i < count; i++)
        {
            Entity entity = entityManager.CreateEntity();

            // Transform
            float px = random.NextFloat(screenMin.x, screenMax.x);
            float py = random.NextFloat(screenMin.y, screenMax.y);
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = new float3(px, py, 0),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            // Velocity
            float angle = random.NextFloat(0f, math.PI * 2f);
            entityManager.AddComponentData(entity, new Velocity
            {
                Value = new float2(math.cos(angle), math.sin(angle)) * moveSpeed
            });

            // Screen bounds
            entityManager.AddComponentData(entity, new ScreenBounds
            {
                Min = screenMin,
                Max = screenMax
            });

            // Rendering
            RenderMeshUtility.AddComponents(entity, entityManager, renderMeshDescription, renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        spawnedCount = count;
    }

    void DestroyAllEntities()
    {
        var query = entityManager.CreateEntityQuery(typeof(Velocity));
        entityManager.DestroyEntity(query);
        spawnedCount = 0;
    }

    void StartTest(int countIndex)
    {
        currentTestIndex = countIndex;
        isTesting = true;
        testTimer = 0f;
        frameTimes.Clear();
        memoryDuringTest = 0;

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        memoryAtStart = System.GC.GetTotalMemory(true);

        SpawnEntities(entityCounts[countIndex]);
        Debug.Log($"[DOTS Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

        if (frameTimes.Count > 0)
        {
            float sum = 0f, min = float.MaxValue, max = float.MinValue;
            foreach (float ft in frameTimes)
            {
                sum += ft;
                if (ft < min) min = ft;
                if (ft > max) max = ft;
            }
            float avg = sum / frameTimes.Count;

            float variance = 0f;
            foreach (float ft in frameTimes)
            {
                float diff = ft - avg;
                variance += diff * diff;
            }
            float stdDev = Mathf.Sqrt(variance / frameTimes.Count);

            float avgFPS = 1000f / avg;
            float minFPS = 1000f / max;

            float memoryMB = (memoryDuringTest - memoryAtStart) / (1024f * 1024f);
            if (memoryMB < 0) memoryMB = memoryDuringTest / (1024f * 1024f);

            csvBuilder.AppendLine(
                $"{entityCounts[currentTestIndex]}," +
                $"{avg:F3},{min:F3},{max:F3},{stdDev:F3}," +
                $"{avgFPS:F1},{minFPS:F1}," +
                $"{memoryMB:F2}," +
                $"{frameTimes.Count},{testDuration:F0}"
            );

            statusText = $"Tamamlandi: {entityCounts[currentTestIndex]:N0} entity | " +
                         $"Ort: {avg:F2}ms ({avgFPS:F0} FPS) | StdDev: {stdDev:F2}ms | " +
                         $"Bellek: {memoryMB:F1} MB";

            Debug.Log($"[DOTS Benchmark] {statusText}");
        }

        if (allTestsMode && currentTestIndex < entityCounts.Length - 1)
        {
            StartTest(currentTestIndex + 1);
        }
        else
        {
            allTestsMode = false;
            DestroyAllEntities();
            SaveCSV();
        }
    }

    void SaveCSV()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"benchmark_unity_dots_{timestamp}.csv";
        string path = System.IO.Path.Combine(Application.dataPath, "..", "..", "..", "results", "benchmarks", filename);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllText(path, csvBuilder.ToString());
        Debug.Log($"[DOTS Benchmark] CSV kaydedildi: {path}");
        statusText += $"\nCSV: {filename}";
    }

    void OnGUI()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14 };
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        GUIStyle lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUIStyle smallLbl = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };

        GUI.Box(new Rect(10, 10, 340, 300), "DOD Benchmark — Unity DOTS", boxStyle);

        GUI.Label(new Rect(20, 40, 200, 25), "Entity Sayisi:", lblStyle);
        for (int i = 0; i < entityCounts.Length; i++)
        {
            string label = $"{entityCounts[i] / 1000}K";
            GUIStyle style = new GUIStyle(btnStyle);
            if (i == selectedCountIndex)
            {
                style.normal.textColor = Color.cyan;
                style.fontStyle = FontStyle.Bold;
            }
            if (GUI.Button(new Rect(20 + (i * 63), 65, 58, 30), label, style))
            {
                selectedCountIndex = i;
            }
        }

        if (!isTesting)
        {
            if (GUI.Button(new Rect(20, 105, 150, 35), "Tek Test", btnStyle))
            {
                InitCSV();
                StartTest(selectedCountIndex);
            }
            if (GUI.Button(new Rect(180, 105, 150, 35), "Tum Testler", btnStyle))
            {
                allTestsMode = true;
                InitCSV();
                StartTest(0);
            }
        }
        else
        {
            GUI.Label(new Rect(20, 110, 300, 25), ">>> Test calisiyor...", lblStyle);
        }

        GUI.Label(new Rect(20, 150, 300, 25), $"FPS: {currentFPS:F0}", lblStyle);
        GUI.Label(new Rect(20, 175, 300, 25), $"Frame Time: {currentFrameTime:F2} ms", lblStyle);
        GUI.Label(new Rect(20, 200, 300, 25), $"Aktif Entity: {spawnedCount:N0}", lblStyle);
        GUI.Label(new Rect(20, 235, 310, 60), statusText, smallLbl);
    }
}
