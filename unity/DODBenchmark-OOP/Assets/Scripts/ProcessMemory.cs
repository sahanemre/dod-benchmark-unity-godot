using System;
using System.Runtime.InteropServices;

/// <summary>
/// Isletim sistemi duzeyinde bellek olcumu (motor profiler'i DEGIL).
/// Tek sorumluluk: surecin "Private Bytes" degerini okumak.
///
/// Windows API: GetProcessMemoryInfo -> PROCESS_MEMORY_COUNTERS_EX.PrivateUsage
/// (perfmon / Task Manager'daki "Private Bytes" / "Commit size" ile ayni deger).
/// Godot projeleri ayni cagriyi ortak "procmem" GDExtension'i uzerinden yapar;
/// dort konfigurasyonda da ayni OS yontemi kullanilir.
///
/// Mono ve IL2CPP'de calisir (P/Invoke). Windows disinda -1 doner.
/// </summary>
public static class ProcessMemory
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // K32GetProcessMemoryInfo kernel32'de bulunur (Windows 7+); psapi.dll
    // yuklemeye gerek kalmaz.
    [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(IntPtr process,
        ref PROCESS_MEMORY_COUNTERS_EX counters, uint size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
#endif

    /// <summary>Surecin private bytes degeri; okunamazsa -1.</summary>
    public static long GetPrivateBytes()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            var c = new PROCESS_MEMORY_COUNTERS_EX();
            c.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));
            if (GetProcessMemoryInfo(GetCurrentProcess(), ref c, c.cb))
                return (long)c.PrivateUsage.ToUInt64();
        }
        catch (Exception)
        {
            // asagida -1 doner
        }
#endif
        return -1;
    }
}
