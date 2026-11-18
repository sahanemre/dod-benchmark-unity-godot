# S1 (Toplu Hareket) — Yeniden Koşu Rehberi

Bu rehber S1'in tez için yeniden koşusunu anlatır:
- konfigürasyon başına 10 tekrar,
- OS düzeyi bellek ölçümü,
- dört konfigürasyonda eşit görüntü ve render koşulları.

## Değişmeyenler

- Hareket ve sekme mantığı, entity hızları, dünya boyutu.
- 13 s koşu: 3 s warmup (atılır) + 10 s ölçüm.
- Frame time'ın ölçülme şekli: Unity'de `Time.unscaledDeltaTime`, Godot'ta
  `_process(delta)`.
- VSync kapalı, FPS sınırı yok, standalone build.
- Tüm konfigürasyonlarda hareket iş yükü tek thread'de, ana thread'de çalışır.
  Unity DOTS'ta Job System kullanılmaz; `MovementSystem` Burst ile ana
  thread'de koşar. (Motorların kendi iç thread'leri, örneğin render thread'i,
  bunun dışındadır.)

## Profiller

Her uygulama `--profile=published|parity` alır. Varsayılan `parity`'dir.

| | published (makaledeki koşul) | parity (eşit koşullar) |
|---|---|---|
| Görüntü (Unity) | Borderless fullscreen, yerel 1920×1080 | Exclusive fullscreen 1920×1080 |
| Görüntü (Godot) | 1280×720 pencere | Exclusive fullscreen 1920×1080; 1280×720 mantıksal dünya `canvas_items/keep` ile 1,5 kat ölçeklenir |
| Entity boyutu (ekranda) | Unity ≈10,1 px, Godot 8 px | Dördü de 10,125 px (Godot 6,75 mantıksal px) |
| Unity OOP sprite | Işıklı (Sprite-Lit-Default) | Işıksız (Sprite-Unlit-Default) |
| Unity DOTS renk / shader | Tek beyaz materyal, **built-in `Sprites/Default`** shader (aşağıdaki nota bkz.) | Entity başına renk (`URPMaterialPropertyBaseColor`), URP/Unlit |
| Unity DOTS pipeline | `original` | `lean` (`--dots-pipeline=original` ile değiştirilebilir) |
| RNG | Orijinal davranış | Sabit seed = 1000 + run_index (pozisyon, hız, renk) |
| HUD (gözetimsiz koşu) | Açık (her frame) | Kapalı |
| Grafik API | Unity: makaledeki gibi (zorlanmaz). Godot: D3D12 | Dördü de Direct3D 12 |
| Build | Godot: debug export; Godot DOD makaledeki DLL ile | Hepsi release (Unity development kapalı, Godot release şablonu, `template_release` DLL) |

**Unity DOTS `lean` pipeline:** URP 3D şablonundan gelen ve iş yüküyle
ilgisi olmayan frame başı maliyetleri kapatır:
- post-processing (Bloom, Vignette, Tonemapping) kapalı,
- skybox yerine düz renk temizleme (Unity OOP ile aynı renk),
- depth ve opaque texture kopyaları kapalı,
- gölgeler kapalı.

**Her iki profilde ortak olanlar:**
- **Odak kaybında çalışmaya devam:** Unity'de Run In Background açık. Koşu
  sırasında pencere odağı kaybedilirse CSV'de `focus_lost=1` yazar. Satır
  yine yazılır; runner bunu sarıyla gösterir.
- **Bellek ölçümü:** Process private bytes (`GetProcessMemoryInfo` →
  `PrivateUsage`), dördünde aynı Windows çağrısıyla, warmup'ın son ~100
  ms'sinde bir kez okunur. Bu çağrının maliyeti atılan bir warmup frame'ine
  düşer, 10 s ölçüm penceresinde hiçbir örnekleme yapılmaz.
- **Eski motor sayaçları kapalı:** Eski motor sayacı okumaları
  (`GC.GetTotalMemory`, `Performance.MEMORY_STATIC`) gözetimsiz koşuda
  kapalıdır; HUD ile elle kullanımda eskisi gibi çalışır.

**Bilinçli olarak değiştirilmeyenler (makalede kısıt olarak belirtilecek):**
- **Godot DOD her frame renk yüklüyor.** MultiMesh entity başına rengi
  (`use_colors`) her frame buffer'a yeniden yazar ve GPU'ya yükler: entity
  başına 12 float'ın 4'ü renktir. Bu fark Unity DOTS lehinedir.
