#!/usr/bin/env python3
"""Extracts map context (roads, rail, waterway incl. the port turning basin)
from the master-plan DWG into the same SVG frame as the plot seed.

Everything visible in the published PDF that is not a parcel — carriageways,
medians, rail, the navigation channel / turning basin / breakwaters — is
emitted as SVG path strings so the prototype and the Blazor map can render
them as a background layer.

Output: src/LFZ.Web/wwwroot/map-context.json
Usage:  python3 tools/extract_context.py  (run AFTER extract_plot_labels.py)
"""
import json
import math
import pathlib
import re
import statistics

import aspose.cad as cad
import aspose.pycore as pycore
from aspose.cad.fileformats.cad import CadImage
from aspose.cad.fileformats.cad.cadobjects import CadLwPolyline
from shapely import wkt as shapely_wkt
from shapely.geometry import LineString, Polygon, box

ROOT = pathlib.Path(__file__).resolve().parent.parent
DWG = ROOT / "Masterplan-LFZ-1001-LFZ-A-MP-DWG-R00 (002).dwg 15-12-25 L(with hatch).dwg"
SEED = ROOT / "src/LFZ.Infrastructure/Seed/plots-seed.json"
OUT = ROOT / "src/LFZ.Web/wwwroot/map-context.json"

# Model-space window: the zone strip plus the port/turning basin; the long
# navigation channel south of y=707k is trimmed to keep the map compact
WINDOW = (604000, 707500, 618500, 713200)
WINDOW_BOX = box(*WINDOW)

# context group -> model-space layer names
CONTEXT_LAYERS = {
    "water": ["FHDI-ZT-航道", "LU_Waterbody Line", "200 WaterBody"],
    "roads": ["00 Carriageway", "000 Tarred-Road", "INNER ROAD", "EXISTING ROAD",
              "new_road", "New_access", "laybys", "LU_Roads"],
    "median": ["Median", "000 Tarred-RoadCenterline", "0 RD Centerline"],
    "rail": ["Rail"],
}
LAYER_TO_GROUP = {layer: group for group, layers in CONTEXT_LAYERS.items() for layer in layers}


def in_window(x, y):
    return WINDOW[0] < x < WINDOW[2] and WINDOW[1] < y < WINDOW[3]


