# Godot DOD Benchmark — Teknik Mimari Dökümanı

## İçindekiler

1. [Genel Bakış ve Motivasyon](#1-genel-bakış-ve-motivasyon)
2. [Godot OOP ile Karşılaştırma](#2-godot-oop-ile-karşılaştırma)
3. [Unity DOTS ile Karşılaştırma](#3-unity-dots-ile-karşılaştırma)
4. [Mimari Kararlar ve Gerekçeleri](#4-mimari-kararlar-ve-gerekçeleri)
5. [Teknik Bileşenler ve Sorumlulukları](#5-teknik-bileşenler-ve-sorumlulukları)
6. [Veri Modeli: SoA (Struct of Arrays)](#6-veri-modeli-soa-struct-of-arrays)
7. [Hareket Sistemi: Hot Loop Analizi](#7-hareket-sistemi-hot-loop-analizi)
8. [Render Katmanı: MultiMesh Hybrid Yaklaşım](#8-render-katmanı-multimesh-hybrid-yaklaşım)
9. [GDExtension Altyapısı](#9-gdextension-altyapısı)
10. [Benchmark Metodolojisi](#10-benchmark-metodolojisi)
11. [Bilinen Sınırlamalar ve Araştırma Notları](#11-bilinen-sınırlamalar-ve-araştırma-notları)

---

## 1. Genel Bakış ve Motivasyon

Bu proje, oyun motorlarında **Nesne Yönelimli Tasarım (OOP)** ile **Veri Yönelimli Tasarım (DOD/ECS)** paradigmalarını karşılaştıran deneysel bir benchmark çalışmasının parçasıdır. Godot DOD prototipi, OOP ile aynı iş yükünü (N adet entity'nin her frame hareket ettirilmesi ve ekrana çizilmesi) mümkün olan en saf DOD yaklaşımıyla gerçekleştirmeyi hedefler.

### Neden Godot?

Godot, açık kaynaklı, MIT lisanslı ve Godot 4 sürümüyle birlikte GDExtension API'sini destekleyen modern bir oyun motorudur. Unity DOTS'un aksine, Godot'un yerleşik bir ECS sistemi bulunmamaktadır. Bu durum araştırma açısından değerlidir: DOD prensiplerini **motor sağlamadan**, sıfırdan uygulamak, kavramın özünü daha net ortaya koyar.

### Benchmark Senaryosu

Her test koşusunda:
- N adet entity (1.000 / 5.000 / 10.000 / 50.000 / 100.000) ekrana yerleştirilir
- Her entity her frame rastgele bir başlangıç açısına sahip sabit hızda hareket eder
- Ekran kenarlarına çarptığında yansır (bounce)
- 3 saniyelik warmup süresi ardından 10 saniye boyunca frame time, FPS ve bellek ölçülür
- Sonuçlar CSV dosyasına yazılır (ortalama, min, maks, standart sapma, FPS, bellek)

---

## 2. Godot OOP ile Karşılaştırma

### OOP Mimarisi (godot/dod-benchmark-oop)

Godot OOP yaklaşımında her entity bağımsız bir `Node2D` nesnesidir. `EntityMover` sınıfı kendi konumunu, hızını ve rengini encapsulate eder; kendi hareketini ve çizimini yönetir:

```gdscript
# EntityMover.gd — her entity bağımsız bir Node2D nesnesidir
class_name EntityMover extends Node2D

var velocity: Vector2 = Vector2.ZERO
var screen_min: Vector2 = Vector2.ZERO
var screen_max: Vector2 = Vector2.ZERO
var _color: Color = Color.WHITE

func _process(delta: float) -> void:
    position += velocity * delta
    if position.x < screen_min.x or position.x > screen_max.x:
        velocity.x = -velocity.x
        ...

func _draw() -> void:
    draw_rect(Rect2(-4.0, -4.0, 8.0, 8.0), _color)
```

#### OOP'un Performans Maliyetleri

**1. Sahne ağacı traversal overhead'i:**
Godot'un `_process()` çağrı mekanizması sahne ağacını (SceneTree) gezer. N entity için N adet `Node2D._process()` çağrısı yapılır. Her çağrı sanal fonksiyon (virtual dispatch) mekanizmasını kullanır; bu, CPU'nun dal tahmini (branch prediction) için ek maliyet anlamına gelir.

**2. Nesne başına bellek erişim düzensizliği (Array of Structs):**
Her `EntityMover` nesnesi heap üzerinde farklı bir adrese yerleşir. Hareket döngüsü 100.000 entity için 100.000 farklı bellek bölgesine erişir. Modern CPU'ların L1/L2 önbelleği (cache) genellikle 32–256 KB'dır. Her nesne atlama, büyük olasılıkla bir **cache miss** üretir ve hafıza veri yolu (memory bus) işlemciyi bekletir.

**3. `_draw()` çağrı sayısı:**
N entity = N adet `_draw()` çağrısı. Her çağrı Godot'un CanvasItem rendering pipeline'ını tetikler.

### DOD Mimarisi (godot/dod-benchmark-dod)

DOD yaklaşımında entity kavramı tamamen ortadan kalkar. Veriler ayrı, bitişik dizilerde (Struct of Arrays — SoA) tutulur; C++ kodu tek bir döngüyle tüm diziyi toplu işler:

```cpp
// movement_world.h — entity yoktur, yalnızca bitişik diziler vardır
std::vector<float> pos_x;   // tüm x pozisyonları ardışık
std::vector<float> pos_y;   // tüm y pozisyonları ardışık
std::vector<float> vel_x;   // tüm x hızları ardışık
std::vector<float> vel_y;   // tüm y hızları ardışık
std::vector<float> col_r;   // tüm kırmızı değerleri ardışık
std::vector<float> col_g;
std::vector<float> col_b;
```

```cpp
// movement_world.cpp — tek bir döngü, ardışık bellek erişimi
void MovementWorld::update(double delta, Vector2 screen_min, Vector2 screen_max) {
    const float dt = static_cast<float>(delta);
    for (int i = 0; i < entity_count; ++i) {
        float px = pos_x[i] + vel_x[i] * dt;
        float py = pos_y[i] + vel_y[i] * dt;
        // bounce mantığı...
        pos_x[i] = px;
        pos_y[i] = py;
    }
}
```

#### OOP → DOD Dönüşüm Tablosu

| Kavram | Godot OOP | Godot DOD |
|---|---|---|
| Entity temsili | `EntityMover` nesnesi (Node2D) | Dizi indeksi (int) |
| Veri depolama | Her nesnenin kendi alanları (AoS) | Ayrı bitişik diziler (SoA) |
| Hareket mantığı | `EntityMover._process()` (N çağrı) | `MovementWorld::update()` (1 C++ döngüsü) |
| Çizim | N × `Node2D._draw()` | 1 × `MultiMeshInstance2D` (tek draw call) |
| Node sayısı | N + 1 (EntityMover'lar + BenchmarkManager) | 2 (BenchmarkOrchestrator + MultiMeshInstance2D) |
| Bellek düzeni | Rastgele heap adresleri (cache-unfriendly) | Bitişik diziler (cache-friendly) |
| Dil | GDScript | C++ (hot loop) + GDScript (orchestration) |

---

## 3. Unity DOTS ile Karşılaştırma

### Unity DOTS Mimarisi (unity/DODBenchmark-DOTS)

Unity DOTS (Data-Oriented Technology Stack), Unity'nin resmi ECS (Entity Component System) çerçevesidir. Bileşenler `IComponentData` yapıları olarak tanımlanır; sistemler `ISystem` arayüzünü uygular ve Burst Compiler ile SIMD optimizasyonu otomatik olarak uygulanır:

```csharp
// Velocity.cs — saf veri bileşeni
public struct Velocity : IComponentData {
    public float2 Value;
}

// MovementSystem.cs — Burst derlenmiş sistem
[BurstCompile]
public partial struct MovementSystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        foreach (var (transform, velocity, bounds) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRW<Velocity>, RefRO<ScreenBounds>>())
        {
            // tüm entity'ler tek döngüde işlenir
        }
    }
}
```

### Unity DOTS vs Godot DOD: Benzerlikler

| Özellik | Unity DOTS | Godot DOD (native) |
|---|---|---|
| Veri düzeni | SoA (ECS chunk'ları) | SoA (std::vector dizileri) |
| Hareket döngüsü | Tek sistem döngüsü | Tek C++ döngüsü |
| Render | RenderMeshUtility + GPU instancing | MultiMesh + GPU instancing |
| Node/GameObject başına maliyet | Yok (saf struct) | Yok (dizi indeksi) |

### Unity DOTS vs Godot DOD: Kritik Farklar

#### 1. Burst Compiler vs Standart C++

Unity DOTS'un en önemli avantajı **Burst Compiler**'dır. Burst, C# kodunu LLVM tabanlı bir derleyiciyle native koda çevirir ve **otomatik SIMD (Single Instruction Multiple Data)** vektörizasyonu uygular. Bir döngüdeki 4 float işlemi, modern CPU'larda SSE/AVX komut setiyle tek clock cycle'da işlenebilir.

Godot DOD implementasyonumuz standart C++17 ile derlenmiştir. Derleyici optimizasyonları (`/O2` veya `-O2`) etkinleştirildiğinde modern C++ derleyicileri de otomatik vektörizasyon uygulayabilir; ancak bu Unity Burst'ün sağladığı garantili vektörizasyon düzeyine ulaşmayabilir.

#### 2. Bellek Düzeni Kontrolü

Unity DOTS, entity bileşenlerini **chunk** adı verilen 16 KB'lık bloklara yerleştirir. Aynı arketipi (component kümesini) paylaşan entity'ler her zaman aynı chunk'ta bulunur. Bu, önbellek kullanımını maksimize eder.

Godot DOD implementasyonumuzda `std::vector<float>` kullanılmıştır. `std::vector`, heap üzerinde tek bitişik blok tahsis eder; bu nedenle `pos_x` dizisinin tamamı ardışık bellekte bulunur. Bu, chunk tabanlı yaklaşımdan farklı ama pratik açıdan benzer önbellek dostu bir düzendir.

#### 3. Motor Entegrasyonu

Unity DOTS, motor ile tam entegre çalışır: `LocalTransform`, `RenderMeshUtility`, `SystemAPI` hepsi DOTS farkındadır. Godot'ta böyle bir entegrasyon yoktur; bu nedenle **hybrid yaklaşım** seçilmiştir: C++ tarafı hesaplamayı yapar, GDScript render'ı üstlenir.

#### 4. Geliştirici Deneyimi ve Kurulum Karmaşıklığı

Unity DOTS, Unity editörüyle entegre gelir; paket yöneticisinden `com.unity.entities` eklenmesi yeterlidir. Godot DOD için GDExtension altyapısı gerektirir: Python, SCons, C++ derleyici (VS2022), godot-cpp submodule ve derleme adımları. Bu karmaşıklık farkı araştırmada bağımsız bir değişken olarak ele alınabilir.

---

## 4. Mimari Kararlar ve Gerekçeleri

### Karar 1: GDScript DOD yerine C++ GDExtension

**İlk yaklaşım:** GDScript içinde `PackedVector2Array` ile SoA ve `MovementProcessor` adlı pure-function sınıf.

**Neden değiştirildi:** GDScript yorumlamalı (interpreted) bir dildir. `PackedVector2Array` indekslemesi copy-on-write semantiğiyle çalışır; her indeks ataması potansiyel olarak bir kopya tetikleyebilir. Daha önemlisi, GDScript'in kendisi DOD prensiplerini uygulasa da JIT derleyicisi olmadığı için hot loop'un maliyeti yüksek kalır. Tezin iddiası "DOD'un cache ve CPU verimliliğini artırdığı"dır; bu iddiayı GDScript ile kanıtlamak güçtür çünkü dil başlı başına performans tavanı oluşturur.

**Seçilen yaklaşım:** C++ GDExtension. Hot loop (hareket hesabı) native C++ olarak derlenir; derleyici optimizasyonları (inlining, auto-vectorization, register kullanımı) tam anlamıyla devreye girer. Bu, DOD'un donanım düzeyindeki etkisini izole etmeye olanak tanır.

### Karar 2: Hybrid Render (C++ compute + GDScript MultiMesh)

**Alternatif 1 — Tam native render:**
C++ tarafında `RenderingServer` API'sini kullanarak her entity için mesh instance oluşturmak. Bu yaklaşım daha fazla GDExtension API çağrısı gerektirdiğinden, kod karmaşıklığı artar ve Godot sürüm değişikliklerine karşı kırılgan olur.

**Alternatif 2 — Hybrid (seçilen):**
C++ tarafı SoA hareket hesabını yapar ve `get_buffer()` ile `MultiMesh` için hazır bir `PackedFloat32Array` üretir. GDScript bu buffer'ı tek satırla `MultiMesh.buffer`'a atar. `MultiMeshInstance2D` tüm entity'leri tek GPU draw call ile çizer.

**Gerekçe:** Bu yaklaşımda render maliyeti OOP ile karşılaştırılabilir bir taban oluşturur (MultiMesh her iki sistemde de kullanılabilirdi); darboğaz CPU tarafındaki hareket hesabına izole edilir. Ayrıca `buffer.ptrw()` ile doğrudan bellek yazımı, GDScript↔C++ sınırında kopyayı minimize eder.

### Karar 3: `std::vector<float>` ile SoA

**Alternatif — tek `std::vector<Entity>` (AoS):**
```cpp
struct Entity { float px, py, vx, vy, cr, cg, cb; };
std::vector<Entity> entities;
```
Bu düzen OOP yaklaşımının hafıza düzeniyle özdeştir. Hareket döngüsü sırayla `px` ve `vx`'e erişir; ancak bunların arasında `py`, `vy`, `cr`, `cg`, `cb` alanları da bulunur. CPU önbellek satırına (cache line, genellikle 64 byte) `px` ve `vx` birlikte sığmayabilir.

**Seçilen — SoA:**
```cpp
std::vector<float> pos_x;  // sadece x pozisyonları
std::vector<float> vel_x;  // sadece x hızları
```
Döngü `pos_x[i]` ve `vel_x[i]`'ye eriştiğinde, her iki dizi de ardışık olduğundan CPU prefetcher (ön yükleyici) sonraki elemanları önbelleğe önceden yükler. 100.000 float için `pos_x` dizisi 400 KB'dır; L2 önbelleği (genellikle 256 KB–4 MB) bu dizi için optimize biçimde çalışır.

---

## 5. Teknik Bileşenler ve Sorumlulukları

Proje Tek Sorumluluk İlkesi (Single Responsibility Principle) doğrultusunda yapılandırılmıştır. Her sınıf veya modül yalnızca bir işten sorumludur.

```
godot/dod-benchmark-dod/
├── src/                          ← C++ GDExtension (native hot path)
│   ├── movement_world.h          → SoA veri tanımı + API arayüzü
│   ├── movement_world.cpp        → spawn / update (hot loop) / get_buffer
│   ├── register_types.h          → Godot sınıf kayıt arayüzü
│   └── register_types.cpp        → Godot ClassDB kaydı + entry symbol
├── SConstruct                    → SCons derleme betiği
├── godot-cpp/                    → Godot C++ binding submodule (4.6 branch)
└── demo/                         ← Godot projesi
    ├── bin/movementdod.gdextension → eklenti tanımı + kütüphane yolları
    ├── scenes/Main.tscn           → tek sahneli minimal giriş noktası
    └── scripts/
        ├── BenchmarkOrchestrator.gd → test akışı + MultiMesh render
        ├── BenchmarkHUD.gd          → UI paneli (signal tabanlı, saf görüntü)
        ├── FrameStatistics.gd       → frame time istatistikleri
        ├── CsvExporter.gd           → CSV serileştirme + dosya IO
        └── BenchmarkResult.gd       → sonuç veri yapısı (saf veri)
```

### Sorumluluk Dağılımı

| Bileşen | Sorumluluk | Dil |
|---|---|---|
| `MovementWorld` | SoA veri deposu + hareket hesabı + buffer üretimi | C++ |
| `BenchmarkOrchestrator` | Test akışı koordinasyonu + MultiMesh güncelleme | GDScript |
| `BenchmarkHUD` | Kullanıcı arayüzü (sadece görüntü, mantık yok) | GDScript |
| `FrameStatistics` | Frame time toplama + istatistik hesaplama | GDScript |
| `CsvExporter` | CSV formatı + FileAccess IO | GDScript |
| `BenchmarkResult` | Sonuç veri yapısı | GDScript |

---

## 6. Veri Modeli: SoA (Struct of Arrays)

DOD'un temel iddiası, verinin bellekte nasıl düzenlendiğinin performansı doğrudan etkilediğidir. Bu kavramı somutlaştırmak için iki düzeni karşılaştıralım.

### AoS (Array of Structs) — OOP'ta verinin bulunduğu düzen

```
Bellek adresleri (her EntityMover heap üzerinde ayrı yerde):

[EntityMover_0]: vel.x | vel.y | pos.x | pos.y | color.r | color.g | color.b
                 ↑ Adres: 0x2A00...
[EntityMover_1]: vel.x | vel.y | pos.x | pos.y | color.r | color.g | color.b
                 ↑ Adres: 0x3F10...  (farklı heap bloğu)
[EntityMover_2]: vel.x | vel.y | pos.x | pos.y | color.r | color.g | color.b
                 ↑ Adres: 0x1B80...  (yine farklı adres)
```

Hareket hesabı için `vel.x` ve `pos.x`'e erişilir. Ancak bu iki alan aynı nesnede yan yana olsa da, **farklı nesneler** birbirinden uzak bellek adreslerindedir. CPU `EntityMover_0`'ın verisini önbelleğe yüklediğinde aynı cache line'da yalnızca `EntityMover_0`'a ait veriler bulunur; `EntityMover_1` için yeni bir cache miss yaşanır.

### SoA (Struct of Arrays) — DOD implementasyonumuzda veri düzeni

```
Bellek adresleri (std::vector garantili bitişik bloklar):

pos_x[]: [0.0 | 128.4 | 640.1 | 300.5 | 512.0 | ...]  ← tek bitişik blok
pos_y[]: [0.0 | 256.8 | 480.2 | 100.3 | 360.0 | ...]  ← tek bitişik blok
vel_x[]: [150.0 | -200.0 | 175.0 | ...]                ← tek bitişik blok
vel_y[]: [200.0 | 150.0 | -180.0 | ...]                ← tek bitişik blok
```

`pos_x[i]` ve `vel_x[i]` aynı anda okunduğunda, CPU önbelleği `pos_x` dizisinin ardışık elemanlarını da önceden yükler (prefetch). 100.000 float için işlemci sayısız cache miss yerine akıcı bir bellek akışı yaşar.

### Cache Hattı Verimliliği Hesabı

Bir x86-64 CPU'nun cache line boyutu 64 byte'tır; bu 16 adet `float` değerine karşılık gelir.

- **AoS:** Hareket döngüsü her entity için yeni bir cache line yüklemek zorundadır. Entity başına ortalama 1 cache miss → 100.000 entity için ~100.000 cache miss.
- **SoA:** `pos_x` dizisindeki her cache line 16 entity'nin x pozisyonunu içerir → 100.000 entity için ~6.250 cache line yüklemesi; yani AoS'a göre teorik olarak **16× daha az cache miss**.

---

## 7. Hareket Sistemi: Hot Loop Analizi

`MovementWorld::update()` fonksiyonu, tüm benchmark süresince her frame çağrılan tek sıcak döngüdür:

```cpp
void MovementWorld::update(double delta, Vector2 screen_min, Vector2 screen_max) {
    const float dt = static_cast<float>(delta);
    // Sınır değerleri register'lara al (döngü içinde tekrar tekrar okunmasın)
    const float min_x = screen_min.x;
    const float min_y = screen_min.y;
    const float max_x = screen_max.x;
    const float max_y = screen_max.y;

    for (int i = 0; i < entity_count; ++i) {
        float px = pos_x[i] + vel_x[i] * dt;  // ardışık okuma
        float py = pos_y[i] + vel_y[i] * dt;  // ardışık okuma

        if (px < min_x) { px = min_x; vel_x[i] = -vel_x[i]; }
        else if (px > max_x) { px = max_x; vel_x[i] = -vel_x[i]; }

        if (py < min_y) { py = min_y; vel_y[i] = -vel_y[i]; }
        else if (py > max_y) { py = max_y; vel_y[i] = -vel_y[i]; }

        pos_x[i] = px;  // ardışık yazma
        pos_y[i] = py;  // ardışık yazma
    }
}
```

**Tasarım seçimleri:**

1. **Sınır değerleri döngü dışında yerel değişkene alınmıştır.** `screen_min.x` gibi değerler `Vector2` struct'ından her iterasyonda okunmak yerine stack'e alınır; derleyici bunları register'da tutabilir.

2. **`if/else if` dallanma yapısı.** Tek yönde çarpma (sola veya sağa) yalnızca bir test yeterlidir; `else if` ile gereç yok olan ikinci karşılaştırma atlanır.

3. **Yerel `float` değişkenleri (px, py).** `pos_x[i]` doğrudan iki kez güncellenmek yerine stack değişkenine alınır. Derleyici bu değişkeni register'da tutabilir; dizi belleğine geri yazma yalnızca bir kez yapılır.

4. **xorshift32 RNG.** `spawn()` sırasında rastgele başlangıç değerleri üretmek için `std::mt19937` yerine hafif bir xorshift32 kullanılmıştır. RNG performans-kritik bir yol değildir (yalnızca spawn sırasında çalışır), ancak bağımlılık azaltma ve deterministik test tekrarlanabilirliği açısından tercih edilmiştir.

---

## 8. Render Katmanı: MultiMesh Hybrid Yaklaşım

### get_buffer() ve MultiMesh Protokolü

Godot'un `MultiMesh` resource'u, tüm instance'ların transform ve renk verilerini tek bir `PackedFloat32Array` buffer içinde bekler. 2D transform formatında (`TRANSFORM_2D`) her instance 12 float kaplar:

```
Instance i için buffer layout (offset = i * 12):
  [o+0] = basis.x.x    [o+1] = basis.y.x    [o+2] = 0.0    [o+3] = origin.x (pos_x)
  [o+4] = basis.x.y    [o+5] = basis.y.y    [o+6] = 0.0    [o+7] = origin.y (pos_y)
  [o+8] = color.r      [o+9] = color.g      [o+10] = color.b  [o+11] = color.a (1.0)
```

Döndürme ve ölçekleme yoktur (`basis = identity`); yalnızca konum (origin) ve renk güncellenir.

```cpp
PackedFloat32Array MovementWorld::get_buffer() {
    buffer.resize(entity_count * 12);
    float *w = buffer.ptrw();  // doğrudan pointer; kopyasız yazım

    for (int i = 0; i < entity_count; ++i) {
        int o = i * 12;
        w[o+0]=1.f; w[o+1]=0.f; w[o+2]=0.f; w[o+3]=pos_x[i];
        w[o+4]=0.f; w[o+5]=1.f; w[o+6]=0.f; w[o+7]=pos_y[i];
        w[o+8]=col_r[i]; w[o+9]=col_g[i]; w[o+10]=col_b[i]; w[o+11]=1.f;
    }
    return buffer;
}
```

`buffer.ptrw()` Godot'un copy-on-write korumasını devre dışı bırakır ve doğrudan ham bellek işaretçisi döner. Bu, döngü içinde kopyasız yazım yapılmasını sağlar.

### GDScript Render Tarafı

```gdscript
# BenchmarkOrchestrator.gd — her frame yalnızca iki satır
_world.update(delta, _screen_min, _screen_max)   # C++ hot loop
_multimesh.buffer = _world.get_buffer()           # buffer ataması
```

`_multimesh.buffer` ataması Godot'a hazır buffer'ı iletir. Bundan sonra `MultiMeshInstance2D` tüm entity'leri GPU instancing ile **tek draw call** olarak çizer.

### OOP ile Draw Call Karşılaştırması

| | Godot OOP | Godot DOD |
|---|---|---|
| N = 1.000 | ~1.000 draw call (her Node2D ayrı) | 1 draw call |
| N = 100.000 | ~100.000 draw call | 1 draw call |
| GPU→CPU senkronizasyonu | N kez | 1 kez |

---

## 9. GDExtension Altyapısı

### Neden GDExtension?

Godot 4, native eklentiler için GDExtension API'sini sunar. Bu API, C++ sınıflarının Godot'un dahili tip sistemine (ClassDB) kayıt edilmesini sağlar. Kayıt edilen sınıflar GDScript'ten sanki yerleşik sınıflar gibi kullanılabilir:

```gdscript
# GDScript — MovementWorld sanki Godot'un kendi sınıfıymış gibi kullanılır
if ClassDB.class_exists("MovementWorld"):
    _world = ClassDB.instantiate("MovementWorld")
    _world.spawn(count, speed, screen_min, screen_max)
```

### Kayıt Mekanizması

```cpp
// register_types.cpp
void initialize_movementdod_module(ModuleInitializationLevel p_level) {
    if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) return;
    GDREGISTER_CLASS(MovementWorld);  // ClassDB'ye kayıt
}

extern "C" {
GDExtensionBool GDE_EXPORT movementdod_library_init(...) {
    // .gdextension dosyasındaki entry_symbol ile eşleşmeli
    ...
}
}
```

```ini
# movementdod.gdextension
[configuration]
entry_symbol = "movementdod_library_init"
compatibility_minimum = "4.4"

[libraries]
windows.debug.x86_64 = "res://bin/libmovementdod.windows.template_debug.x86_64.dll"
linux.debug.x86_64 = "res://bin/libmovementdod.linux.template_debug.x86_64.so"
```

### Derleme Zinciri

```
Python + SCons (build sistemi)
    └── godot-cpp (Godot 4.6 C++ binding)
            └── src/*.cpp  →  libmovementdod.*.dll/.so
                                └── demo/bin/  (Godot'un yüklediği)
```

```bash
# Kurulum (tek seferlik)
git submodule add -b 4.6 https://github.com/godotengine/godot-cpp godot-cpp
git submodule update --init --recursive

# Derleme (her kaynak değişikliğinde)
scons platform=windows target=template_debug
```

### Eklenti Yoksa: Graceful Degradation

Derleme yapılmadan Godot projesi açılırsa uygulama çökmez. `ClassDB.class_exists("MovementWorld")` kontrolü başarısız olur; UI paneli kilitlenir ve ekranda build talimatı gösterilir. Bu, geliştirme sürecinde esneklik sağlar.

---

## 10. Benchmark Metodolojisi

### Ölçüm Parametreleri

| Parametre | Değer | Gerekçe |
|---|---|---|
| VSync | KAPALI (`VSYNC_DISABLED`) | Açık olduğunda frame time ekran yenileme hızına kilitlenir; tüm entity sayıları için özdeş sonuç üretilir (ör. 165 Hz → 6.06 ms, StdDev = 0) |
| FPS sınırı | Yok (`Engine.max_fps = 0`) | Yapay tavan kaldırılır |
| Warmup süresi | 3 saniye | İlk spawn sonrası GC/JIT/shader derleme tepelerini atlatır |
| Ölçüm süresi | 10 saniye | Yeterli örnek sayısı; toplam 13 saniyelik test |
| Entity sayıları | 1K, 5K, 10K, 50K, 100K | Geniş mertebe aralığı; doğrusal/üstel ölçekleme farkı gözlemlenebilir |

### Toplanan Metrikler

- **Ortalama frame time (ms):** N frame'in aritmetik ortalaması
- **Min / Maks frame time (ms):** En iyi ve en kötü frame
- **Standart sapma (ms):** Frame-to-frame tutarlılık; düşük değer daha istikrarlı davranışı gösterir
- **Ortalama FPS:** 1000 / ortalama frame time
- **Min FPS:** 1000 / maks frame time
- **Bellek kullanımı (MB):** Test süresinin ortasında `Performance.MEMORY_STATIC` farkı

### CSV Çıktı Formatı

```
EntityCount,AvgFrameTime_ms,MinFrameTime_ms,MaxFrameTime_ms,StdDev_ms,
AvgFPS,MinFPS,MemoryUsed_MB,TotalFrames,Duration_s
```

Tüm sayısal değerler locale-bağımsız noktalı ondalık ayırıcı ile yazılır (GDScript `%` operatörü C-locale kullanır).

---

## 11. Bilinen Sınırlamalar ve Araştırma Notları

### 1. Bellek Ölçümünün Sınırlılığı

`Performance.MEMORY_STATIC`, Godot'un kendi yığın tahsislerini raporlar. C++ tarafındaki `std::vector` tahsisleri Godot'un bellek yöneticisi dışında gerçekleştiğinden bu metriğe **dahil değildir**. Dolayısıyla:

- **OOP:** Entity başına Node2D nesneleri Godot yığınında → bellek doğru ölçülür
- **DOD:** `std::vector` dizileri native yığında → bellek eksik ölçülür

Gerçek karşılaştırmalı bellek analizi için harici profiler (Windows: Visual Studio Profiler, Valgrind/Massif) kullanımı önerilir.

### 2. GDScript↔C++ Sınır Maliyeti

Her frame `get_buffer()` çağrısı ve `_multimesh.buffer = ...` ataması, C++→GDScript→Godot sınırını geçer. Bu sınır geçişinin maliyeti entity sayısıyla lineer olarak artar (buffer boyutu = N × 12 × 4 byte). Çok yüksek entity sayılarında (>500K) bu aktarım maliyeti, hareket hesabı maliyetini geçebilir.

### 3. Render Bottleneck Ayırımı

Bu benchmark, CPU-side hesabı (hareket) ve GPU-side render'ı birlikte ölçer. Entity sayısı düşükken CPU çok hızlı tamamlar; frame time darboğazı render olabilir. Entity sayısı yüksekken CPU bottleneck öne çıkar. OOP/DOD karşılaştırması temel olarak CPU davranışını hedefler; ancak her iki implementasyonda MultiMesh kullanımı render maliyetlerini eşitler.

### 4. Tekrar Edilebilirlik

Sonuçlar aynı makinede bile run'dan run'a farklılık gösterebilir: işletim sistemi zamanlayıcısı, arka plan süreçleri, termal kısıtlama (thermal throttling) etkilidir. Güvenilir karşılaştırma için:
- Her test koşusunu en az 3 kez tekrarlayın
- Ortalama ve standart sapma değerlerine birlikte bakın
- Yüksek StdDev değerleri ölçüm gürültüsüne işaret eder

### 5. Platform Bağımlılığı

`scons platform=windows target=template_debug` ile üretilen DLL'de derleyici optimizasyonları tam etkin değildir (`/Od` veya minimum optimizasyon). Release build için:

```bash
scons platform=windows target=template_release
```

Debug build ile release build arasındaki performans farkı %20–200 olabilir; tez karşılaştırmaları için aynı build tipini tutarlı kullanmak önemlidir.
