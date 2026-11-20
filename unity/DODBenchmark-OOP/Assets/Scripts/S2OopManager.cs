using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 (carpisma) yoneticisi — Unity OOP. S2Bootstrap tarafindan, --scenario=S2
/// verildiginde eklenir (S1 BenchmarkManager devre disi kalir; S1 koduna dokunulmaz).
///
/// Her kare, bu sirayla:
///   1. Hareket: her S2OopEntity kendi Update'inde (S1 ile ayni mantik).
///   2-5. LateUpdate'te (tum Update'lerden SONRA): S2OopGrid ile uniform grid,
///        komsu hucre testi, elastik tepki, kesisen cift sayisi.
///
/// Modlar:
///   --validate : sabit dt (Time.captureDeltaTime = 1/60) ile --frames kare;
///                kare basina cift sayilari dosyaya (N &lt;= 2000'de O(N^2) kontrol).
///   aksi halde : S1 ile ayni olcum (bosta bekleme, 3 s warmup + 10 s olcum,
///                parity); CSV'ye collision_pairs_mean eklenir.
/// </summary>
public class S2OopManager : MonoBehaviour
{
    public int[] entityCounts = { 1000, 5000, 10000, 50000, 100000 };
    public float testDuration = 10f;

    private const string UnlitSpriteShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    private S1RunSession session;
    private readonly FrameStatistics stats = new FrameStatistics();
    private readonly S2OopGrid grid = new S2OopGrid();
    private S2OopEntity[] entities;
    private Sprite sprite;
    private Material spriteMaterial;
    private int lastSteps;

    private bool isTesting;
    private float testTimer;
    private float currentFrameTime;
    private double pairsSum;
    private int pairsFrames;

    private bool validating;
    private bool validateSpawnPending;
    private bool bruteforce;
    private int frame;
    private readonly List<string> lines = new List<string>();

    void Start()
    {
        session = new S1RunSession("unity", "oop", "[S2 OOP]", System.Environment.GetCommandLineArgs());
        Application.runInBackground = true;
        DisplayProfile.Apply(session.Parity);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        // Ortak simulasyon uzayi (1280x720) ekrana: S1 parity ile ayni gorunum
        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = S2Params.CameraOrthoSize;
        cam.transform.position = new Vector3(S2Params.CameraX, S2Params.CameraY, -10f);

        sprite = CreateDefaultSprite();
        Shader unlit = Shader.Find(UnlitSpriteShader);
        if (unlit != null) spriteMaterial = new Material(unlit);

        if (!session.Setup(entityCounts)) return;
        RunArgs args = session.Args;
        if (args.Scenario != "S2" || !args.Unattended || !args.IsParity)
        {
            session.Fail(S1RunSession.ExitBadArgs, "S2 yalnizca --scenario=S2, --profile=parity ve --n ile calisir.");
            return;
        }
        if (spriteMaterial == null)
        {
            session.Fail(S1RunSession.ExitShaderMissing, $"'{UnlitSpriteShader}' build'de yok.");
            return;
        }

        if (args.Validate)
        {
            validating = true;
            bruteforce = args.N <= S2Params.ValidateBruteforceMaxN;
            // captureDeltaTime bu karenin deltaTime'ini degistirmez (kare basinda
            // hesaplandi); spawn bir sonraki karede, ilk adim kesin 1/60 ile olsun.
            Time.captureDeltaTime = S2Params.FixedDt;
            validateSpawnPending = true;
            Debug.Log($"[S2 OOP] Dogrulama: N={args.N}, seed={args.Seed}, {args.Frames} kare, dt=1/60" +
                      (bruteforce ? ", O(N^2) kontrol" : ""));
        }
    }

    static Sprite CreateDefaultSprite()
    {
        // S1 ile ayni 4x4 beyaz doku, 1 birim; olcek = 2r
        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    /// <summary>Ortak RNG ile spawn (entity basina 6 cekilis: x, y, aci, r, g, b).</summary>
    void Spawn()
    {
        int n = session.Args.N;
        var rng = new S2Rng(session.Args.Seed);
        var min = new Vector2(S2Params.MinX, S2Params.MinY);
        var max = new Vector2(S2Params.MaxX, S2Params.MaxY);
        entities = new S2OopEntity[n];
        for (int i = 0; i < n; i++)
        {
            rng.NextEntity(out float x, out float y, out float vx, out float vy,
                out float r, out float g, out float b);
            var go = new GameObject($"S2Entity_{i}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = spriteMaterial;
            sr.sprite = sprite;
            sr.color = new Color(r, g, b);
            go.transform.localScale = Vector3.one * (2f * S2Params.Radius);
            var e = go.AddComponent<S2OopEntity>();
            e.Setup(i, new Vector2(x, y), new Vector2(vx, vy), min, max);
            entities[i] = e;
        }
        lastSteps = 0;
    }

    void Update()
    {
        if (validating)
        {
            if (validateSpawnPending)
            {
                validateSpawnPending = false;
                Spawn();
            }
            return;
        }
        currentFrameTime = Time.unscaledDeltaTime * 1000f;

        if (session.WaitingToStart)
        {
            if (session.TickIdle(Time.unscaledDeltaTime)) StartTest();
            return;
        }
        if (!isTesting) return;

        testTimer += Time.unscaledDeltaTime;
        session.AfterTimerAdvance(testTimer, Time.unscaledDeltaTime);

        // Ilk 3 saniye warmup — veriye dahil etme
        if (testTimer > 3f)
            stats.AddSample(currentFrameTime);

        if (testTimer >= testDuration + 3f)
        {
            isTesting = false;
            double mean = pairsFrames > 0 ? pairsSum / pairsFrames : 0.0;
            session.Finish(stats, testDuration, "na", "na", mean);
        }
    }

    void StartTest()
    {
        isTesting = true;
        testTimer = 0f;
        stats.Reset();
        pairsSum = 0;
        pairsFrames = 0;
        session.OnTestStarted();
        Spawn();
        Debug.Log($"[S2 OOP] Test basladi: {session.Args.N:N0} entity, seed={session.Args.Seed}");
    }

    /// <summary>Tum entity Update'lerinden sonra: carpisma adimi.</summary>
    void LateUpdate()
    {
        if (entities == null || entities.Length == 0) return;
        // Entity'ler bu karede hareket etmediyse (ornegin olusturuldugu kare) atla
        if (entities[0].Steps == lastSteps) return;
        lastSteps = entities[0].Steps;

        int pairs = grid.Collide(entities);

        if (validating)
        {
            frame++;
            if (bruteforce)
                lines.Add($"{frame},{pairs},{grid.LastResponses},{S2OopGrid.CountBruteforce(entities)}");
            else
                lines.Add($"{frame},{pairs},{grid.LastResponses}");
            if (frame >= session.Args.Frames)
            {
                validating = false;
                int code = 0;
                try { S2ValidationWriter.Write(session.Args.OutPath, lines, bruteforce); }
                catch (System.Exception ex) { Debug.LogError($"[S2 OOP] Yazilamadi: {ex}"); code = S1RunSession.ExitWriteFailed; }
                Debug.Log($"[S2 OOP] Dogrulama bitti: {frame} kare -> {session.Args.OutPath}");
                Application.Quit(code);
            }
            return;
        }

        if (isTesting && testTimer > 3f)
        {
            pairsSum += pairs;
            pairsFrames++;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        session?.OnFocusChanged(hasFocus, isTesting);
    }
}
