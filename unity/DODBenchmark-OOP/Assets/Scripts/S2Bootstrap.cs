using UnityEngine;

/// <summary>
/// --scenario=S2 verildiginde S1 yoneticisini (BenchmarkManager) Start'i
/// cagrilmadan devre disi birakir ve S2 yoneticisini ekler. S1 koduna dokunmaz;
/// --scenario verilmezse (S1) hicbir sey yapmaz.
/// AfterSceneLoad: sahne nesnelerinin Awake'inden sonra, Start'tan once.
/// </summary>
public static class S2Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        RunArgs args = RunArgs.Parse(System.Environment.GetCommandLineArgs());
        if (args.Scenario != "S2") return;

        foreach (var m in Object.FindObjectsByType<BenchmarkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            m.enabled = false;
        new GameObject("S2OopManager").AddComponent<S2OopManager>();
    }
}