- **Duvara çarpma sıklığı farklı.** Godot'ta entity'ler dünyayı yaklaşık
  4,2 s'de geçer (300 px/s, 1260 px), Unity'de yaklaşık 9,1 s'de (3 birim/s,
  27,4 birim). Yani Godot'ta duvara çarpma yaklaşık 2,2 kat daha sıktır.

### Makaledeki Unity DOTS shader'ı: built-in `Sprites/Default`

**Ne oldu?** Makaledeki Unity DOTS entity'leri, Unity'nin built-in (eski render
hattına ait) `Sprites/Default` shader'ıyla çizildi. Bu, published smoke
koşusunun logunda doğrulandı:
`entity shader: Sprites/Default | renk: tek beyaz | pipeline: original`.

**Neden?** Kod sırasıyla URP/Unlit, URP/Lit, Sprites/Default ve Unlit/Color
deniyordu. URP/Unlit ve URP/Lit build'de yoktu, çünkü hiçbir asset ya da ayar
bunları build'e almıyordu. `Sprites/Default` ise Unity'nin varsayılan Always
Included listesinde olduğu için ilk bulunan oldu. Published profili bu seçimi
aynen tekrarlar. Parity'de URP/Unlit Always Included'a eklendi.

**Entities Graphics ile uyumlu mu?** Bu shader Entities Graphics'in beklediği
shader türünde değil. Shader kaynağına göre:
- **DOTS instancing desteği yok.** Yalnızca klasik GPU instancing var
  (`#pragma multi_compile_instancing`). `DOTS_INSTANCING_ON` varyantı ve
  `UNITY_DOTS_INSTANCED_PROP` tanımları yok.
- **SRP Batcher uyumlu değil.** Built-in CG shader'ı; materyal özellikleri
  `UnityPerMaterial` cbuffer'ında değil.
- **Saydam kuyrukta çiziliyor.** `Queue=Transparent`, alpha blend
  (`Blend One OneMinusSrcAlpha`), `ZWrite Off`.

Entities Graphics (BatchRendererGroup) DOTS instancing destekleyen shader
bekler. Bu shader'ın Entities Graphics'in hızlı yolu dışında kalması ya da
farklı/fallback bir yoldan çizilmesi muhtemeldir. Üstüne saydam olması
yüzünden entity başına derinlik sıralaması ve alpha blending maliyeti
eklenir. Yani makaledeki Unity DOTS render maliyeti, opak ve DOTS instancing
uyumlu bir shader'a göre büyük olasılıkla daha yüksektir. Bu durum Unity DOTS
**aleyhine** bir farktır.

**Henüz doğrulanmadı.** Hangi yolun kullanıldığı ve maliyetin büyüklüğü
ölçülmedi. Kaynak bir GitHub aynasından incelendi; 6000.4.7f1 sürümündeki
kaynakla birebir aynı olduğu doğrulanmalı (bkz. "Loglardan kontrol").

## 1. Build'ler (bir kerelik)

Tüm yeni build'ler `%USERPROFILE%\Documents\s1-builds\` altına gider. Makaledeki
eski build'ler (`Documents\benchmark-godot-oop`, `benchmark-godot-dod`, …)
**olduğu gibi kalır**; published Godot DOD'un DLL'i oradan referans alınır.

### Godot DOD — native eklentiler

```powershell
cd godot\dod-benchmark-dod
scons platform=windows target=template_release
```

- Bu komut `movementdod` (yeni `set_seed` metoduyla) ve `procmem`'in release
  DLL'lerini üretir. `procmem`'i Godot OOP'a da kopyalar.
- **`template_debug`'ı yeniden DERLEME.** Published koşu, makaledeki debug
  DLL'in kendisini kullanacak (aşağıda).
- `procmem`'in debug DLL'i önceki adımda zaten üretildi:
  `demo\bin\libprocmem.windows.template_debug.x86_64.dll`.

### Godot export'ları (her iki proje, ikişer kez)

