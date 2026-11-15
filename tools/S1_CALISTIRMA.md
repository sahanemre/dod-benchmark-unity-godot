# S1 (Toplu Hareket) — Gözetimsiz Yeniden Koşu Rehberi

Bu rehber S1'in 10 tekrarlı, bellek ölçümlü yeniden koşusunun nasıl
çalıştırılacağını anlatır. Ölçüm davranışı yayınlanan S1 ile aynıdır:

- Hareket/sekme mantığı değişmedi.
- 13 s koşu: 3 s warmup + 10 s ölçüm.
- Frame time aynı kaynaktan ölçülüyor: Unity'de `Time.unscaledDeltaTime`,
  Godot'ta `_process(delta)`.
- VSync kapalı, FPS sınırı yok, standalone build.
- HUD koşu sırasında da çizilir (yayınlanan ölçümde de çiziliyordu).

## Ne değişti?

| | Önce | Şimdi |
|---|---|---|
| Tekrar | 3, elle | Varsayılan 10, `-Runs` ile ayarlanabilir |
| Süreç | Bir açılışta 5 N arka arkaya ("Tüm Testler") | Her (konfigürasyon, N, tekrar) ayrı süreç |
| Bellek | Motor sayaçları (`GC.GetTotalMemory`, `Performance.MEMORY_STATIC`) | OS düzeyi **process private bytes**, warmup biter bitmez bir kez |
| Çıktı | Açılış başına 5 satırlık CSV | Koşu başına 1 satırlık CSV + birleşik `s1_runs.csv` |
| Başlatma | HUD butonu | Komut satırı (`--n`); argümansız açılınca HUD eskisi gibi çalışır |

**Bellek yöntemi:** Dört konfigürasyonda da aynı Windows çağrısı kullanılır:
`GetProcessMemoryInfo` → `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage`. Bu,
perfmon'daki "Private Bytes" sayacıdır.

- Unity bu çağrıyı P/Invoke ile yapar (`ProcessMemory.cs`).
- GDScript işletim sistemi API'lerine erişemediği için iki Godot projesi
  ortak, küçük bir GDExtension kullanır (`godot/procmem`).

Değer, ilk ölçülen frame'de (warmup bittiğinde) bir kez okunur. Çağrının
maliyeti mikrosaniyeler düzeyindedir.

## 1. Bir kerelik hazırlık

1. **Kodu çek:** `git pull`

2. **Native eklentileri derle.** `procmem`'in kaynakları `godot\procmem\src`
   altındadır, ama Godot DOD'un SConstruct'ı tarafından ikinci bir kütüphane
   olarak derlenir. Böylece godot-cpp tek kez derlenir. Godot DOD'u derlerken
   kullandığın godot-cpp klasörü yeterlidir. Aynı komut iki DLL'i de üretir:
   ```powershell
   cd godot\dod-benchmark-dod
   scons platform=windows target=template_debug
   scons platform=windows target=template_release
   ```
   - `libmovementdod...dll` ve `libprocmem...dll` → `demo\bin\`
   - `libprocmem...dll` → `godot\dod-benchmark-oop\bin\` (kendiliğinden kopyalanır)

   `movementdod`'un kaynağı değişmedi. Export'u "Export With Debug" ile
   alıyorsan `template_debug`, almıyorsan `template_release` DLL'leri
   kullanılır. İkisini de derlemek en garantisi.

3. **Unity OOP ve Unity DOTS:** Projeyi Unity'de aç. Yeni `.cs` dosyaları
   otomatik derlenir. Ardından mevcut ayarlarla Windows standalone build al.

4. **Godot OOP ve Godot DOD:** Projeyi editörde aç ve Windows'a export et.
   `procmem.gdextension` sayesinde DLL export'a kendiliğinden eklenir.
   Export'tan sonra exe'nin yanında `libprocmem...dll` olduğunu kontrol et.

5. **Exe yolları:** `tools\s1_builds.json` dosyasını aç ve dört exe'nin
   yolunu kontrol et. Varsayılan yollar `%USERPROFILE%\Documents\...`
   altındadır.

## 2. Çalıştırma

Repo kökünde, PowerShell'de:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1
```

Varsayılan olarak 4 konfigürasyon × 5 N × 10 tekrar = 200 süreç çalışır.
Toplam süre yaklaşık 80–90 dakikadır.

