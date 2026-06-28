"""
Benchmark Analysis Script (IEEE paper-ready, English labels)
Reads benchmarkresults.xlsx, computes 3-run averages,
produces comparison table + frame-time and FPS charts.

Style: grayscale-safe (distinct line styles + markers) so figures remain
legible in black-and-white print, as commonly required by IEEE venues.
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

# ── 1. load data ────────────────────────────────────────────────────────────
SHEET_GROUPS = {
    "Unity OOP":  ["unity-oop-1",  "unity-oop-2",  "unity-oop-3"],
    "Unity DOTS": ["unity-dod-1",  "unity-dod-2",  "unity-dod-3"],
    "Godot OOP":  ["godot-oop-1",  "godot-oop-2",  "godot-oop-3"],
    "Godot DOD":  ["godot-dod-1",  "godot-dod-2",  "godot-dod-3"],
}

# Grayscale-safe styling: each series gets a distinct grayscale shade,
# line style, and marker shape so the figure survives B/W printing.
STYLE = {
    "Unity OOP":  dict(color="0.55", linestyle="-",  marker="o"),
    "Unity DOTS": dict(color="0.35", linestyle="--", marker="s"),
    "Godot OOP":  dict(color="0.75", linestyle="-.", marker="^"),
    "Godot DOD":  dict(color="0.05", linestyle=":",  marker="D"),
}

ENTITY_COUNTS = [1_000, 5_000, 10_000, 50_000, 100_000]

plt.rcParams.update({
    "font.size": 11,
    "font.family": "serif",
    "axes.edgecolor": "black",
    "axes.linewidth": 0.8,
})

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

# 3-run averages
avg = {}
for label, sheets in SHEET_GROUPS.items():
    avg[label] = {
        "AvgFrameTime_ms": avg_runs(sheets, "AvgFrameTime_ms"),
        "AvgFPS":          avg_runs(sheets, "AvgFPS"),
        "StdDev_ms":       avg_runs(sheets, "StdDev_ms"),
        "MemoryUsed_MB":   avg_runs(sheets, "MemoryUsed_MB"),
    }

# ── 2. summary tables (console) ───────────────────────────────────────────
print("\n" + "="*80)
print("AVERAGE FRAME TIME (ms) — mean of 3 runs")
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
print("AVERAGE FPS — mean of 3 runs")
print("="*80)
print(header)
print("-"*len(header))
for ec in ENTITY_COUNTS:
    row = f"{ec:>8,} | "
    for lbl in ["Unity OOP","Unity DOTS","Godot OOP","Godot DOD"]:
        v = avg[lbl]["AvgFPS"][ec]
        row += f"{v:>10.1f} | "
    print(row)

print("\n" + "="*80)
print("SPEEDUP RATIO: OOP frame time / DOD frame time (higher = DOD wins more)")
print("="*80)
print(f"{'Entity':>8} | {'Unity DOTS/OOP':>15} | {'Godot DOD/OOP':>14}")
print("-"*46)
for ec in ENTITY_COUNTS:
    u = avg["Unity OOP"]["AvgFrameTime_ms"][ec] / avg["Unity DOTS"]["AvgFrameTime_ms"][ec]
    g = avg["Godot OOP"]["AvgFrameTime_ms"][ec]  / avg["Godot DOD"]["AvgFrameTime_ms"][ec]
    print(f"{ec:>8,} | {u:>14.1f}x | {g:>13.1f}x")

# ── 3. Figure 1: frame time & FPS vs entity count (log-log) ──────────────────
fig, axes = plt.subplots(1, 2, figsize=(12, 5))

ax = axes[0]
for lbl, data in avg.items():
    y = [data["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    ax.plot(ENTITY_COUNTS, y, label=lbl, linewidth=1.6, markersize=6,
            markerfacecolor="white", markeredgewidth=1.3, **s)

ax.set_xscale("log")
ax.set_yscale("log")
ax.set_xlabel("Entity Count")
ax.set_ylabel("Average Frame Time (ms)")
ax.set_title("(a) Frame Time vs. Entity Count")
ax.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax.yaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{x:.1f}"))
ax.grid(True, which="both", alpha=0.25, color="0.7")
ax.legend(fontsize=9, frameon=True, edgecolor="black")

ax2 = axes[1]
for lbl, data in avg.items():
    y = [data["AvgFPS"][ec] for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    ax2.plot(ENTITY_COUNTS, y, label=lbl, linewidth=1.6, markersize=6,
             markerfacecolor="white", markeredgewidth=1.3, **s)

ax2.set_xscale("log")
ax2.set_xlabel("Entity Count")
ax2.set_ylabel("Average FPS")
ax2.set_title("(b) FPS vs. Entity Count")
ax2.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax2.axhline(60, color="0.6", linestyle="--", linewidth=0.8)
ax2.axhline(30, color="0.6", linestyle=":",  linewidth=0.8)
ax2.text(1100, 65, "60 FPS", fontsize=8, color="0.4")
ax2.text(1100, 35, "30 FPS", fontsize=8, color="0.4")
ax2.grid(True, which="both", alpha=0.25, color="0.7")
ax2.legend(fontsize=9, frameon=True, edgecolor="black")

plt.tight_layout()
out1 = OUT / "fig1_frametime_fps.png"
plt.savefig(out1, dpi=300, bbox_inches="tight")
plt.savefig(OUT / "fig1_frametime_fps.pdf", bbox_inches="tight")
print(f"\nSaved: {out1}")

# ── 4. Figure 2: paradigm speedup bar chart ──────────────────────────────────
fig2, ax3 = plt.subplots(figsize=(8, 4.5))
x = np.arange(len(ENTITY_COUNTS))
width = 0.35

unity_ratios = [avg["Unity OOP"]["AvgFrameTime_ms"][ec] /
                avg["Unity DOTS"]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
godot_ratios = [avg["Godot OOP"]["AvgFrameTime_ms"][ec] /
                avg["Godot DOD"]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]

bars1 = ax3.bar(x - width/2, unity_ratios, width, label="Unity",
                color="0.65", edgecolor="black", linewidth=0.8, hatch="//")
bars2 = ax3.bar(x + width/2, godot_ratios, width, label="Godot",
                color="0.15", edgecolor="black", linewidth=0.8, hatch="..")

ax3.axhline(1, color="black", linewidth=0.8, linestyle="--")
ax3.text(len(ENTITY_COUNTS) - 0.45, 1.0, "break-even (1$\\times$)",
         ha="right", va="bottom", fontsize=7.5, color="0.3")
ax3.set_xlabel("Entity Count")
ax3.set_ylabel("DOD Speedup over OOP ($\\times$)\n(higher = larger DOD advantage)")
ax3.set_title("DOD Speedup over OOP, per Engine")
ax3.set_xticks(x)
ax3.set_xticklabels([f"{ec:,}" for ec in ENTITY_COUNTS])
ax3.legend(fontsize=9, frameon=True, edgecolor="black")
ax3.grid(axis="y", alpha=0.25, color="0.7")

for bar in bars1:
    ax3.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.4,
             f"{bar.get_height():.1f}x", ha="center", va="bottom", fontsize=8)
for bar in bars2:
    ax3.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.4,
             f"{bar.get_height():.1f}x", ha="center", va="bottom", fontsize=8)

plt.tight_layout()
out2 = OUT / "fig2_speedup_ratio.png"
plt.savefig(out2, dpi=300, bbox_inches="tight")
plt.savefig(OUT / "fig2_speedup_ratio.pdf", bbox_inches="tight")
print(f"Saved: {out2}")

# ── 5. Figure 3: cross-engine comparison (OOP vs OOP, DOD vs DOD) ────────────
# Shared y-axis across both panels so the two paradigms are directly
# comparable: the reader can see at a glance that DOD (panel b) sits far
# lower (faster) than OOP (panel a) at every scale.
fig3, axes3 = plt.subplots(1, 2, figsize=(12, 4.5), sharey=True)

# common y-range covering every series in this figure
_all_ft = [avg[l]["AvgFrameTime_ms"][ec]
           for l in ["Unity OOP", "Godot OOP", "Unity DOTS", "Godot DOD"]
           for ec in ENTITY_COUNTS]
_ylim = (min(_all_ft) * 0.7, max(_all_ft) * 1.5)

ax_oop = axes3[0]
for lbl in ["Unity OOP", "Godot OOP"]:
    y = [avg[lbl]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    ax_oop.plot(ENTITY_COUNTS, y, label=lbl, linewidth=1.6, markersize=6,
                markerfacecolor="white", markeredgewidth=1.3, **s)
ax_oop.set_xscale("log")
ax_oop.set_yscale("log")
ax_oop.set_ylim(_ylim)
ax_oop.set_xlabel("Entity Count")
ax_oop.set_ylabel("Frame Time (ms)")
ax_oop.set_title("(a) OOP: Unity vs. Godot")
ax_oop.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax_oop.yaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{x:.1f}"))
ax_oop.axhline(16.7, color="0.6", linestyle="--", linewidth=0.8)
ax_oop.text(1100, 17.5, "60 FPS", fontsize=8, color="0.4")
ax_oop.grid(True, which="both", alpha=0.25, color="0.7")
ax_oop.legend(fontsize=9, frameon=True, edgecolor="black")

ax_dod = axes3[1]
for lbl in ["Unity DOTS", "Godot DOD"]:
    y = [avg[lbl]["AvgFrameTime_ms"][ec] for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    ax_dod.plot(ENTITY_COUNTS, y, label=lbl, linewidth=1.6, markersize=6,
                markerfacecolor="white", markeredgewidth=1.3, **s)
ax_dod.set_xscale("log")
ax_dod.set_yscale("log")
ax_dod.set_ylim(_ylim)
ax_dod.set_xlabel("Entity Count")
ax_dod.set_title("(b) DOD: Unity vs. Godot")
ax_dod.xaxis.set_major_formatter(mticker.FuncFormatter(lambda x, _: f"{int(x):,}"))
ax_dod.axhline(16.7, color="0.6", linestyle="--", linewidth=0.8)
ax_dod.text(1100, 17.5, "60 FPS", fontsize=8, color="0.4")
ax_dod.grid(True, which="both", alpha=0.25, color="0.7")
ax_dod.legend(fontsize=9, frameon=True, edgecolor="black")

plt.tight_layout()
out3 = OUT / "fig3_platform_comparison.png"
plt.savefig(out3, dpi=300, bbox_inches="tight")
plt.savefig(OUT / "fig3_platform_comparison.pdf", bbox_inches="tight")
print(f"Saved: {out3}")

print("\nDone.")
