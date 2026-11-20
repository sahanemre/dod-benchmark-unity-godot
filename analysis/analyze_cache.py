"""
Cache-Miss Analysis Script (IEEE paper-ready, English labels)
Reads results/cache-profiling/cache_miss_results.csv (AMD uProf 4.2,
IBS-DC "Investigate Data Access" profile) and produces an IPC / L2 cache
miss-rate comparison chart across implementations and entity counts.

Style: grayscale-safe (distinct hatch + shade per implementation) so the
figure remains legible in black-and-white print, as commonly required by
IEEE venues.
"""

import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from pathlib import Path

CSV = Path(__file__).parent.parent / "results" / "cache-profiling" / "cache_miss_results.csv"
OUT = Path(__file__).parent.parent / "results" / "figures"
OUT.mkdir(parents=True, exist_ok=True)

LABELS = ["Unity OOP", "Unity DOTS", "Godot OOP", "Godot DOD"]
ENTITY_COUNTS = [1_000, 100_000]

# Grayscale-safe styling: each implementation gets a distinct shade + hatch.
STYLE = {
    "Unity OOP":  dict(color="0.65", hatch="//"),
    "Unity DOTS": dict(color="0.40", hatch="xx"),
    "Godot OOP":  dict(color="0.85", hatch=".."),
    "Godot DOD":  dict(color="0.10", hatch=""),
}

plt.rcParams.update({
    "font.size": 11,
    "font.family": "serif",
    "axes.edgecolor": "black",
    "axes.linewidth": 0.8,
})

df = pd.read_csv(CSV)

fig, axes = plt.subplots(1, 2, figsize=(12, 4.8))
x = np.arange(len(ENTITY_COUNTS))
width = 0.2

# (a) IPC
ax = axes[0]
for i, lbl in enumerate(LABELS):
    y = [df[(df.implementation == lbl) & (df.entity_count == ec)]["ipc"].iloc[0]
         for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    bars = ax.bar(x + (i - 1.5) * width, y, width, label=lbl,
                   edgecolor="black", linewidth=0.8, **s)
    for bar, v in zip(bars, y):
        ax.text(bar.get_x() + bar.get_width() / 2, bar.get_height() + 0.02,
                 f"{v:.2f}", ha="center", va="bottom", fontsize=7)

ax.set_xlabel("Entity Count")
ax.set_ylabel("IPC (Instructions per Cycle)")
ax.set_title("(a) IPC vs. Entity Count")
ax.set_xticks(x)
ax.set_xticklabels([f"{ec:,}" for ec in ENTITY_COUNTS])
ax.grid(axis="y", alpha=0.25, color="0.7")
ax.legend(fontsize=8, frameon=True, edgecolor="black", ncol=2)

# (b) L2 cache miss rate
ax2 = axes[1]
for i, lbl in enumerate(LABELS):
    y = [df[(df.implementation == lbl) & (df.entity_count == ec)]["l2_miss_rate_pct"].iloc[0]
         for ec in ENTITY_COUNTS]
    s = STYLE[lbl]
    bars = ax2.bar(x + (i - 1.5) * width, y, width, label=lbl,
                    edgecolor="black", linewidth=0.8, **s)
    for bar, v in zip(bars, y):
        ax2.text(bar.get_x() + bar.get_width() / 2, bar.get_height() + 0.6,
                  f"{v:.1f}%", ha="center", va="bottom", fontsize=7)

ax2.set_xlabel("Entity Count")
ax2.set_ylabel("L2 Cache Miss Rate (%)")
ax2.set_title("(b) L2 Cache Miss Rate vs. Entity Count")
ax2.set_xticks(x)
ax2.set_xticklabels([f"{ec:,}" for ec in ENTITY_COUNTS])
ax2.set_ylim(0, 60)
ax2.grid(axis="y", alpha=0.25, color="0.7")
ax2.legend(fontsize=8, frameon=True, edgecolor="black", ncol=2)

plt.tight_layout()
out = OUT / "fig4_cache_miss_ipc.png"
plt.savefig(out, dpi=300, bbox_inches="tight")
plt.savefig(OUT / "fig4_cache_miss_ipc.pdf", bbox_inches="tight")
print(f"Saved: {out}")
