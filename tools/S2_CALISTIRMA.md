# S2 (Çarpışma) — Çalıştırma Rehberi

S2, S1'in devamıdır: dört yapılandırmada (Unity OOP, Unity DOTS, Godot OOP,
Godot DOD) **aynı çarpışma algoritması** elle yazılmıştır. Motorların fizik
sistemleri (Rigidbody, Collider, Area2D, PhysicsServer, Unity Physics)
kullanılmaz. S1 koduna ve sonuçlarına dokunulmadı; S2 `--scenario=S2` ile
seçilir.

## Algoritma (her kare, bu sırayla)

1. **Hareket:** S1 ile aynı (`pos += vel·dt`; duvarda hızın işareti döner,
   pozisyon sınıra kıstırılır).
2. **Broad-phase:** Uniform grid, hücre = 2r. Her kare sıfırdan counting sort
   ile kurulur: hücre başına sayım → prefix sum → indeks/referans dizisi.
   Dinamik liste ya da sözlük yoktur.
3. **Narrow-phase:** Entity i kendi hücresi + 8 komşu hücreye bakar (satır
   satır: dy = −1..1, dx = −1..1). Hücre içinde indeks sırasıyla, yalnızca
   j > i. Test: `d² < (2r)²`.
4. **Tepki:** Kesişen ve yaklaşan çiftte (`(vj − vi)·d < 0`) eşit kütleli
   elastik çarpışma: hızların normal bileşenleri değiş tokuş edilir
   (`f = ((vj−vi)·d)/d²`, `vi += f·d`, `vj −= f·d`). Çiftler sırayla işlenir;
   bir çiftin yeni hızı sonraki çiftlerde kullanılır (Gauss-Seidel).
   Pozisyon düzeltmesi yoktur.
5. **Sayım:** Kare başına kesişen çift sayısı (CSV metriği) ve tepki verilen
   çift sayısı (doğrulama dosyalarında).

**Ortak parametreler (dört yapılandırmada aynı sayılar):**

| Parametre | Değer |
|---|---|
| Dünya | Godot'un mantıksal 1280×720 alanı; hareket sınırları [10, 1270] × [10, 710] |
| r | 3,375 (S1 parity entity'si 6,75) |
| Hücre | 6,75 (= 2r; 187 × 104 hücre) |
| Hız | 300 birim/s |

Unity'de S2 kamerası bu alanı gösterir (orthographic size 360, merkez
640,360); ekranda S1 parity ile aynı boyuttur.

**Başlangıç durumu:** Dört dilde aynı yazılmış ortak RNG kullanılır (seed
murmur3 fmix32 ile karıştırılır, sonra xorshift32). Seed = 1000 + run_index.
Entity başına 6 çekiliş yapılır: x, y, açı, r, g, b. Hız
`(float)cos((double)açı)·300` ile hesaplanır.

**Paradigma farkı (tek fark):**

