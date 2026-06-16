"""
Benchmark Analysis Script
Reads benchmarkresults.xlsx, computes 3-run averages,
produces comparison table + frame-time and FPS charts.
"""

import openpyxl
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np
from pathlib import Path

XLSX = Path(__file__).parent.parent / "results" / "benchmarks" / "benchmarkresults.xlsx"
OUT  = Path(__file__).parent.parent / "results" / "figures"
OUT.mkdir(parents=True, exist_ok=True)

# ── 1. veri yukle ──────────────────────────────────────────────────────────────
SHEET_GROUPS = {
    "Unity OOP":  ["unity-oop-1",  "unity-oop-2",  "unity-oop-3"],
    "Unity DOTS": ["unity-dod-1",  "unity-dod-2",  "unity-dod-3"],
    "Godot OOP":  ["godot-oop-1",  "godot-oop-2"],          # 2 kosuda kaldi
    "Godot DOD":  ["godot-dod-1",  "godot-dod-2",  "godot-dod-3"],
}

COLORS = {
    "Unity OOP":  "#4C72B0",
    "Unity DOTS": "#55A868",
    "Godot OOP":  "#C44E52",
    "Godot DOD":  "#DD8452",
}

ENTITY_COUNTS = [1_000, 5_000, 10_000, 50_000, 100_000]

wb = openpyxl.load_workbook(str(XLSX), data_only=True)

def load_sheet(name) -> pd.DataFrame:
    ws = wb[name]
    rows = list(ws.iter_rows(values_only=True))
    df = pd.DataFrame(rows[1:], columns=rows[0])
    df = df.map(lambda x: float(str(x).replace(",", ".")) if x is not None else None)
    df["EntityCount"] = df["EntityCount"].astype(int)
    return df.set_index("EntityCount")

def avg_runs(sheets: list[str], col: str) -> pd.Series:
    frames = [load_sheet(s)[col] for s in sheets]
    return pd.concat(frames, axis=1).mean(axis=1)

# 3-run ortalamalari
avg = {}
for label, sheets in SHEET_GROUPS.items():
    avg[label] = {
        "AvgFrameTime_ms": avg_runs(sheets, "AvgFrameTime_ms"),
        "AvgFPS":          avg_runs(sheets, "AvgFPS"),
        "StdDev_ms":       avg_runs(sheets, "StdDev_ms"),
        "MemoryUsed_MB":   avg_runs(sheets, "MemoryUsed_MB"),
    }

# ── 2. ozet tablo ──────────────────────────────────────────────────────────────
print("\n" + "="*80)
print("ORTALAMA FRAME TIME (ms)  [3-kosuluk ortalama]")
print("="*80)
header = f"{'Entity':>8} | {'Unity OOP':>10} | {'Unity DOTS':>11} | {'Godot OOP':>10} | {'Godot DOD':>10}"
print(header)
print("-"*len(header))
for ec in ENTITY_COUNTS:
    row = f"{ec:>8,} | "
    for lbl in ["Unity OOP","Unity DOTS","Godot OOP","Godot DOD"]:
        v = avg[lbl]["AvgFrameTime_ms"][ec]
        row += f"{v:>10.2f} | "
    print(row)

print("\n" + "="*80)
print("ORTALAMA FPS")
print("="*80)
print(header.replace("FRAME TIME (ms)","FPS").replace(
    "Unity OOP","Unity OOP").replace("AvgFrameTime_ms","AvgFPS"))
print(header)
print("-"*len(header))
for ec in ENTITY_COUNTS:
    row = f"{ec:>8,} | "
    for lbl in ["Unity OOP","Unity DOTS","Godot OOP","Godot DOD"]:
        v = avg[lbl]["AvgFPS"][ec]
        row += f"{v:>10.1f} | "
    print(row)

