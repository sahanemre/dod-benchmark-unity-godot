/// <summary>
/// S2 (carpisma) ortak parametreleri. Dort konfigurasyonda AYNI sayilar
/// (Godot'ta S2Params.gd, referans: tools/s2_reference/s2_reference.c).
///
/// Ortak simulasyon uzayi = Godot'un mantiksal 1280x720 dunyasi. Unity'de S2
/// kamerasi bu uzayi gosterir (orthographic size 360, merkez 640,360); ekranda
/// S1 parity ile ayni boyut (entity 6,75 birim = 10,125 px @1080p).
/// Burst kodundan da kullanilir: yalnizca sabitler.
/// </summary>
public static class S2Params
{
    public const float MinX = 10f;
    public const float MinY = 10f;
    public const float MaxX = 1270f;
    public const float MaxY = 710f;
    public const float Radius = 3.375f;     // S1 parity entity'si 6,75 -> r = 3,375
    public const float Cell = 6.75f;        // hucre = 2r
    public const float D2Max = 45.5625f;    // (2r)^2, ikilik tabanda tam
    public const float Speed = 300f;
    public const float TwoPi = 6.28318530718f;
    public const float FixedDt = 1f / 60f;  // dogrulama modu
    public const int ValidateBruteforceMaxN = 2000;

    public const float CameraOrthoSize = 360f;
    public const float CameraX = 640f;
    public const float CameraY = 360f;
}
