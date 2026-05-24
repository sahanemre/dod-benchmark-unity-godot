using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

/// <summary>
/// Benchmark yoneticisi.
/// Entity olusturur, performans olcer, CSV'ye yazar.
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

    // Dahili degiskenler
    private List<GameObject> activeEntities = new List<GameObject>();
    private int currentTestIndex = -1;
    private bool isTesting = false;
    private float testTimer = 0f;

    // Performans olcum
    private List<float> frameTimes = new List<float>();
    private StringBuilder csvBuilder = new StringBuilder();

    // Ekran sinirlari
    private Vector2 screenMin;
    private Vector2 screenMax;

    // UI
    private int selectedCountIndex = 0;
    private bool allTestsMode = false;
    private string statusText = "Hazir. Test secin ve baslatin.";
    private float currentFPS = 0f;
    private float currentFrameTime = 0f;

    // Bellek olcum
    private long memoryAtStart = 0;
    private long memoryDuringTest = 0;

    void Start()
    {
        // Ekran sinirlarini hesapla
        Camera cam = Camera.main;
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        screenMin = new Vector2(-width + 0.5f, -height + 0.5f);
        screenMax = new Vector2(width - 0.5f, height - 0.5f);

        // Varsayilan sprite olustur (4x4 beyaz kare)
        if (entitySprite == null)
        {
            Texture2D tex = new Texture2D(4, 4);
            Color[] colors = new Color[16];
            for (int i = 0; i < 16; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            entitySprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        InitCSV();
    }

    void InitCSV()
    {
        csvBuilder.Clear();
        csvBuilder.AppendLine(
            "EntityCount," +
            "AvgFrameTime_ms," +
            "MinFrameTime_ms," +
            "MaxFrameTime_ms," +
            "StdDev_ms," +
            "AvgFPS," +
            "MinFPS," +
            "MemoryUsed_MB," +
            "TotalFrames," +
            "Duration_s"
        );
    }

    void Update()
    {
        currentFrameTime = Time.unscaledDeltaTime * 1000f;
        currentFPS = 1f / Time.unscaledDeltaTime;

        if (isTesting)
        {
            testTimer += Time.unscaledDeltaTime;

            // Ilk 1 saniye warmup — veriye dahil etme
            if (testTimer > 1f)
            {
                frameTimes.Add(currentFrameTime);
            }

            // Bellek olcumu (test ortasinda)
            if (testTimer > testDuration / 2f && memoryDuringTest == 0)
            {
                memoryDuringTest = System.GC.GetTotalMemory(false);
            }

            // Test suresi doldu
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
        ClearEntities();

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Entity_{i}");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = entitySprite;
            sr.color = new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f)
            );
            go.transform.localScale = Vector3.one * 0.15f;

            EntityMover mover = go.AddComponent<EntityMover>();
            mover.Initialize(moveSpeed, screenMin, screenMax);

            activeEntities.Add(go);
        }
    }

    void ClearEntities()
    {
        foreach (var go in activeEntities)
        {
            Destroy(go);
        }
        activeEntities.Clear();
    }

    void StartTest(int countIndex)
    {
        currentTestIndex = countIndex;
        isTesting = true;
        testTimer = 0f;
        frameTimes.Clear();
        memoryDuringTest = 0;

        // GC temizle
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        memoryAtStart = System.GC.GetTotalMemory(true);

        SpawnEntities(entityCounts[countIndex]);

        Debug.Log($"[Benchmark] Test basladi: {entityCounts[countIndex]:N0} entity");
    }

    void EndCurrentTest()
    {
        isTesting = false;

        if (frameTimes.Count > 0)
        {
            // Istatistik hesapla
            float sum = 0f, min = float.MaxValue, max = float.MinValue;
            foreach (float ft in frameTimes)
            {
                sum += ft;
                if (ft < min) min = ft;
                if (ft > max) max = ft;
            }
            float avg = sum / frameTimes.Count;

            // Standart sapma
            float variance = 0f;
            foreach (float ft in frameTimes)
            {
                float diff = ft - avg;
                variance += diff * diff;
            }
            float stdDev = Mathf.Sqrt(variance / frameTimes.Count);

            float avgFPS = 1000f / avg;
            float minFPS = 1000f / max;

            // Bellek (MB)
            float memoryMB = (memoryDuringTest - memoryAtStart) / (1024f * 1024f);
            if (memoryMB < 0) memoryMB = memoryDuringTest / (1024f * 1024f);

            // CSV satirı (InvariantCulture: ondalik ayirici nokta olsun)
            var ci = CultureInfo.InvariantCulture;
            csvBuilder.AppendLine(string.Format(ci,
                "{0},{1:F3},{2:F3},{3:F3},{4:F3},{5:F1},{6:F1},{7:F2},{8},{9:F0}",
                entityCounts[currentTestIndex],
                avg, min, max, stdDev,
                avgFPS, minFPS,
                memoryMB,
                frameTimes.Count, testDuration
            ));

            statusText = $"Tamamlandi: {entityCounts[currentTestIndex]:N0} entity | " +
                         $"Ort: {avg:F2}ms ({avgFPS:F0} FPS) | StdDev: {stdDev:F2}ms | " +
                         $"Bellek: {memoryMB:F1} MB";

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
            ClearEntities();
            SaveCSV();
        }
    }

    void SaveCSV()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"benchmark_unity_oop_{timestamp}.csv";

        // Projenin ust klasorundeki results/benchmarks/ klasorune kaydet
        string path = Path.Combine(Application.dataPath, "..", "..", "..", "results", "benchmarks", filename);

        // Alternatif: Desktop'a kaydet (yukardaki yol calismazsa)
        // string path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), filename);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, csvBuilder.ToString());

        Debug.Log($"[Benchmark] CSV kaydedildi: {path}");
        statusText += $"\nCSV: {filename}";
    }

    /// <summary>
    /// Ekranin sol ustunde benchmark kontrol paneli.
    /// Entity sayisi sec, test baslat, anlik metrikleri gor.
    /// </summary>
    void OnGUI()
    {
        // Stiller
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14 };
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        GUIStyle lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUIStyle smallLbl = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };

        // Panel
        GUI.Box(new Rect(10, 10, 340, 300), "DOD Benchmark — Unity OOP", boxStyle);

        // Entity sayisi secimi
        GUI.Label(new Rect(20, 40, 200, 25), "Entity Sayisi:", lblStyle);
        for (int i = 0; i < entityCounts.Length; i++)
        {
            string label = $"{entityCounts[i] / 1000}K";
            GUIStyle style = new GUIStyle(btnStyle);
            if (i == selectedCountIndex)
            {
                style.normal.textColor = Color.green;
                style.fontStyle = FontStyle.Bold;
            }
            if (GUI.Button(new Rect(20 + (i * 63), 65, 58, 30), label, style))
            {
                selectedCountIndex = i;
            }
        }

        // Butonlar
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

        // Anlik metrikler
        GUI.Label(new Rect(20, 150, 300, 25), $"FPS: {currentFPS:F0}", lblStyle);
        GUI.Label(new Rect(20, 175, 300, 25), $"Frame Time: {currentFrameTime:F2} ms", lblStyle);
        GUI.Label(new Rect(20, 200, 300, 25), $"Aktif Entity: {activeEntities.Count:N0}", lblStyle);

        // Durum
        GUI.Label(new Rect(20, 235, 310, 60), statusText, smallLbl);
    }
}