print("\n" + "="*80)
print("PARADIGMA KAZANIM: DOD/OOP hiz orani  (OOP frame time / DOD frame time)")
print("="*80)
print(f"{'Entity':>8} | {'Unity DOTS/OOP':>15} | {'Godot DOD/OOP':>14}")
print("-"*46)
for ec in ENTITY_COUNTS:
    u = avg["Unity OOP"]["AvgFrameTime_ms"][ec] / avg["Unity DOTS"]["AvgFrameTime_ms"][ec]
    g = avg["Godot OOP"]["AvgFrameTime_ms"][ec]  / avg["Godot DOD"]["AvgFrameTime_ms"][ec]
    print(f"{ec:>8,} | {u:>14.1f}x | {g:>13.1f}x")

# ── 3. grafik 1: frame time vs entity count (log-log) ─────────────────────────
fig, axes = plt.subplots(1, 2, figsize=(14, 6))
fig.suptitle("OOP vs DOD Benchmark — Frame Time & FPS Karşılaştırması",
             fontsize=14, fontweight="bold", y=1.01)

ax = axes[0]
for lbl, data in avg.items():
    y = [data["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    ax.plot(ENTITY_COUNTS, y, marker="o", label=lbl,
            color=COLORS[lbl], linewidth=2, markersize=6)

ax.set_xscale("log")
ax.set_yscale("log")
ax.set_xlabel("Entity Sayısı", fontsize=12)
ax.set_ylabel("Ortalama Frame Time (ms)", fontsize=12)
ax.set_title("Frame Time (ms) — log-log", fontsize=12)
ax.xaxis.set_major_formatter(mticker.FuncFormatter(
    lambda x, _: f"{int(x):,}"))
ax.yaxis.set_major_formatter(mticker.FuncFormatter(
    lambda x, _: f"{x:.1f}"))
ax.grid(True, which="both", alpha=0.3)
ax.legend(fontsize=11)

# referans cizgisi: lineer olcekleme
x_ref = np.array(ENTITY_COUNTS, dtype=float)
y_ref_base = avg["Unity OOP"]["AvgFrameTime_ms"][1000]
y_ref = y_ref_base * (x_ref / 1000)
ax.plot(ENTITY_COUNTS, y_ref, "k--", alpha=0.3, linewidth=1, label="Lineer ref.")

# ── 4. grafik 2: FPS vs entity count ─────────────────────────────────────────
ax2 = axes[1]
for lbl, data in avg.items():
    y = [data["AvgFPS"][ec] for ec in ENTITY_COUNTS]
    ax2.plot(ENTITY_COUNTS, y, marker="o", label=lbl,
             color=COLORS[lbl], linewidth=2, markersize=6)

ax2.set_xscale("log")
ax2.set_xlabel("Entity Sayısı", fontsize=12)
ax2.set_ylabel("Ortalama FPS", fontsize=12)
ax2.set_title("FPS — log x", fontsize=12)
ax2.xaxis.set_major_formatter(mticker.FuncFormatter(
    lambda x, _: f"{int(x):,}"))
ax2.axhline(60,  color="gray", linestyle="--", alpha=0.4, linewidth=1)
ax2.axhline(30,  color="gray", linestyle=":",  alpha=0.4, linewidth=1)
ax2.text(1100, 62, "60 FPS", fontsize=9, color="gray")
ax2.text(1100, 32, "30 FPS", fontsize=9, color="gray")
ax2.grid(True, which="both", alpha=0.3)
ax2.legend(fontsize=11)

plt.tight_layout()
out1 = OUT / "fig1_frametime_fps.png"
plt.savefig(out1, dpi=150, bbox_inches="tight")
print(f"\nGrafik kaydedildi: {out1}")

# ── 5. grafik 2: paradigma kazanim bar chart ──────────────────────────────────
fig2, ax3 = plt.subplots(figsize=(10, 5))
x = np.arange(len(ENTITY_COUNTS))
width = 0.35

unity_ratios = [avg["Unity OOP"]["AvgFrameTime_ms"][ec] /
                avg["Unity DOTS"]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
godot_ratios = [avg["Godot OOP"]["AvgFrameTime_ms"][ec] /
                avg["Godot DOD"]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]

bars1 = ax3.bar(x - width/2, unity_ratios, width, label="Unity (DOTS/OOP)",
                color=COLORS["Unity DOTS"], alpha=0.85)
bars2 = ax3.bar(x + width/2, godot_ratios, width, label="Godot (DOD/OOP)",
                color=COLORS["Godot DOD"], alpha=0.85)

ax3.axhline(1, color="black", linewidth=0.8, linestyle="--")
ax3.set_xlabel("Entity Sayısı", fontsize=12)
ax3.set_ylabel("Hız Oranı (OOP / DOD)  ↑ daha iyi = DOD kazanır", fontsize=11)
ax3.set_title("DOD Kazanım Oranı — Her Entity Sayısında OOP kaç kat yavaş?", fontsize=12)
ax3.set_xticks(x)
ax3.set_xticklabels([f"{ec:,}" for ec in ENTITY_COUNTS])
ax3.legend(fontsize=11)
ax3.grid(axis="y", alpha=0.3)

for bar in bars1:
    ax3.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.05,
             f"{bar.get_height():.1f}x", ha="center", va="bottom", fontsize=9)
for bar in bars2:
    ax3.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.05,
             f"{bar.get_height():.1f}x", ha="center", va="bottom", fontsize=9)

