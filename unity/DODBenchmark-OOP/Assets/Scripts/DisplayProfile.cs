using UnityEngine;

/// <summary>
/// Profile gore pencere modu ve cozunurluk.
/// Tek sorumluluk: goruntu kosullarini uygulamak ve dogrulamak.
///
/// published: makaledeki Unity kosulu = borderless fullscreen ("FullScreenWindow"),
///            ekranin yerel cozunurlugu (proje ayari).
/// parity   : exclusive fullscreen, 1920x1080 (dort konfigurasyonda ayni).
///
/// Calistirma scripti ayni modu komut satirindan da verir (-window-mode,
/// -screen-width/height); boylece player ilk frame'den dogru modda acilir.
/// Burada ayrica uygulanir ki elle acilista da profil gecerli olsun.
/// 16:9'da kamera en-boy orani degismedigi icin dunya sinirlari etkilenmez.
/// </summary>
public static class DisplayProfile
{
    public const int ParityWidth = 1920;
    public const int ParityHeight = 1080;

    public static void Expected(bool parity, out FullScreenMode mode, out int width, out int height)
    {
        if (parity)
        {
            mode = FullScreenMode.ExclusiveFullScreen;
            width = ParityWidth;
            height = ParityHeight;
        }
        else
        {
            mode = FullScreenMode.FullScreenWindow;
            width = Display.main.systemWidth;
            height = Display.main.systemHeight;
        }
    }

    /// <summary>Gerekiyorsa modu/cozunurlugu degistirir (zaten dogruysa dokunmaz).</summary>
    public static void Apply(bool parity)
    {
        Expected(parity, out var mode, out int w, out int h);
        if (Screen.fullScreenMode != mode || Screen.width != w || Screen.height != h)
            Screen.SetResolution(w, h, mode);
    }

    public static bool Matches(bool parity)
    {
        Expected(parity, out var mode, out int w, out int h);
        return Screen.fullScreenMode == mode && Screen.width == w && Screen.height == h;
    }

    public static string ModeName(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "exclusive";
            case FullScreenMode.FullScreenWindow: return "borderless";
            case FullScreenMode.MaximizedWindow: return "maximized";
            case FullScreenMode.Windowed: return "windowed";
            default: return "unknown";
        }
    }

    public static string Describe(bool parity)
    {
        Expected(parity, out var mode, out int w, out int h);
        return $"{ModeName(mode)} {w}x{h}";
    }
}
