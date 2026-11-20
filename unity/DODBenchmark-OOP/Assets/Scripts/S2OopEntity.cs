using UnityEngine;

/// <summary>
/// S2 OOP entity'si: kendi pozisyonunu ve hizini KENDI nesnesinde (alanlarda)
/// tutar ve kendi Update'inde hareket eder (S1 EntityMover ile ayni mantik).
/// transform.position yalnizca cizim icin, karede bir kez yazilir.
/// Carpisma testi (S2OopGrid) bu alanlari nesne referansi uzerinden okur/yazar.
/// </summary>
public class S2OopEntity : MonoBehaviour
{
    public Vector2 Pos;
    public Vector2 Vel;
    public int Index;   // spawn sirasi (cift sirasi j > i icin)
    public int Cell;    // bu karedeki grid hucresi
    public int Steps;   // yapilan hareket adimi (kare hizalamasi icin)

    private Vector2 screenMin;
    private Vector2 screenMax;

    public void Setup(int index, Vector2 pos, Vector2 vel, Vector2 min, Vector2 max)
    {
        Index = index;
        Pos = pos;
        Vel = vel;
        screenMin = min;
        screenMax = max;
        transform.position = new Vector3(Pos.x, Pos.y, 0f);
    }

    void Update()
    {
        // S1 EntityMover ile ayni hareket ve sekme (alanlar uzerinde)
        Pos.x += Vel.x * Time.deltaTime;
        Pos.y += Vel.y * Time.deltaTime;

        if (Pos.x < screenMin.x || Pos.x > screenMax.x)
        {
            Vel.x = -Vel.x;
            Pos.x = Mathf.Clamp(Pos.x, screenMin.x, screenMax.x);
        }
        if (Pos.y < screenMin.y || Pos.y > screenMax.y)
        {
            Vel.y = -Vel.y;
            Pos.y = Mathf.Clamp(Pos.y, screenMin.y, screenMax.y);
        }

        transform.position = new Vector3(Pos.x, Pos.y, 0f);
        Steps++;
    }
}
