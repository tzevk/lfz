#!/usr/bin/env python3
"""Tags each seeded plot with its phase using extent polygons.

Run immediately after copying the extractor output (raw-frame geometry) into
the seed, BEFORE extract_plot_labels.py rescales it to true metres.

Extents are WKT files in raw drawing units (cm), as used by the extractor's
--extent-wkt option. Plots whose centroid falls inside an extent get that
phase name; everything else gets the fallback.
"""
import json
import pathlib

from shapely import wkt as shapely_wkt
from shapely.affinity import scale

ROOT = pathlib.Path(__file__).resolve().parent.parent
SEED = ROOT / "src/LFZ.Infrastructure/Seed/plots-seed.json"

PHASE_EXTENTS = [
    ("Phase 1A", ROOT / "tools/LFZ.Tools.PlotExtractor/phase1a-extent.wkt"),
    # Add further phases here as boundary polygons become available:
    # ("Phase 1B", ROOT / "tools/LFZ.Tools.PlotExtractor/phase1b-extent.wkt"),
]
FALLBACK_PHASE = "Phase 3 / Future"

seed = json.loads(SEED.read_text())
extents = [
    (name, scale(shapely_wkt.loads(path.read_text()), xfact=0.01, yfact=0.01, origin=(0, 0)))
    for name, path in PHASE_EXTENTS if path.exists()
]

counts: dict[str, int] = {}
for plot in seed:
    centroid = shapely_wkt.loads(plot["boundaryWkt"]).centroid
    phase = next((name for name, poly in extents if poly.contains(centroid)), FALLBACK_PHASE)
    plot["phase"] = phase
    counts[phase] = counts.get(phase, 0) + 1

SEED.write_text(json.dumps(seed, indent=2))
print(f"Tagged {len(seed)} plots: {counts}")
