using Unity.Entities;

/// <summary>S2: entity'nin spawn sirasi (grid indeks tutar; cift sirasi j &gt; i).</summary>
public struct S2Index : IComponentData
{
    public int Value;
}

/// <summary>
/// S2 singleton: son carpisma adiminin sonuclari. Yonetici (MonoBehaviour)
/// LateUpdate'te okur. Bu bilesen olusturulmadan S2CollisionSystem calismaz.
/// </summary>
public struct S2Stats : IComponentData
{
    public int Pairs;      // kesisen cift sayisi
    public int Responses;  // tepki verilen cift sayisi
    public int Steps;      // yapilan carpisma adimi (kare hizalamasi icin)
}
