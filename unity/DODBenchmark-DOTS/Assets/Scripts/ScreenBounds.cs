using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Ekran sinirlari component'i.
/// Her entity'de ayni deger — SharedComponentData da olabilirdi
/// ama basitlik icin normal component kullaniyoruz.
/// </summary>
public struct ScreenBounds : IComponentData
{
    public float2 Min;
    public float2 Max;
}