plt.tight_layout()
out2 = OUT / "fig2_speedup_ratio.png"
plt.savefig(out2, dpi=150, bbox_inches="tight")
print(f"Grafik kaydedildi: {out2}")

# ── 6. grafik 3: platform karsilastirma (OOP vs OOP, DOD vs DOD) ─────────────
fig3, axes3 = plt.subplots(1, 2, figsize=(14, 5))
fig3.suptitle("Platform Karşılaştırması (Unity vs Godot)", fontsize=13, fontweight="bold")

# OOP: Unity vs Godot
ax_oop = axes3[0]
for lbl in ["Unity OOP", "Godot OOP"]:
    y = [avg[lbl]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    ax_oop.plot(ENTITY_COUNTS, y, marker="o", label=lbl,
                color=COLORS[lbl], linewidth=2, markersize=6)
ax_oop.set_xscale("log")
ax_oop.set_yscale("log")
ax_oop.set_xlabel("Entity Sayısı", fontsize=11)
ax_oop.set_ylabel("Frame Time (ms)", fontsize=11)
ax_oop.set_title("Eksen 2: Platform Farkı — OOP Seviyesinde\n(C#/.NET vs GDScript)", fontsize=11)
ax_oop.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax_oop.yaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{x:.1f}"))
ax_oop.grid(True, which="both", alpha=0.3)
ax_oop.legend(fontsize=11)

# DOD: Unity vs Godot
ax_dod = axes3[1]
for lbl in ["Unity DOTS", "Godot DOD"]:
    y = [avg[lbl]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    ax_dod.plot(ENTITY_COUNTS, y, marker="o", label=lbl,
                color=COLORS[lbl], linewidth=2, markersize=6)
ax_dod.set_xscale("log")
ax_dod.set_yscale("log")
ax_dod.set_xlabel("Entity Sayısı", fontsize=11)
ax_dod.set_ylabel("Frame Time (ms)", fontsize=11)
ax_dod.set_title("Eksen 3: Platform Farkı — DOD Seviyesinde\n(Burst+ECS vs C++ SoA)", fontsize=11)
ax_dod.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax_dod.yaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{x:.1f}"))
ax_dod.grid(True, which="both", alpha=0.3)
ax_dod.legend(fontsize=11)

plt.tight_layout()
out3 = OUT / "fig3_platform_comparison.png"
plt.savefig(out3, dpi=150, bbox_inches="tight")
print(f"Grafik kaydedildi: {out3}")

print("\nTamamlandi.")