def bbox_intersects(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return not (max(xs) < WINDOW[0] or min(xs) > WINDOW[2] or
                max(ys) < WINDOW[1] or min(ys) > WINDOW[3])


def load_model(dwg_path):
    print(f"Loading DWG: {dwg_path.name}")
    image = cad.Image.load(str(dwg_path))
    cad_image = pycore.cast(CadImage, image)

    plot_rings = []           # for transform derivation
    context = {g: [] for g in CONTEXT_LAYERS}
    for block in cad_image.block_entities.values:
        if getattr(block, "name", "") != "*Model_Space":
            continue
        for entity in block.entities:
            type_name = str(entity.type_name)
            layer = getattr(entity, "layer_name", "") or ""
            if type_name.endswith("LWPOLYLINE"):
                lw = pycore.cast(CadLwPolyline, entity)
                try:
                    pts = [(c.x, c.y) for c in lw.coordinates]
                except Exception:
                    continue
                if len(pts) < 2 or not bbox_intersects(pts):
                    continue
                if layer == "PLOT AREA" and len(pts) >= 3:
                    poly = Polygon(pts)
                    if poly.is_valid and poly.area > 0:
                        plot_rings.append(poly)
                elif layer in LAYER_TO_GROUP:
                    context[LAYER_TO_GROUP[layer]].append(pts)
            elif type_name.endswith("LINE") and not type_name.endswith("LWPOLYLINE") \
                    and layer in LAYER_TO_GROUP:
                fp = getattr(entity, "first_point", None)
                sp = getattr(entity, "second_point", None)
                if fp is not None and sp is not None and \
                        bbox_intersects([(fp.x, fp.y), (sp.x, sp.y)]):
                    context[LAYER_TO_GROUP[layer]].append([(fp.x, fp.y), (sp.x, sp.y)])
    return plot_rings, context


def derive_transform(plot_rings, seed):
    """Model space -> seed SVG frame.

    svg_x = (model_x + dx) - A ; svg_y = B - (model_y + dy)
    where (dx,dy) maps model coords onto the seed's boundaryWkt frame and
    A/B are the WKT->SVG offsets baked in by the extractor.
    """
    deltas = []
    for plot in seed:
        g = shapely_wkt.loads(plot["boundaryWkt"])
        nv = len(g.exterior.coords) - 1
        factor = g.length**2 / g.area
        for ring in plot_rings:
            if abs((len(ring.exterior.coords) - 1) - nv) > 1:
                continue
            if abs(ring.length**2 / ring.area - factor) / factor < 0.001 and \
                    abs(math.sqrt(g.area / ring.area) - 1.0) < 0.01:
                deltas.append((g.centroid.x - ring.centroid.x, g.centroid.y - ring.centroid.y))
                # keep collecting: identical shapes exist at several locations
    # robust two-pass: median, then refit on inliers within 5 m
    dx = statistics.median(d[0] for d in deltas)
    dy = statistics.median(d[1] for d in deltas)
    inliers = [d for d in deltas if abs(d[0] - dx) < 5 and abs(d[1] - dy) < 5]
    dx = statistics.mean(d[0] for d in inliers)
    dy = statistics.mean(d[1] for d in inliers)
    spread = max(statistics.pstdev([d[0] for d in inliers]), statistics.pstdev([d[1] for d in inliers]))
    print(f"Transform: d=({dx:.2f},{dy:.2f}) m from {len(inliers)}/{len(deltas)} inlier pairs, spread {spread:.2f} m")

    # WKT -> SVG offsets from the first seed parcel's first vertex
    first = seed[0]
    wx, wy = list(shapely_wkt.loads(first["boundaryWkt"]).exterior.coords)[0]
    m = re.match(r"M(-?\d+(?:\.\d+)?) (-?\d+(?:\.\d+)?)", first["svgPath"])
    sx, sy = float(m.group(1)), float(m.group(2))
    a, b = wx - sx, wy + sy
    return lambda x, y: ((x + dx) - a, b - (y + dy))


def to_paths(pts, transform):
    """Clip geometry to the plan window, then emit SVG path strings."""
    closed = len(pts) >= 3 and abs(pts[0][0] - pts[-1][0]) < 0.5 and abs(pts[0][1] - pts[-1][1]) < 0.5
    try:
        geom = Polygon(pts) if closed and len(pts) >= 3 else LineString(pts)
        if closed and not geom.is_valid:
            geom = geom.buffer(0)
        clipped = geom.intersection(WINDOW_BOX)
    except Exception:
        return []

    paths = []
    parts = getattr(clipped, "geoms", [clipped])
    for part in parts:
        if part.is_empty:
            continue
        if part.geom_type == "Polygon":
            coords = [transform(x, y) for x, y in part.exterior.coords]
            paths.append("M" + "L".join(f"{x:.1f} {y:.1f}" for x, y in coords) + "Z")
        elif part.geom_type == "LineString":
            coords = [transform(x, y) for x, y in part.coords]
            if len(coords) >= 2:
                paths.append("M" + "L".join(f"{x:.1f} {y:.1f}" for x, y in coords))
    return paths


def main():
    seed = json.loads(SEED.read_text())
    plot_rings, context = load_model(DWG)
    transform = derive_transform(plot_rings, seed)

    out = {group: [path for pts in items for path in to_paths(pts, transform)]
           for group, items in context.items()}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(out, separators=(",", ":")))
    sizes = {g: len(v) for g, v in out.items()}
    print(f"Wrote {OUT} ({OUT.stat().st_size/1024:.0f} KB): {sizes}")


if __name__ == "__main__":
    main()
