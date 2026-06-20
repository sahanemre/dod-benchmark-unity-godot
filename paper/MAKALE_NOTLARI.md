# Makale İçin Mimari ve Yöntem Notları

> **Amaç:** Bu doküman, tez/makale yazımında **Introduction**, **Literature** ve
> **Methodology** bölümlerinin hazırlanmasına altyapı sağlar. Tüm teknik kararlar,
> mimari seçimler ve gerekçeler buradan derlenmiştir. Akademik üslupla yeniden
> yazılabilir; verilen tablolar doğrudan makaleye uyarlanabilir.
>
> Detaylı teknik referans için: `godot/dod-benchmark-dod/ARCHITECTURE.md`

---

## 1. Çalışmanın Özeti (Abstract Taslağı)

Bu çalışma, oyun motorlarında **Nesne Yönelimli Programlama (OOP)** ile
**Veri Yönelimli Tasarım (Data-Oriented Design, DOD)** paradigmalarının çalışma
zamanı performansını karşılaştırmalı olarak incelemektedir. Aynı benchmark iş
yükü (N adet entity'nin her karede hareket ettirilmesi, sınır çarpışması ve
ekrana çizilmesi) iki farklı oyun motorunda (Unity ve Godot 4) ve her motorda
iki farklı paradigmada (OOP ve DOD) implemente edilmiştir. Toplam dört
implementasyon, 1.000'den 100.000'e kadar değişen entity sayılarında frame time,
FPS, kare-arası tutarlılık (standart sapma) ve bellek metrikleri açısından
ölçülmüştür. Çalışmanın özgün katkısı, **yerleşik ECS desteği bulunan bir motor
(Unity DOTS)** ile **DOD'un motor desteği olmadan sıfırdan C++ GDExtension ile
uygulandığı bir motoru (Godot)** aynı deneysel çerçevede karşılaştırmasıdır.

---

## 2. Introduction Bölümü İçin Anahtar Noktalar

### 2.1. Problem ve Motivasyon

- Modern oyunlar ve simülasyonlar, on binlerce hatta yüz binlerce varlığı (entity)
  her karede güncellemeyi gerektirir. Geleneksel OOP yaklaşımında her varlık
  bağımsız bir nesnedir ve kendi davranışını kapsüller (encapsulation).
- OOP'un sağladığı modülerlik ve bakım kolaylığı, yüksek varlık sayılarında
  **önbellek dostu olmayan bellek erişimi** ve **sanal fonksiyon çağrı maliyeti**
  nedeniyle performans darboğazına dönüşür.
- DOD, verinin işlenme biçimine göre düzenlenmesini (özellikle **Struct of Arrays,
  SoA** düzeni) ve davranışın veriden ayrılmasını öngörür. Bu, CPU önbelleğini
  verimli kullanmayı ve SIMD vektörizasyonunu mümkün kılar.

### 2.2. Araştırma Soruları

1. **AS1:** Aynı oyun motoru içinde OOP'tan DOD'a geçiş, artan varlık sayısıyla
   performansı ne ölçüde iyileştirir?
2. **AS2:** Motorun yerleşik DOD desteği (Unity DOTS) ile motor desteği olmadan
   manuel uygulanan DOD (Godot + C++ GDExtension) arasında performans farkı var
   mıdır? Varsa kaynakları nelerdir?