**Her koşuda olanlar:**

1. Script exe'yi argümanlarla başlatır.
2. Uygulama yaklaşık 2 s boşta bekler. Bu süre butona basmanın yerini
   tutar; motor açılışındaki takılmalar teste karışmaz.
3. Uygulama testi kendisi başlatır.
4. 3 s warmup biter, o anda private bytes bir kez okunur.
5. 10 s boyunca frame time ölçülür.
6. Uygulama tek satırlık sonucunu kendi dosyasına yazar ve kapanır.
7. Script sonraki koşudan önce 5 s bekler (soğuma).

**Sıra karışıktır:** tekrar → konfigürasyon → N. Böylece termal kayma tek
bir konfigürasyona yığılmaz.

**Script çalışırken uyku ve ekran kapanması engellenir.**

### Ölçüm sırasında

- Laptop prize takılı olsun, güç modu "En iyi performans" olsun.
- Arka plan uygulamalarını kapat.
- Pencerelere dokunma. Odak değişimi frame time'ı etkileyebilir.

## 3. Tekrar çalıştırma ve hata durumu

Geçerli sonuç dosyası olan koşular **atlanır**. Script yarıda kesilirse ya
da bazı koşular başarısız olursa aynı komutu tekrar çalıştırman yeterli;
yalnızca eksik koşular alınır.

```powershell
# Tek konfigürasyon + tek N (eksik tekrarlar)
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Configs godot-oop -N 50000

# Tek bir koşuyu zorla yeniden al
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Configs unity-dots -N 100000 -RunIndex 7 -Force

# Birden fazla değer virgülle verilir
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Configs unity-oop,godot-oop -N 1000,5000

# Yalnızca birleştir
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -MergeOnly
```

Diğer parametreler (varsayılanlarıyla):

| Parametre | Varsayılan | Açıklama |
|---|---|---|
| `-Runs` | 10 | Tekrar sayısı |
| `-TimeoutSec` | 120 | Bu süreyi aşan koşu sonlandırılır |
| `-CooldownSec` | 5 | Süreçler arası bekleme |
| `-IdleDelaySec` | 2 | Açılış sonrası testi başlatmadan önce bekleme |

Başarısız bir koşu (çökme, zaman aşımı ya da hata kodu) diğerlerini
etkilemez. Her başarısız koşu `failures.log`'a tek satır olarak yazılır.
Uygulamanın kendi logları `logs/` klasöründedir (repoya eklenmez).

**Uygulama çıkış kodları:**

| Kod | Anlamı |
|---|---|
| 0 | Başarılı |
| 2 | Geçersiz argüman ya da listede olmayan N |
| 3 | Ölçüm penceresinde örnek yok |
| 4 | Private bytes okunamadı |
| 5 | Sonuç dosyası yazılamadı |
| 6 | Motor/eklenti kurulumu başarısız (DOTS shader ya da MovementWorld DLL'i) |
| 7 | procmem eklentisi yüklenemedi (Godot) |

## 4. Çıktılar

Hepsi `results\benchmarks\s1\` altındadır:

| Dosya | İçerik |
|---|---|
| `<konfig>_N<N>_run<kk>.csv` | Koşu başına tek satır, örn. `godot-dod_N100000_run07.csv` |
| `s1_runs.csv` | Tüm koşuların birleşimi |
| `failures.log` | Başarısız koşular |
| `logs\` | Uygulama logları (gitignore'da) |

Sütunlar:

```
engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes
```

| Sütun | Değerler / açıklama |
|---|---|
| `engine` | `unity`, `godot` |
| `paradigm` | `oop`, `dod` (Unity DOTS = `unity` + `dod`) |
| `scenario` | `S1` |
| `mean_frame_time_ms` | 10 s ölçüm penceresindeki frame time'ların ortalaması |
| `mean_fps` | `1000 / mean_frame_time_ms`. Yayınlanan `AvgFPS` ile aynı tanım, per-frame FPS ortalaması değil |
| `process_private_bytes` | Warmup bitiminde okunan private bytes (bayt) |

## Elle (HUD ile) kullanım

Exe argümansız açıldığında her şey eskisi gibi çalışır: HUD butonları,
"Tüm Testler" ve eski CSV biçimi.
