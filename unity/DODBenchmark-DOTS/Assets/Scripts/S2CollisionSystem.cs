using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// S2 carpisma sistemi (Unity DOTS). Burst ile derlenir, ana is parcaciginda
/// tek dongu olarak calisir (S1 MovementSystem gibi; Job System yok).
///
/// Her kare MovementSystem'den SONRA:
///   1. Toplama: chunk'lardaki LocalTransform/Velocity, S2Index sirasina gore
///      kalici SoA dizilerine (px, py, vx, vy) kopyalanir.
///   2. S2Collision.Collide: grid (indeks), komsu hucre testi, elastik tepki.
///   3. Geri yazma: yeni hizlar Velocity bilesenlerine.
///   4. S2Stats singleton'ina cift sayilari yazilir.
/// Toplama/geri yazma maliyeti DOTS olcumunun parcasidir.
///
/// [DisableAutoCreation]: yalnizca S2DotsManager olusturur; S1 dunyasi degismez.
/// </summary>
[BurstCompile]
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MovementSystem))]
public partial struct S2CollisionSystem : ISystem
{
    private NativeArray<float> px, py, vx, vy;
    private NativeArray<int> cellOf, sorted, cellStart, cellFill;
    private int capacity;
    private int cols, rows;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<S2Stats>();
        cols = (int)math.ceil((S2Params.MaxX - S2Params.MinX) / S2Params.Cell);
        rows = (int)math.ceil((S2Params.MaxY - S2Params.MinY) / S2Params.Cell);
        cellStart = new NativeArray<int>(cols * rows + 1, Allocator.Persistent);
        cellFill = new NativeArray<int>(cols * rows, Allocator.Persistent);
        capacity = 0;
    }

    public void OnDestroy(ref SystemState state)
    {
        if (cellStart.IsCreated) cellStart.Dispose();
        if (cellFill.IsCreated) cellFill.Dispose();
        DisposeEntityArrays();
    }

    private void DisposeEntityArrays()
    {
        if (px.IsCreated) px.Dispose();
        if (py.IsCreated) py.Dispose();
        if (vx.IsCreated) vx.Dispose();
        if (vy.IsCreated) vy.Dispose();
        if (cellOf.IsCreated) cellOf.Dispose();
        if (sorted.IsCreated) sorted.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int n = SystemAPI.QueryBuilder().WithAll<S2Index>().Build().CalculateEntityCount();
        if (n > capacity)
        {
            DisposeEntityArrays();
            px = new NativeArray<float>(n, Allocator.Persistent);
            py = new NativeArray<float>(n, Allocator.Persistent);
            vx = new NativeArray<float>(n, Allocator.Persistent);
            vy = new NativeArray<float>(n, Allocator.Persistent);
            cellOf = new NativeArray<int>(n, Allocator.Persistent);
            sorted = new NativeArray<int>(n, Allocator.Persistent);
            capacity = n;
        }

        // 1. Toplama (spawn sirasina gore SoA)
        foreach (var (transform, velocity, index) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<Velocity>, RefRO<S2Index>>())
        {
            int i = index.ValueRO.Value;
            px[i] = transform.ValueRO.Position.x;
            py[i] = transform.ValueRO.Position.y;
            vx[i] = velocity.ValueRO.Value.x;
            vy[i] = velocity.ValueRO.Value.y;
        }

        // 2. Grid + narrow-phase + tepki
        int pairs = S2Collision.Collide(n, cols, rows, px, py, vx, vy,
            cellOf, sorted, cellStart, cellFill, out int responses);

        // 3. Geri yazma
        foreach (var (velocity, index) in SystemAPI.Query<RefRW<Velocity>, RefRO<S2Index>>())
        {
            int i = index.ValueRO.Value;
            velocity.ValueRW.Value = new float2(vx[i], vy[i]);
        }

        // 4. Sonuc
        var stats = SystemAPI.GetSingletonRW<S2Stats>();
        stats.ValueRW.Pairs = pairs;
        stats.ValueRW.Responses = responses;
        stats.ValueRW.Steps += 1;
    }
}
