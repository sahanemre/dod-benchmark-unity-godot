# Godot DOD Benchmark (native C++ GDExtension)

Tez icin Godot tarafindaki **Data-Oriented Design** prototipi. Hareket
hesabi (SoA, sicak dongu) C++ tarafinda bir GDExtension olarak calisir;
render Godot'ta GDScript + tek `MultiMesh` ile yapilir (hybrid yaklasim).

## Mimari

```
dod-benchmark-dod/
├── src/                     ← C++ kaynak (GDExtension)
│   ├── movement_world.h     ← SoA veri + arayuz
│   ├── movement_world.cpp   ← spawn / update (hot loop) / get_buffer
│   ├── register_types.h
│   └── register_types.cpp   ← Godot'a sinif kaydi + entry symbol
├── SConstruct               ← derleme scripti (cikti: demo/bin/)
├── godot-cpp/               ← submodule (lokalde eklenir, repoda yok)
└── demo/                    ← asil Godot projesi (bunu Godot'ta acin)
    ├── project.godot
    ├── bin/movementdod.gdextension
    ├── scenes/Main.tscn
    └── scripts/
        ├── BenchmarkOrchestrator.gd  ← akis + MultiMesh render (hybrid)
        ├── BenchmarkHUD.gd
        ├── FrameStatistics.gd
        ├── CsvExporter.gd
        └── BenchmarkResult.gd
```

### Neden hybrid?
- **C++ (MovementWorld):** `pos_x, pos_y, vel_x, vel_y, col_*` ayri dizilerde
  (Struct of Arrays). `update()` tum entity'leri tek ardisik dongude isler —
  cache dostu, gercek DOD. `get_buffer()` `MultiMesh`'in bekledigi 2B
  transform+renk dizisini (12 float/instance) uretir.
- **GDScript (orchestrator):** her frame `world.update()` cagirir, donen
  buffer'i `MultiMesh`'e verir; tek `MultiMeshInstance2D` tum entity'leri
  **tek draw call** ile cizer.

## Kurulum gereksinimleri

- Python 3.x
- SCons: `pip install scons`
- C++ derleyici:
  - **Windows:** Visual Studio 2022 (Desktop development with C++ workload)
  - Linux: gcc/clang, macOS: Xcode command line tools
- Godot 4.6.x

## Derleme adimlari

1. godot-cpp submodule'unu ekle (Godot surumunle ESLESEN branch — onemli):
   ```bash
   cd godot/dod-benchmark-dod
   git submodule add -b 4.6 https://github.com/godotengine/godot-cpp godot-cpp
   git submodule update --init --recursive
   ```

2. Eklentiyi derle (ilk derleme godot-cpp dahil 5-10 dk surer):
   ```bash
   # Windows
   scons platform=windows target=template_debug
   # Linux
   scons platform=linux target=template_debug
   # macOS
   scons platform=macos target=template_debug
   ```
   Cikti `demo/bin/` altina yazilir (orn.
   `libmovementdod.windows.template_debug.x86_64.dll`).

3. `demo/` klasorunu Godot'ta ac ve `Main.tscn`'i calistir.
   `bin/movementdod.gdextension` derlenen kutuphaneyi otomatik yukler.

> Native eklenti henuz derlenmemisse proje yine acilir; panel kilitlenir ve
> ekranda derleme talimati gosterilir (`MovementWorld` sinifi bulunamaz).

## Olcum notlari

- Warmup **3 sn**, olcum **10 sn**, toplam **13 sn** (diger projelerle ayni).
- CSV `user://` altina `benchmark_godot_dod_native_*.csv` olarak yazilir
  (Windows: `%APPDATA%/Godot/app_userdata/...`). Nokta ondalik ayirici,
  diger projelerle ayni sutun duzeni.
- **Bellek:** `Performance.MEMORY_STATIC` native `std::vector` tahsislerini
  KAPSAMAZ. Native bellek karsilastirmasi gerekiyorsa harici profiler kullan;
  CSV'deki bellek sutunu yalnizca Godot tarafi icindir.
