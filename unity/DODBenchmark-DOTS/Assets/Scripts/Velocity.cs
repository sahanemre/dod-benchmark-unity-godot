using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Velocity component — sadece veri, davranis yok.
/// DOD yaklasimi: veri ve davranis birbirinden ayrilir.
/// Her entity'nin hareket yonu ve hizi bu component'ta tutulur.
/// </summary>
public struct Velocity : IComponentData
{
    public float2 Value;
}
