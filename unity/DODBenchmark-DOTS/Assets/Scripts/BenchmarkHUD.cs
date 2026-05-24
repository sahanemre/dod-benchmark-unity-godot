using UnityEngine;

/// <summary>
/// Benchmark kontrol paneli (OnGUI cizimi).
/// Tek sorumluluk: kullanici arayuzu. Mantik tasimaz; gosterilecek durum
/// disaridan set edilir, buton aksiyonlari callback ile disari bildirilir.
/// </summary>
public class BenchmarkHUD
{
    public string Title = "DOD Benchmark";
    public Color HighlightColor = Color.cyan;

    // Her frame orkestrator tarafindan guncellenen durum
    public int[] EntityCounts = new int[0];
    public int SelectedIndex;
    public bool IsTesting;
    public float Fps;
    public float FrameTimeMs;
    public int ActiveCount;
    public string StatusText = "";

    // Buton aksiyonlari
    public System.Action<int> OnSelect;
    public System.Action OnStartSingle;
    public System.Action OnStartAll;

    public void Draw()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14 };
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        GUIStyle lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUIStyle smallLbl = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };

        GUI.Box(new Rect(10, 10, 340, 300), Title, boxStyle);

        GUI.Label(new Rect(20, 40, 200, 25), "Entity Sayisi:", lblStyle);
        for (int i = 0; i < EntityCounts.Length; i++)
        {
            string label = $"{EntityCounts[i] / 1000}K";
            GUIStyle style = new GUIStyle(btnStyle);
            if (i == SelectedIndex)
            {
                style.normal.textColor = HighlightColor;
                style.fontStyle = FontStyle.Bold;
            }
            if (GUI.Button(new Rect(20 + (i * 63), 65, 58, 30), label, style))
                OnSelect?.Invoke(i);
        }

        if (!IsTesting)
        {
            if (GUI.Button(new Rect(20, 105, 150, 35), "Tek Test", btnStyle))
                OnStartSingle?.Invoke();
            if (GUI.Button(new Rect(180, 105, 150, 35), "Tum Testler", btnStyle))
                OnStartAll?.Invoke();
        }
        else
        {
            GUI.Label(new Rect(20, 110, 300, 25), ">>> Test calisiyor...", lblStyle);
        }

        GUI.Label(new Rect(20, 150, 300, 25), $"FPS: {Fps:F0}", lblStyle);
        GUI.Label(new Rect(20, 175, 300, 25), $"Frame Time: {FrameTimeMs:F2} ms", lblStyle);
        GUI.Label(new Rect(20, 200, 300, 25), $"Aktif Entity: {ActiveCount:N0}", lblStyle);
        GUI.Label(new Rect(20, 235, 310, 60), StatusText, smallLbl);
    }
}