| | OOP | DOD |
|---|---|---|
| Veri | Her entity kendi nesnesinde (`pos`, `vel` alanları); motorun transform'u yalnızca çizim için karede bir kez yazılır | SoA dizileri (`px`, `py`, `vx`, `vy`) |
| Grid | Nesne **referansları** | Entity **indeksleri** |
| Unity | `S2OopEntity` (MonoBehaviour, kendi `Update`'inde hareket) + `S2OopManager.LateUpdate`'te `S2OopGrid` | S1 `MovementSystem` + `S2CollisionSystem` (Burst, ana iş parçacığı, Job System yok; chunk'lardan SoA'ya toplama/geri yazma ölçüme dahil) |
| Godot | `EntityS2` (Node2D, kendi `_process`'inde hareket) + `S2ManagerOOP` (`process_priority = 1`, entity'lerden sonra) | `MovementWorld.update` (S1) + `MovementWorld.collide` (C++) |

**Referans uygulama:** `tools/s2_reference/s2_reference.c`, motorsuz C
kodudur ve aynı float32 işlem sırasını izler. `expected/` klasöründe N=1000 ve
N=10000 için (run 1, 300 kare) beklenen çıktılar vardır. Her iki N'de de grid
sonucu O(N²) brute force ile 300/300 karede aynıdır.

## Kontrol edilenler (geliştirme ortamı)

| Yapılandırma | N=1000 (300 kare) | N=10000 (300 kare) | Nasıl |
|---|---|---|---|
| Godot DOD | referansla 300/300 birebir; O(N²) 300/300 | 300/300 birebir | Gerçek Godot 4.6.2 (headless) |
| Godot OOP | 300/300 birebir | 300/300 birebir | Gerçek Godot 4.6.2 (headless) |
| Unity OOP | 300/300 birebir | 300/300 birebir | Çekirdek kod mono ile (Unity olmadan) |
| Unity DOTS | 300/300 birebir | 300/300 birebir | `S2Collision` çekirdeği mono ile (Burst olmadan) |

Unity build'inde Burst/IL2CPP float farkları küçük sapmalar üretebilir.
Gerçek sonuç için aşağıdaki doğrulama koşusunu çalıştır.

## 1. Build'ler

Yalnızca parity build'leri gerekir; published build'lere dokunma.

| Yapılandırma | Ne yapılacak | Klasör |
|---|---|---|
| Godot DOD | `cd godot\dod-benchmark-dod` → `scons platform=windows target=template_release`; sonra `demo` projesini **release** (Export With Debug kapalı) export et | `Documents\s1-builds\godot-dod-release\` |
| Godot OOP | **Release** export | `Documents\s1-builds\godot-oop-release\` |
| Unity OOP | Projeyi aç (yeni scriptler derlenir), Development Build kapalı, Build | `Documents\s1-builds\unity-oop\` |
| Unity DOTS | Aynı şekilde | `Documents\s1-builds\unity-dots\` |

- `template_debug`'ı yeniden derleme. Published Godot DOD, makaledeki DLL'i
  kullanır.
- Godot export'u yeni `scenes/S2Main.tscn` sahnesini ve scriptleri
  kendiliğinden içerir; ana sahne (S1) değişmedi.
- Aynı build'ler S1'de de kullanılır. `--scenario` verilmezse S1 eskisi gibi
  çalışır.

## 2. Doğrulama (ölçümden önce zorunlu)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Scenario S2 -Validate
```

Dört yapılandırma, N=1000 ve 10000, 300 kare, sabit dt = 1/60 ile koşar
(8 koşu, birkaç dakika; Godot OOP 10K yaklaşık 15 s). Runner şu tabloları
basar:
- kare başına kesişen çift sayısı: referans ve dört yapılandırma yan yana,
- her yapılandırmanın referansla birebir aynı kare sayısı, ilk fark karesi,
  ilk 50 karede ve tümünde en büyük |fark|,
- O(N²) kontrol sonucu.

Tablo `results\benchmarks\s2_validate\validate_summary.txt` dosyasına da
yazılır.

**Kabul ölçütü:**
- İlk ~50 karede fark 0 ya da çok küçük olmalı.
- Sonrasında float farkları kaotik sistemde büyüyebilir; birkaç çiftlik
  sapma kabul edilir.
- O(N²) hatası 0 olmalı.

## 3. Smoke test (4 yapılandırma, N = 1000 ve 10000, 1 koşu)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Scenario S2 -N 1000,10000 -Runs 1 -OutDir results\benchmarks\s2_smoke
```

8 koşu, yaklaşık 4 dakika. Hepsi `OK` olmalı. Birleşik CSV 17 sütunludur
(`scenario=S2`, son sütun `collision_pairs_mean`).

## 4. Tam S2 ölçümü (parity, 4 yapılandırma × 5 N × 10 koşu)

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_s1.ps1 -Scenario S2
```

Çıktı `results\benchmarks\s2\` altına gider. 200 koşu, tahmini
**1,5–2 saat**. Her koşu 13 s simülasyon süresidir; Godot OOP yüksek N'de
kare başına saniyeler sürebilir, bu yüzden olası uzun açılış/spawn için
S2'de varsayılan zaman aşımı 600 s'dir. Koşuyu kesme.

## Notlar ve kısıtlar

- **Kare başına çift sayısı yapılandırmalar arasında farklı olur.** Ölçüm
  modunda simülasyon, S1'deki gibi gerçek kare süresiyle (değişken dt)
  ilerler. Kare süresi kısa olan yapılandırma çakışmaları daha erken yakalar
  ve çiftler daha çabuk ayrılır. Bu yüzden `collision_pairs_mean`
  yapılandırmalar arasında farklıdır; bu fark algoritmadan değil, dt'den
  gelir. Algoritmanın aynılığı sabit dt'li doğrulama modunda gösterilir.
- **Yüksek N'de yoğunluk çok yüksek.** N=100.000'de alanın kapsama oranı
  yaklaşık %406'dır: entity'ler üst üste biner (kare başına yüz binlerce
  kesişen çift).
- **Godot OOP doğrulamasında O(N²) sütunu yoktur.** GDScript'te 300 kare
  brute force çok yavaştır. Aynı kontrol referans, Godot DOD ve Unity'de
  yapılır.
- **Godot OOP'ta ölçülen kare sayısı az olabilir.** Yüksek N'de kare başına
  birkaç saniye sürebilir; 10 s ölçüm penceresinde birkaç kare olabilir.
  Runner süreyi raporlar.
