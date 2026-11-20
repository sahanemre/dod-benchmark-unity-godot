using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// S2 (carpisma) yoneticisi — Unity DOTS. S2Bootstrap tarafindan, --scenario=S2
/// verildiginde eklenir (S1 DOTSBenchmarkSpawner devre disi kalir; S1 koduna
/// dokunulmaz).
///
/// Her kare (SimulationSystemGroup icinde, bu sirayla):
///   1. Hareket: S1 MovementSystem (degismedi).
///   2-5. S2CollisionSystem (Burst, ana is parcacigi): grid (indeks), komsu
///        hucre testi, elastik tepki, cift sayilari S2Stats singleton'ina.
/// Yonetici LateUpdate'te S2Stats'i okur.
///
/// Parity kosullari S1 ile ayni: exclusive 1920x1080, D3D12, lean pipeline,
/// URP/Unlit + entity basina renk.
/// </summary>
public class S2DotsManager : MonoBehaviour
{
    public int[] entityCounts = { 1000, 5000, 10000, 50000, 100000 };
    public float testDuration = 10f;

    private const string ParityShader = "Universal Render Pipeline/Unlit";

    private S1RunSession session;
    private readonly FrameStatistics stats = new FrameStatistics();
    private EntityManager entityManager;
    private Entity statsEntity = Entity.Null;
    private Mesh mesh;
    private Material material;
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
        session = new S1RunSession("unity", "dod", "[S2 DOTS]", System.Environment.GetCommandLineArgs());
        Application.runInBackground = true;
        DisplayProfile.Apply(session.Parity);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = S2Params.CameraOrthoSize;
        cam.transform.position = new Vector3(S2Params.CameraX, S2Params.CameraY, -10f);

        if (!session.Setup(entityCounts)) return;
        RunArgs args = session.Args;
        if (args.Scenario != "S2" || !args.Unattended || !args.IsParity)
        {
            session.Fail(S1RunSession.ExitBadArgs, "S2 yalnizca --scenario=S2, --profile=parity ve --n ile calisir.");
            return;
        }

        if (args.DotsPipeline == "lean")
            ApplyLeanPipeline(cam);

        Shader shader = Shader.Find(ParityShader);
        if (shader == null)
        {
            session.Fail(S1RunSession.ExitShaderMissing, $"'{ParityShader}' build'de yok.");
            return;
        }
        material = new Material(shader) { color = Color.white };
        mesh = CreateQuadMesh(S2Params.Radius);

        var world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;
        var system = world.CreateSystem<S2CollisionSystem>();
        world.GetExistingSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(system);

        Debug.Log($"[S2 DOTS] entity shader: {material.shader.name} | pipeline: {args.DotsPipeline}");

        if (args.Validate)
        {
            validating = true;
            bruteforce = args.N <= S2Params.ValidateBruteforceMaxN;
            // captureDeltaTime bu karenin deltaTime'ini degistirmez (kare basinda
            // hesaplandi); spawn bir sonraki karede, ilk adim kesin 1/60 ile olsun.
            Time.captureDeltaTime = S2Params.FixedDt;
            validateSpawnPending = true;
            Debug.Log($"[S2 DOTS] Dogrulama: N={args.N}, seed={args.Seed}, {args.Frames} kare, dt=1/60" +
                      (bruteforce ? ", O(N^2) kontrol" : ""));
        }
    }

    /// <summary>S1 DOTSBenchmarkSpawner.ApplyLeanPipeline ile ayni (S1 dosyasina dokunmamak icin kopya).</summary>
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

    /// <summary>Kenari 2r olan kare mesh (S2 dunya biriminde).</summary>
    static Mesh CreateQuadMesh(float r)
    {
        var m = new Mesh();
        m.vertices = new[]
        {
            new Vector3(-r, -r, 0), new Vector3(r, -r, 0), new Vector3(r, r, 0), new Vector3(-r, r, 0),
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Ortak RNG ile spawn (entity basina 6 cekilis: x, y, aci, r, g, b).</summary>
    void Spawn()
    {
        int n = session.Args.N;
        var rng = new S2Rng(session.Args.Seed);
        var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
        var renderMeshDescription = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
        var bounds = new ScreenBounds
        {
            Min = new float2(S2Params.MinX, S2Params.MinY),
            Max = new float2(S2Params.MaxX, S2Params.MaxY),
        };

        for (int i = 0; i < n; i++)
        {
            rng.NextEntity(out float x, out float y, out float vx, out float vy,
                out float r, out float g, out float b);
            Entity e = entityManager.CreateEntity();
            entityManager.AddComponentData(e, new LocalTransform
            {
                Position = new float3(x, y, 0f),
                Rotation = quaternion.identity,
                Scale = 1f,
            });
            entityManager.AddComponentData(e, new Velocity { Value = new float2(vx, vy) });
            entityManager.AddComponentData(e, bounds);
            entityManager.AddComponentData(e, new S2Index { Value = i });
            RenderMeshUtility.AddComponents(e, entityManager, renderMeshDescription, renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            Color c = new Color(r, g, b).linear;
            entityManager.AddComponentData(e, new URPMaterialPropertyBaseColor { Value = new float4(c.r, c.g, c.b, 1f) });
        }

        // S2Stats singleton'i spawn ile olusur: S2CollisionSystem bundan sonra calisir
        statsEntity = entityManager.CreateEntity(typeof(S2Stats));
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
            session.Finish(stats, testDuration, "on", session.Args.DotsPipeline, mean);
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
        Debug.Log($"[S2 DOTS] Test basladi: {session.Args.N:N0} entity, seed={session.Args.Seed}");
    }

    /// <summary>SimulationSystemGroup (Update fazi) bittikten sonra: S2Stats'i oku.</summary>
    void LateUpdate()
    {
        if (statsEntity == Entity.Null) return;
        S2Stats s = entityManager.GetComponentData<S2Stats>(statsEntity);
        if (s.Steps == lastSteps) return;   // bu karede carpisma adimi olmadi
        lastSteps = s.Steps;

        if (validating)
        {
            frame++;
            if (bruteforce)
                lines.Add($"{frame},{s.Pairs},{s.Responses},{CountBruteforce()}");
            else
                lines.Add($"{frame},{s.Pairs},{s.Responses}");
            if (frame >= session.Args.Frames)
            {
                validating = false;
                int code = 0;
                try { S2ValidationWriter.Write(session.Args.OutPath, lines, bruteforce); }
                catch (System.Exception ex) { Debug.LogError($"[S2 DOTS] Yazilamadi: {ex}"); code = S1RunSession.ExitWriteFailed; }
                Debug.Log($"[S2 DOTS] Dogrulama bitti: {frame} kare -> {session.Args.OutPath}");
                Application.Quit(code);
            }
            return;
        }

        if (isTesting && testTimer > 3f)
        {
            pairsSum += s.Pairs;
            pairsFrames++;
        }
    }

    /// <summary>Dogrulama: O(N^2) kesisen cift sayisi (carpisma pozisyonlari degistirmez).</summary>
    int CountBruteforce()
    {
        var query = entityManager.CreateEntityQuery(typeof(LocalTransform), typeof(S2Index));
        var t = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        int pairs = 0;
        for (int i = 0; i < t.Length; i++)
            for (int j = i + 1; j < t.Length; j++)
            {
                float dx = t[j].Position.x - t[i].Position.x;
                float dy = t[j].Position.y - t[i].Position.y;
                if (dx * dx + dy * dy < S2Params.D2Max) pairs++;
            }
        t.Dispose();
        return pairs;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        session?.OnFocusChanged(hasFocus, isTesting);
    }
}
