using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Movement System — tum entity'leri hareket ettirir.
/// 
/// DOD yaklasimi:
/// - Bu system sadece LocalTransform, Velocity, ScreenBounds component'larina sahip
///   entity'leri bulur ve isler.
/// - Burst Compiler ile derlenir → SIMD optimizasyonu otomatik.
/// - Veriler bellekte ardisik (SoA) tutulur → cache-friendly.
/// 
/// OOP farki:
/// - OOP'ta her GameObject'in Update()'i tek tek cagrilirdi (virtual method call).
/// - Burada tek bir for dongusu tum entity'leri isler, veri ardisik bellekte.
/// </summary>
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        // Tum entity'leri tek dongude isle
        // RefRW = read-write, RefRO = read-only
        foreach (var (transform, velocity, bounds) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<Velocity>, RefRO<ScreenBounds>>())
        {
            // Pozisyonu guncelle
            float3 pos = transform.ValueRW.Position;
            float2 vel = velocity.ValueRW.Value;

            pos.x += vel.x * dt;
            pos.y += vel.y * dt;

            // Duvar kontrolu (bounce)
            if (pos.x < bounds.ValueRO.Min.x || pos.x > bounds.ValueRO.Max.x)
            {
                vel.x = -vel.x;
                pos.x = math.clamp(pos.x, bounds.ValueRO.Min.x, bounds.ValueRO.Max.x);
            }
            if (pos.y < bounds.ValueRO.Min.y || pos.y > bounds.ValueRO.Max.y)
            {
                vel.y = -vel.y;
                pos.y = math.clamp(pos.y, bounds.ValueRO.Min.y, bounds.ValueRO.Max.y);
            }

            // Degerleri geri yaz
            transform.ValueRW.Position = pos;
            velocity.ValueRW.Value = vel;
        }
    }
}