| Hedef klasör (`Documents\s1-builds\`) | Proje | "Export With Debug" |
|---|---|---|
| `godot-oop-debug\` | Godot OOP | **açık** (published) |
| `godot-oop-release\` | Godot OOP | kapalı (parity) |
| `godot-dod-debug\` | Godot DOD (`demo`) | **açık** (published) |
| `godot-dod-release\` | Godot DOD (`demo`) | kapalı (parity) |

Exe adları `s1_builds.json` ile aynı olmalı: `dod-benchmark-oop.exe` ve
`dod-benchmark-dod (native C++).exe`.

**Published Godot DOD için zorunlu adımlar:**

1. **Hiçbir export'tan önce** makaledeki DLL'i yedekle. Godot DOD export
   preset'inin varsayılan yolu eski build klasörüdür (`Documents\benchmark-godot-dod`);
   oraya export edilirse makaledeki DLL'in üstüne yazılır.
   ```powershell
   New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\s1-builds\_paper-reference" | Out-Null
   Copy-Item "$env:USERPROFILE\Documents\benchmark-godot-dod\libmovementdod.windows.template_debug.x86_64.dll" `
             "$env:USERPROFILE\Documents\s1-builds\_paper-reference\"
   ```
2. Debug export'tan sonra yedekteki DLL'i export klasörüne kopyala:
   ```powershell
   Copy-Item "$env:USERPROFILE\Documents\s1-builds\_paper-reference\libmovementdod.windows.template_debug.x86_64.dll" `
             "$env:USERPROFILE\Documents\s1-builds\godot-dod-debug\" -Force
   ```

Runner, published Godot DOD koşmadan önce bu DLL'i SHA-256 ile
`_paper-reference` içindeki dosyayla karşılaştırır. Farklıysa hiç koşmadan durur.

### Unity (iki proje)

1. Projeyi aç. Yeni scriptler derlenir; Console'da hata olmamalı.
2. Build Profiles'ta **Development Build kapalı** olsun. Parity bunu zorunlu
   tutar.
3. Build hedefleri:
   - `Documents\s1-builds\unity-oop\DODBenchmark-OOP.exe`
   - `Documents\s1-builds\unity-dots\DODBenchmark-DOTS.exe`

   Aynı build iki profilde de kullanılır; profil çalışma anında seçilir.
4. **Grafik API:** Parity koşusu `-force-d3d12` ile açılır, bu yüzden build'de
   Direct3D 12 bulunmalı. Smoke test'te parity çıkış kodu 10 ile "Direct3D12
   bekleniyordu" derse:
   - Player Settings → Other Settings → "Auto Graphics API for Windows"
     işaretini kaldır.
   - Listeyi **Direct3D11, Direct3D12** sırasıyla yap ve yeniden build al.
   - Sıra önemli: published (zorlanmayan) koşu listenin ilk API'sini kullanır.

Unity OOP'ta parity için `Sprite-Unlit-Default` shader'ı Always Included
Shaders'a eklendi (repoda). Published'ı etkilemez.

## 2. Smoke test (her konfigürasyon, N=1K, 1 koşu, iki profil)

Repo kökünde:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 `
  -Profile published,parity -N 1000 -Runs 1 -OutDir results\benchmarks\s1_smoke
```

8 koşu yapılır, yaklaşık 3–4 dakika sürer. Hepsi `OK` olmalı. Sonra
`results\benchmarks\s1_smoke\s1_runs.csv` dosyasını kontrol et:

| Sütun | published beklenen | parity beklenen |
|---|---|---|
| `window_mode` | Unity `borderless`, Godot `windowed` | dördü `exclusive` |
| `render_width/height` | Unity 1920×1080, Godot 1280×720 | dördü 1920×1080 |
| `dots_color` | unity-dots `off`, diğerleri `na` | unity-dots `on` |
| `dots_pipeline` | unity-dots `original` | unity-dots `lean` |
| `focus_lost` | 0 | 0 |
| `process_private_bytes` | > 0 | > 0 |

**Loglardan kontrol** (`results\benchmarks\s1_smoke\logs\`):
- Unity loglarında `[Benchmark] profile=... graphics=...` satırı, kullanılan
  grafik API'yi gösterir. Published satırı, makaledeki API'dir.
- DOTS logunda `entity shader:` satırı, kullanılan shader'ı gösterir. Beklenen:
  published → `Sprites/Default`, parity → `Universal Render Pipeline/Unlit`.
- Published DOTS'ta Entities Graphics'in `Sprites/Default` için uyarı verip
  vermediğine bak:
  ```powershell
  Select-String -Path results\benchmarks\s1_smoke\logs\unity-dots_published_*.log `
    -Pattern "DOTS","BatchRendererGroup","compatib","instancing","error shader","not supported" |
    Select-Object -First 20
  ```

**Hata kodları** (`failures.log`):

| Kod | Anlamı | Ne yapmalı |
|---|---|---|
| 2 | Geçersiz argüman | Komutu kontrol et |
| 3 | Ölçüm penceresinde örnek yok | Logu incele |
| 4 | Private bytes okunamadı | `procmem` / Windows API |
| 5 | Sonuç dosyası yazılamadı | Klasör izinleri |
| 6 | Motor/eklenti kurulumu başarısız | DOTS materyali ya da MovementWorld DLL'i |
| 7 | `procmem` eklentisi yok | Exe yanında `libprocmem...dll` var mı? |
| 8 | Görüntü modu/çözünürlük beklenenden farklı | Masaüstü 1920×1080 ve ölçek %100 mü? |
| 9 | Gerekli shader build'de yok | Always Included Shaders (parity DOTS: URP/Unlit) |
| 10 | Build/API profille uyuşmuyor | Development build, Godot şablonu, D3D12 ya da eski DLL |

## 3. A/B testleri (N=1K ve 100K, 10 koşu)

### Published ve parity (4 konfigürasyon × 2 N × 10 koşu × 2 profil)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 `
  -Profile published,parity -N 1000,100000 -Runs 10 -OutDir results\benchmarks\s1_ab_profile
```

160 koşu, yaklaşık 70 dakika sürer. Profiller her N için art arda koşar
(karışık sıra).

### Unity DOTS pipeline: original ve lean (parity, 2 N × 10 koşu)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 `
  -Configs unity-dots -DotsPipeline original,lean -N 1000,100000 -Runs 10 `
  -OutDir results\benchmarks\s1_ab_pipeline
```

40 koşu, yaklaşık 18 dakika sürer. Geri kalan her şey parity'dir. Fark
yalnızca DOTS render pipeline'ının etkisidir.

## 4. Tam S1 seti (parity, 4 konfigürasyon × 5 N × 10 koşu)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1
```

Varsayılanlar: parity, 4 konfigürasyon, 5 N, 10 koşu. Çıktı
`results\benchmarks\s1\` altına gider. 200 koşu, yaklaşık 85 dakika sürer.

## Koşu sırasında

- Laptop prize takılı, güç modu "En iyi performans" olsun.
- Windows "Rahatsız Etmeyin" açık olsun, arka plan uygulamaları kapalı olsun.
- Ekrana dokunma.
- Script uyku ve ekran kapanmasını kendisi engeller.

## Yarıda kalırsa / tek koşuyu yeniden almak

Geçerli sonuç dosyası olan koşular atlanır. Aynı komutu tekrar çalıştırmak
yalnızca eksikleri alır.

```powershell
# Tek konfigürasyon + tek N
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Configs godot-oop -N 50000

# Tek bir koşuyu zorla yeniden al (örn. focus_lost=1 olan)
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Configs unity-dots -N 100000 -RunIndex 7 -Force

# Yalnızca birleştir
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -MergeOnly
```

Diğer parametreler: `-TimeoutSec` (120), `-CooldownSec` (5),
`-IdleDelaySec` (2), `-BuildsFile`, `-OutDir`.

## Çıktılar

Her `-OutDir` altında şunlar oluşur:

| Dosya | İçerik |
|---|---|
| `<konfig>_<profil>[_<pipeline>]_N<N>_run<kk>.csv` | Koşu başına tek satır, örn. `unity-dots_parity_lean_N1000_run03.csv` |
| `s1_runs.csv` | Tüm koşuların birleşimi |
| `failures.log` | Başarısız koşular |
| `logs\` | Uygulama logları (repoya eklenmez) |

Sütunlar:

```
engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes,
window_mode,render_width,render_height,dots_color,profile,dots_pipeline,focus_lost
```

- `mean_fps` = `1000 / mean_frame_time_ms`. Yayınlanan `AvgFPS` ile aynı
  tanımdır, per-frame FPS ortalaması değildir.
- `window_mode` / `render_*` warmup sonunda ölçülen gerçek değerlerdir.
  Profilin beklediğinden farklıysa satır yazılmaz (kod 8).

## Elle (HUD ile) kullanım

Exe argümansız açıldığında HUD ile çalışır. Varsayılan profil `parity`'dir;
makaledeki koşul için `--profile=published` ver (Godot'ta: `-- --profile=published`).
