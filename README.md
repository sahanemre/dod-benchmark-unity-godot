# Comparative Analysis of Data-Oriented Design Performance in Game Engines
## A Case Study of Unity DOTS and Godot

> **Yüksek Lisans Tezi** — Bilgisayar Mühendisliği  
> **Yazar:** Muhammed Emre ŞAHAN  
> **Danışman:** [Danışman Adı]  
> **Hedef Mezuniyet:** Ocak 2027

---

## Proje Hakkında

Bu proje, oyun motorlarında **Data-Oriented Design (DOD)** ve geleneksel **Object-Oriented Programming (OOP)** yaklaşımlarının performans karşılaştırmasını yapmaktadır. Unity DOTS/ECS ve Godot GDExtension/C++ kullanılarak aynı benchmark senaryoları her iki motorda implemente edilmiş ve karşılaştırılmıştır.

## Test Yapılandırmaları

| # | Motor | Yaklaşım | Uygulama |
|---|-------|----------|----------|
| 1 | Unity | OOP | MonoBehaviour + C# |
| 2 | Unity | DOD | DOTS / ECS + Burst + Job System |
| 3 | Godot 4 | OOP | Node2D + GDScript |
| 4 | Godot 4 | DOD | GDExtension + C++ (Struct of Arrays) |

## Benchmark Senaryoları

- **S1: Toplu Hareket** — N entity'nin pozisyonlarını velocity ile güncelleme
- **S2: Collision Detection** — N-body yakın komşu kontrolü (AABB)
- **S3: Spawn/Despawn** — Entity oluşturma ve silme döngüsü

Her senaryo 1K, 5K, 10K, 50K, 100K entity ile test edilmektedir.

## Ölçülen Metrikler

- Frame time (ms/frame)
- FPS (frames per second)
- CPU time — update loop (ms)
- Bellek kullanımı (MB)
- L1/L2 cache miss oranı
- Ölçeklenme eğrisi (entity count vs frame time)

## Proje Yapısı

```
├── unity/
│   ├── DODBenchmark-OOP/        # Unity OOP projesi
│   └── DODBenchmark-DOTS/       # Unity DOTS projesi
├── godot/
│   ├── dod-benchmark-oop/       # Godot OOP projesi
│   └── dod-benchmark-dod/       # Godot DOD (GDExtension C++)
├── results/
│   └── benchmarks/              # Benchmark sonuçları (CSV)
├── paper/
│   └── ubmk-2026/               # UBMK 2026 makale dosyaları
└── .gitignore
```

## Donanım

- **CPU:** AMD Ryzen 5 5600H (12 CPUs, 3.3GHz)
- **RAM:** 16GB
- **GPU:** AMD Radeon Graphics (entegre)
- **OS:** Windows 11

## Sürümler

- Unity: 2022.3.x LTS (DOTS 1.0)
- Godot: 4.6.2 stable
- GDExtension: godot-cpp 4.6.2

## Yayınlar

- **UBMK 2026** — [durum güncellenecek]

## Lisans

Bu proje akademik araştırma amaçlı açık kaynak olarak paylaşılmaktadır.  
MIT License

---

*Bu repo, yüksek lisans tezi kapsamında sürekli güncellenmektedir.*