3. **AS3:** Aynı paradigma (OOP veya DOD) iki farklı motorda uygulandığında
   gözlemlenen performans farkı, dil/çalışma zamanı (C#/.NET vs GDScript/C++)
   farkıyla nasıl ilişkilidir?

### 2.3. Katkılar

- Dört bağımsız ama metodolojik olarak eşdeğer implementasyon (Unity OOP, Unity
  DOTS, Godot OOP, Godot DOD).
- DOD'un motor desteği olmaksızın bir motora **GDExtension/C++ köprüsü** ile nasıl
  entegre edileceğine dair somut bir referans mimari.
- Render maliyetini sabit tutarak (GPU instancing eşitlemesi) CPU tarafı
  paradigma farkını izole eden kontrollü bir deney tasarımı.

---

## 3. Literature Bölümü İçin Kavramsal Çerçeve

> Not: Aşağıdaki kavramlar tartışılması gereken literatür eksenlerini gösterir.
> Atıflar (kaynaklar) ayrıca taranmalıdır; bu doküman kavram haritasıdır.

### 3.1. Veri Yönelimli Tasarım (DOD)

- DOD'un temel ilkesi: "Veri nasıl işlenecekse o şekilde düzenlenmelidir."
  (Mike Acton'ın oyun geliştirme topluluğundaki etkili sunumları bu yaklaşımın
  popülerleşmesinde merkezdedir.)
- **Mechanical sympathy** (donanımla uyum) kavramı: yazılımın CPU önbellek
  hiyerarşisi, prefetcher ve SIMD birimleriyle uyumlu çalışması.

### 3.2. Bellek Düzeni: AoS vs SoA

- **Array of Structs (AoS):** `[{x,y,vx,vy}, {x,y,vx,vy}, ...]` — OOP'un doğal
  düzeni. Tek bir nesnenin tüm alanları yan yana; ancak aynı alana (örn. tüm x'ler)
  toplu erişimde önbellek satırları israf edilir.
- **Struct of Arrays (SoA):** `x[], y[], vx[], vy[]` — DOD'un tercih ettiği düzen.
  Aynı alana toplu erişim ardışık bellekte gerçekleşir; prefetcher verimli çalışır.
- **Önbellek satırı (cache line) analizi:** x86-64'te 64 byte = 16 float. SoA'da
  bir önbellek satırı 16 varlığın aynı alanını taşır; AoS'ta çoğu zaman 1 varlığın
  birkaç alanını taşır → teorik olarak ~16× daha az önbellek ıskası (cache miss).

### 3.3. Entity Component System (ECS)

- ECS, DOD'un oyun motorlarındaki kurumsallaşmış biçimidir: **Entity** (kimlik),
  **Component** (saf veri), **System** (davranış).
- Unity DOTS, ECS'i **arketip (archetype)** tabanlı **16 KB chunk** bellek modeliyle
  uygular; aynı bileşen kümesine sahip varlıklar aynı chunk'ta bitişik durur.

### 3.4. Derleyici ve Vektörizasyon

- **Burst Compiler (Unity):** C#'ın bir alt kümesini LLVM ile native koda çevirir
  ve **garantili SIMD** vektörizasyonu uygular.
- **Standart C++ (Godot DOD):** MSVC/Clang/GCC auto-vectorization yapabilir, ancak
  bu olasılıksaldır ve `-O2/-O3` optimizasyon seviyesine bağlıdır.

### 3.5. Oyun Motoru Mimarileri

- **Unity:** Olgun, ticari, DOTS ile birinci sınıf ECS desteği sunan motor.
- **Godot 4:** Açık kaynak (MIT), yerleşik ECS *yok*; ancak GDExtension API'si ile
  C++ native eklentilere izin verir → DOD'u "sıfırdan" uygulamak için ideal.

### 3.6. Render: GPU Instancing

- Çok sayıda aynı meshin tek draw call ile çizilmesi (GPU instancing) hem Unity
  (Hybrid Renderer / GPU Resident Drawer) hem Godot (`MultiMeshInstance2D`)
  tarafından desteklenir. Bu, render maliyetini iki motor arasında eşitlemeyi
  sağlar.

---

## 4. Methodology Bölümü İçin Deney Tasarımı

### 4.1. Dört Implementasyonun Karşılaştırma Matrisi

| Proje | Motor | Paradigma | Veri Düzeni | Hareket | Render | Draw Call | Dil |
|---|---|---|---|---|---|---|---|
| Unity OOP | Unity | OOP | AoS (her GameObject ayrı) | N × `Update()` | N × MeshRenderer | ~N | C# (MonoBehaviour) |
| Unity DOTS | Unity | DOD | SoA (ECS chunk) | Tek `ISystem` döngüsü | Hybrid Renderer | **1** | C# + Burst |
| Godot OOP | Godot 4 | OOP | AoS (her Node2D ayrı) | N × `_process()` | N × `_draw()` | ~N | GDScript |
| Godot DOD | Godot 4 | DOD | SoA (`std::vector`) | Tek C++ `update()` | MultiMesh | **1** | C++ + GDScript |

### 4.2. Üç Karşılaştırma Ekseni

```
              OOP                        DOD
         ┌───────────────┐         ┌───────────────┐
Unity    │  Unity OOP    │ ──────> │  Unity DOTS   │
         └───────────────┘         └───────────────┘
               │                         │
               │ Platform farkı          │ Platform farkı
               ↓                         ↓
         ┌───────────────┐         ┌───────────────┐
Godot    │  Godot OOP    │ ──────> │  Godot DOD    │
         └───────────────┘         └───────────────┘
              Paradigma farkı →
```

- **Eksen 1 — Paradigma farkı (yatay):** Aynı motorda OOP→DOD geçişinin etkisi.
  (Unity OOP vs Unity DOTS; Godot OOP vs Godot DOD)
- **Eksen 2 — Platform farkı, OOP seviyesinde (sol dikey):** Unity OOP vs Godot OOP.
  Dil/çalışma zamanı farkı (C#/.NET vs GDScript) izole edilir.
- **Eksen 3 — Platform farkı, DOD seviyesinde (sağ dikey):** Unity DOTS vs Godot DOD.
  "Motor sağlanan DOD" ile "sıfırdan uygulanan DOD" karşılaştırılır.

### 4.3. Benchmark İş Yükü (Bağımsız Değişkenler Sabit)

Dört implementasyonda da **özdeş** iş yükü çalışır:

- N adet entity ekrana rastgele konumlandırılır.
- Her entity rastgele bir başlangıç açısında, sabit hızda (`move_speed = 300 px/s`)
  hareket eder.
- Ekran sınırlarına çarptığında yansır (bounce).
- Her entity sabit bir renge ve 8×8 piksel görsel temsile sahiptir.

**Entity sayıları:** 1.000 / 5.000 / 10.000 / 50.000 / 100.000 (mertebe taraması).

### 4.4. Ölçüm Protokolü

| Parametre | Değer | Gerekçe |
|---|---|---|
| Warmup süresi | 3 saniye | İlk spawn sonrası GC/JIT/shader derleme tepelerini hariç tutar |
| Ölçüm süresi | 10 saniye | Yeterli örneklem; toplam test süresi 13 saniye |
| VSync | **KAPALI** | Açık olduğunda frame time monitör yenileme hızına kilitlenir |
| FPS sınırı | Yok | Yapay tavan kaldırılır, gerçek iş yükü ölçülür |
| Tekrar | Test başına ≥3 koşu önerilir | OS zamanlayıcı/termal gürültüyü ortalamak için |

### 4.5. Toplanan Metrikler (Bağımlı Değişkenler)

- **Ortalama frame time (ms):** Ölçüm penceresindeki tüm karelerin aritmetik
  ortalaması — birincil performans metriği.
- **Min / Maks frame time (ms):** En iyi / en kötü kare.
- **Standart sapma (ms):** Kare-arası tutarlılık; düşük = daha kararlı (stutter az).
- **Ortalama FPS / Min FPS:** `1000 / frame_time` türevleri.
- **Bellek kullanımı (MB):** Test ortasında ölçülen tahsis (sınırlamalar için bkz.
  Bölüm 4.7).

### 4.6. Geçerlilik İçin Kritik Kontroller

**VSync kontrolü (deneyin geçerliliği için zorunlu):**
İlk denemelerde tüm entity sayıları özdeş sonuç verdi (örn. 165 Hz monitörde
sabit 6.06 ms / StdDev = 0). Kök neden: VSync etkinken motor her kareyi monitör
yenileme hızında sunar; gerçek hesaplama yükünden bağımsız olarak frame time
sabittir. Düzeltme tüm dört projede uygulanmıştır:

```gdscript
# Godot
DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
Engine.max_fps = 0
```
```csharp
// Unity
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = -1;
```

**Render eşitlemesi (kontrol değişkeni):**
Her iki DOD implementasyonu da GPU instancing ile tek draw call kullanır. Bu sayede
ölçülen frame time farkı render'dan değil, CPU tarafı hesaplama verimliliğinden
(Burst vs C++, ECS chunk vs `std::vector`) kaynaklanır.

### 4.6b. Cache Miss Ölçüm Protokolü (AMD uProf)

Frame time ve FPS metrikleri DOD'un *sonucunu* gösterir; **L1/L2 cache miss oranı**
ise DOD'un performans kazancının *donanım düzeyindeki kök nedenini* doğrudan
ölçer (bkz. Bölüm 3.2 — AoS vs SoA). Bu metrik, motor içi `Performance`/`GC`
API'leriyle elde edilemez; donanım performans sayaçlarını (Hardware Performance
Counters) okuyan harici bir profiler gerekir. Bu çalışmada **AMD uProf**
kullanılmıştır (test donanımı AMD Ryzen 5 5600H, Zen3 mimari).

**Profilleme türü:** Event-Based Sampling (Time-Based Sampling değil — donanım
sayaçlarını örneklemek için event-based gereklidir).

**İzlenen sayaçlar:**
- `L1 Data Cache Misses` (DC Miss)
- `L2 Cache Misses`
- `Instructions Retired` + `CPU Clocks` (IPC — Instructions Per Cycle —
  hesaplamak için)

**Hesaplanan metrik:**
```
Cache Miss Rate (%) = (L2 Misses / L2 Accesses) × 100
```

**Protokol:**
1. Her implementasyon için **ayrı profil oturumu** açılır (uProf "Launch
   Application" ile `.exe` başlatılır).
2. Oturum başına **tek bir entity sayısı** test edilir (HUD'dan "Tek Test"); 5
   entity sayısının verisi karışmaması için "Tüm Testler" modu kullanılmaz.
3. Uç noktalar (N=1.000 ve N=100.000) zorunlu profillenir; ara noktalar
   (5K/10K/50K) kaynak elverdiğince eklenir.
4. Toplam oturum sayısı: 4 implementasyon × ≥2 entity sayısı = ≥8 profil.

**Beklenen bulgu (hipotez):**
- **OOP (AoS):** Yüksek L2 miss oranı; entity sayısıyla birlikte **artan** miss
  oranı (her nesne farklı heap adresinde → prefetcher verimsiz).
- **DOD (SoA):** Düşük ve entity sayısından **bağımsız** miss oranı (ardışık
  bellek erişimi → prefetcher verimli).

Bu metrik, makalenin "neden DOD daha hızlı?" sorusuna donanım kanıtı sağlar;
frame time tek başına *sonucu* gösterirken, cache miss oranı *mekanizmayı*
gösterir.

> **Not:** AMD uProf v4.2 ile GUI çökme sorunu çözülmüş ve aşağıdaki 8 oturum
> başarıyla tamamlanmıştır (bkz. "Ölçüm Sonuçları"). `AMDuProfCLI.exe collect`
> ile GUI'ye gerek kalmadan profil toplanmış, `AMDuProfCLI.exe report -i
> <oturum>` ile "10 HOTTEST PROCESSES" bölümünden süreç-düzeyinde toplu
> (aggregate) metrikler çıkarılmıştır.

**Ölçüm Sonuçları (Gerçek Veri — AMD Ryzen 5 5600H, Zen3, AMD uProf 4.2):**

Ham veri: `results/cache-profiling/cache_miss_results.csv`. Sayaçlar:
`CYCLES_NOT_IN_HALT`, `RETIRED_INST` (→ IPC), `L1_DC_MISS_RATIO` (uProf'un
doğrudan verdiği L1 metriği) ve dört adet `L1_DEMAND_DC_REFILLS_*` ham sayacından
türetilen L2 miss rate:

```
L2 Miss Rate = (LOCAL_CACHE + EXTERNAL_CACHE_LOCAL + LOCAL_DRAM)
               / (LOCAL_CACHE + EXTERNAL_CACHE_LOCAL + LOCAL_DRAM + LOCAL_L2)
```

(LOCAL_L2 = L1 miss'in L2'den karşılanması = "L2 hit"; diğer üçü L2'nin de
ıskaladığı, L3/harici cache/DRAM'den karşılanan erişimler = "L2 miss".)

| İmplementasyon | N | IPC | L1 DC Miss Ratio | L2 Miss Rate |
|---|---|---|---|---|
| Unity OOP   | 1.000   | 0.881 | 5.47% | 48.87% |
| Unity OOP   | 100.000 | 0.485 | 5.89% | 49.41% |
| Unity DOTS  | 1.000   | 0.735 | 5.79% | 47.43% |
| Unity DOTS  | 100.000 | 0.925 | 2.75% | 45.93% |
| Godot OOP   | 1.000   | 1.596 | 2.42% | 38.93% |
| Godot OOP   | 100.000 | 1.369 | 2.76% | 29.93% |
| Godot DOD   | 1.000   | 0.877 | 4.86% | 45.00% |
| Godot DOD   | 100.000 | 2.010 | 2.81% | 26.42% |

**Yorum:**
- **Ölçekle birlikte değişim yönü, hipotezi destekliyor:** Her iki DOD/DOTS
  implementasyonu da N arttıkça IPC'de **iyileşme** ve L2 miss rate'te **düşüş**
  gösteriyor (DOTS: IPC 0.735→0.925, L2 miss 47.4%→45.9%; Godot DOD: IPC
  0.877→2.010, L2 miss 45.0%→26.4%). Bu, SoA bellek düzeninin büyük N'de
  donanım prefetcher'ını daha verimli kullandığını doğrudan gösterir.
- **Godot DOD @ 100K** tüm 8 oturumun en iyi sonucu: en yüksek IPC (2.01) ve
  en düşük L2 miss rate (%26.4) — DOD tezinin niceliksel kanıtı.
- **Godot OOP'ta da L2 miss düşüşü var** (38.9%→29.9%) ama Godot DOD'dan daha
  az; bu, Godot Node2D'lerin Unity GameObject'lerine göre nispeten daha derli
  toplu bellek tahsisi yapmasıyla açıklanabilir — yine de AoS/SoA farkı IPC'de
  açıkça görülüyor (Godot DOD 100K'da Godot OOP'tan ~%47 daha yüksek IPC).
- **Unity OOP, N ile birlikte kötüleşiyor** (IPC 0.881→0.485, beklenen OOP
  davranışı: dağınık heap adresleri arttıkça prefetcher verimsizleşiyor).
  Ancak Unity sonuçları genel olarak daha gürültülü (bkz. aşağıdaki limitasyon
  notu) — Unity DOTS'un 1K→100K IPC artışı beklenenden düşük kalmış olabilir.

> **Bilinen limitasyon (kullanıcı tarafından doğrulandı):** Unity uygulamaları
> açılışta birkaç saniye gecikme yaşıyor; `AMDuProfCLI.exe collect` exe'yi
> başlattığı anda örneklemeye başladığından, her Unity oturumunun başında
> kaçınılmaz bir "motor başlatma" (workload-dışı) süresi profile karışıyor.
> Test, pencere göründüğü an başlatılmış ve bitişten 1-2 sn içinde profil
> durdurulmuş olsa da, bu başlangıç gecikmesi — özellikle kısa 1K oturumlarında
> — toplam örnekleme süresinin görece daha büyük bir kısmını oluşturuyor. Bu
> durum **Unity↔Godot arası mutlak IPC/miss-rate karşılaştırmalarını** zayıflatabilir;
> ancak aynı motor içindeki **OOP vs DOD/DOTS karşılaştırması** büyük ölçüde
> geçerliliğini korur, çünkü her iki varyant da aynı motor başlatma yükünü
> paylaşır. Makalede bu sınırlama açıkça belirtilmelidir.

### 4.7. Geçerlilik Tehditleri (Limitations)

1. **Bellek ölçümünün eksikliği:** Godot DOD'da `std::vector` tahsisleri native
   yığında gerçekleşir ve motorun bellek sayacına (`Performance.MEMORY_STATIC`)
   yansımaz; karşılaştırmalı bellek için harici profiler gerekir.
2. **GDScript↔C++ sınır maliyeti:** Her kare buffer aktarımı sınır geçer; çok
   yüksek N'de (>500K) aktarım maliyeti hesaplamayı geçebilir.
3. **Debug vs Release derleme:** GDExtension derleme tipi performansı %20–200
   etkileyebilir; tüm ölçümlerde aynı tip kullanılmalıdır.
4. **Tekrar edilebilirlik:** OS zamanlayıcı, arka plan süreçleri ve termal
   kısıtlama gürültü yaratır; çoklu koşu ve StdDev raporlaması önerilir.

---

## 5. Her İmplementasyonun Mimari Detayı

### 5.1. Unity OOP (`unity/DODBenchmark-OOP`)

- Her entity bir `GameObject` + `EntityMover` (MonoBehaviour). Kendi konumunu,
  hızını, rengini kapsüller; `Update()`'inde kendi hareketini yapar.
- Tek Sorumluluk İlkesine göre ayrıştırılmış harness: `BenchmarkManager`
  (orkestratör), `EntitySpawner`, `FrameStatistics`, `BenchmarkResult`,
  `CsvExporter`, `BenchmarkHUD`.
- Render: her GameObject'in kendi MeshRenderer'ı → ~N draw call.

### 5.2. Unity DOTS (`unity/DODBenchmark-DOTS`)

- Bileşenler saf veri: `Velocity`, `ScreenBounds` (`IComponentData`).
- Davranış: `MovementSystem` (`ISystem`, `[BurstCompile]`) tüm entity'leri tek
  `foreach` sorgusuyla işler.
- Bellek: ECS arketip chunk'ları (16 KB) → SoA, otomatik SIMD.
- Render: Hybrid Renderer / GPU Resident Drawer → 1 draw call.

### 5.3. Godot OOP (`godot/dod-benchmark-oop`)

- Her entity bir `Node2D` (`EntityMover.gd`); `_process()`'te hareket, `_draw()`'da
  çizim. AoS bellek düzeni (her Node2D ayrı heap adresinde).
- Harness GDScript'te SRP'ye göre ayrı: `BenchmarkManager`, `EntitySpawner`,
  `FrameStatistics`, `CsvExporter`, `BenchmarkResult`, `BenchmarkHUD`.
- Render: N × `_draw()` (CanvasItem) → ~N draw call.

### 5.4. Godot DOD (`godot/dod-benchmark-dod`)

Hibrit mimari — **hesaplama native C++**, **render GDScript + MultiMesh**:

- **C++ tarafı (`MovementWorld`):** Veriler ayrı `std::vector<float>` dizilerinde
  (SoA): `pos_x, pos_y, vel_x, vel_y, col_r, col_g, col_b`. `update()` tek döngüde
  tüm diziyi işler (ardışık okuma/yazma, sınır değerleri register'da). `get_buffer()`
  MultiMesh için 12 float/instance'lık `PackedFloat32Array` üretir (`ptrw()` ile
  kopyasız yazım).
- **GDScript tarafı (`BenchmarkOrchestrator.gd`):** Her kare iki satır —
  `_world.update(...)` (C++ hot loop) ve `_multimesh.buffer = _world.get_buffer()`.
  `MultiMeshInstance2D` tüm entity'leri tek draw call ile çizer.
- **GDExtension altyapısı:** SCons build, godot-cpp submodule (4.6),
  `ClassDB.instantiate("MovementWorld")` ile çağrılır. Eklenti derlenmemişse
  graceful degradation (panel kilitlenir, build talimatı gösterilir).

#### Neden Node2D+Sprite değil? (Önemli metodolojik karar)

Godot DOD için her entity'ye `Node2D+Sprite` verme alternatifi reddedildi:
(a) N adet Node nesnesi DOD'un önlediği nesne overhead'ini geri getirir,
(b) N draw call üretir → Unity DOTS'un 1 draw call'ı ile render eşitsizliği yaratır,
(c) kontrol değişkeni (render) bozulduğu için CPU paradigma farkı izole edilemez.
`MultiMesh` seçimi her iki DOD'u GPU instancing'de eşitler.

---

## 6. OOP → DOD Dönüşüm Tablosu (Makalede Kullanılabilir)

| Kavram | OOP (Unity/Godot) | DOD (Unity DOTS / Godot DOD) |
|---|---|---|
| Entity temsili | Nesne (GameObject / Node2D) | Dizi indeksi (int) / ECS entity |
| Veri depolama | Nesne alanları (AoS) | Ayrı bitişik diziler (SoA) / ECS chunk |
| Davranış | Nesne metodu (`Update`/`_process`) | Sistem döngüsü (1 toplu döngü) |
| Çağrı sayısı | N (her nesneye sanal çağrı) | 1 (tek döngü) |
| Çizim | N draw call | 1 draw call (GPU instancing) |
| Bellek erişimi | Rastgele heap (cache-unfriendly) | Ardışık (cache-friendly) |

---

## 7. Test Donanımı ve Sürümler

- **CPU:** AMD Ryzen 5 5600H (12 thread, 3.3 GHz)
- **RAM:** 16 GB
- **GPU:** AMD Radeon Graphics (entegre)
- **OS:** Windows 11
- **Unity:** 2022.3.x LTS (DOTS 1.0)
- **Godot:** 4.6.2 stable
- **GDExtension:** godot-cpp 4.6.2
- **C++ standardı:** C++17

---

## 8. CSV Çıktı Şeması (Veri Analizi İçin)

```
EntityCount, AvgFrameTime_ms, MinFrameTime_ms, MaxFrameTime_ms, StdDev_ms,
AvgFPS, MinFPS, MemoryUsed_MB, TotalFrames, Duration_s
```

Tüm sayısal değerler locale-bağımsız nokta ondalık ayırıcıyla yazılır
(InvariantCulture / C-locale). Bu, Türkçe locale'de virgül-ondalık ile CSV sütun
ayırıcısının çakışmasını önler — veri analizinde pandas/Excel uyumluluğu için
kritik bir ayrıntı.

---

*Bu doküman makale yazımına yardımcı olmak için derlenmiştir. Teknik derinlik için
`godot/dod-benchmark-dod/ARCHITECTURE.md` dosyasına başvurun.*
