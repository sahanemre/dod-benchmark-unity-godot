/// <summary>
/// S2 ortak RNG: murmur3 fmix32 ile karistirilmis seed + xorshift32.
/// Godot (S2Rng.gd), C++ (MovementWorld.spawn_s2) ve referans (s2_reference.c)
/// ile birebir ayni sayilari uretir. Seed = 1000 + run_index.
/// </summary>
public struct S2Rng
{
    private uint state;

    public S2Rng(long seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= h >> 16;
            h *= 0x85ebca6bu;
            h ^= h >> 13;
            h *= 0xc2b2ae35u;
            h ^= h >> 16;
            state = h != 0u ? h : 0x9e3779b9u;
        }
    }

    /// <summary>[0, 1), 24 bit.</summary>
    public float NextFloat()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0x00FFFFFFu) / 16777216f;
    }

    public float Range(float lo, float hi) => lo + (hi - lo) * NextFloat();

    /// <summary>
    /// Bir entity'nin baslangic durumu. Entity basina 6 cekilis, bu sirayla:
    /// x, y, aci, r, g, b (dort konfigurasyonda ayni sira). Hiz,
    /// (float)cos((double)aci) * hiz (dort dilde ayni sonuc icin double'da).
    /// </summary>
    public void NextEntity(out float x, out float y, out float vx, out float vy,
        out float r, out float g, out float b)
    {
        x = Range(S2Params.MinX, S2Params.MaxX);
        y = Range(S2Params.MinY, S2Params.MaxY);
        float angle = Range(0f, S2Params.TwoPi);
        vx = (float)System.Math.Cos(angle) * S2Params.Speed;
        vy = (float)System.Math.Sin(angle) * S2Params.Speed;
        r = Range(0.3f, 1f);
        g = Range(0.3f, 1f);
        b = Range(0.3f, 1f);
    }
}
