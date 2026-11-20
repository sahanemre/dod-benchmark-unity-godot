using UnityEngine;

/// <summary>
/// IMGUI HUD'u cizen bilesen. Tek sorumluluk: OnGUI cagrisini iletmek.
///
/// HUD ayri bir bilesende cunku Unity, sahnede OnGUI metodu olan aktif bir
/// MonoBehaviour varsa her frame IMGUI olaylarini (Layout + Repaint) isler;
/// erken return bile bu maliyeti tamamen kaldirmaz. parity profilindeki
/// gozetimsiz kosularda bu bilesen hic eklenmez, boylece HUD maliyeti sifir
/// olur. published profilinde ve elle kullanimda eskisi gibi her frame cizilir.
/// </summary>
public class HudDrawer : MonoBehaviour
{
    public System.Action Draw;

    void OnGUI()
    {
        Draw?.Invoke();
    }
}
