#!/usr/bin/env python3
"""Recovers real plot codes/names from the master-plan DWG and fixes the seed scale.

Background (see docs/dwg-extraction-report.md):
- The aspose DWG->DXF export strips text labels AND scales geometry by ~0.392.
- The DWG model space contains the true-scale PLOT AREA rings and the plot ID
  labels (MTEXT/TEXT such as "I 004 / 54961 SQ.M", "CP 01 / CUSTOMS CHECKPOINT").

This script:
1. Loads model-space PLOT AREA rings and text labels from the DWG.
2. Assigns each label to the smallest ring containing its insertion point.
3. Matches every seeded parcel to its model ring by scale-invariant shape
   fingerprint (vertex count + isoperimetric factor), deriving the exact scale.
4. Rescales seed geometry (svgPath, centroid, boundaryWkt) to true metres and
   recomputes areaHectares.
5. Replaces synthesised IND-nnn codes (and placeholder names) with the real
   drawing codes/names; duplicate drawing codes get a b/c suffix.

Usage:
    python3 tools/extract_plot_labels.py [--dwg <path>] [--seed <path>] [--dry-run]
"""
import argparse
import json
import math
import pathlib
import re
import statistics

import aspose.cad as cad
import aspose.pycore as pycore
from aspose.cad.fileformats.cad import CadImage
from aspose.cad.fileformats.cad.cadobjects import CadLwPolyline, CadMText, CadText
from shapely import wkt as shapely_wkt
from shapely.affinity import scale as shapely_scale
from shapely.geometry import Point, Polygon

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_DWG = ROOT / "Masterplan-LFZ-1001-LFZ-A-MP-DWG-R00 (002).dwg 15-12-25 L(with hatch).dwg"
DEFAULT_SEED = ROOT / "src/LFZ.Infrastructure/Seed/plots-seed.json"

CODE_PATTERN = re.compile(r"^(I|L|U|C|M|T|CP|RT|SIF|WH)\s?-?\s?(\d{1,3})\s?(?:/([A-Z]))?$", re.I)
AREA_PATTERN = re.compile(r"([\d,.]+)\s*(?:SQ\.?\s?M|sq\s?m|SQM)", re.I)


def clean_lines(raw: str) -> list[str]:
    """Strip MTEXT formatting codes and split into lines."""
    s = raw.replace("\\P", "\n")
    s = re.sub(r"\{\\f[^;]*;([^}]*)\}", r"\1", s)
    s = re.sub(r"\\[A-Za-z][^;\n]*;", "", s)
    s = s.replace("{", "").replace("}", "")
    return [line.strip() for line in s.split("\n") if line.strip()]


def load_model_space(dwg_path: pathlib.Path):
    print(f"Loading DWG: {dwg_path.name}")
    image = cad.Image.load(str(dwg_path))
    cad_image = pycore.cast(CadImage, image)

    rings: list[Polygon] = []
    labels: list[tuple[Point, list[str]]] = []
    for block in cad_image.block_entities.values:
        for entity in block.entities:
            type_name = str(entity.type_name)
            if getattr(entity, "layer_name", "") == "PLOT AREA" and type_name.endswith("LWPOLYLINE"):
                lw = pycore.cast(CadLwPolyline, entity)
                try:
                    pts = [(c.x, c.y) for c in lw.coordinates]
                except Exception:
                    continue
                if len(pts) >= 3:
                    poly = Polygon(pts)
                    if poly.is_valid and poly.area > 0:
                        rings.append(poly)
            elif type_name.endswith("MTEXT") or type_name.endswith(".TEXT"):
                try:
                    if type_name.endswith("MTEXT"):
                        m = pycore.cast(CadMText, entity)
                        value, ip = m.text, m.insertion_point
                    else:
                        t = pycore.cast(CadText, entity)
                        value, ip = t.default_value, t.first_alignment
                except Exception:
                    continue
                if value and ip is not None:
                    lines = clean_lines(value.strip())
                    if lines:
                        labels.append((Point(ip.x, ip.y), lines))

    print(f"Model space: {len(rings)} PLOT AREA rings, {len(labels)} text labels")
    return rings, labels


def assign_labels_to_rings(rings, labels):
    """Each label belongs to the smallest ring containing its insertion point."""
    per_ring: dict[int, list[list[str]]] = {}
    for point, lines in labels:
        best_idx, best_area = None, math.inf
        for idx, ring in enumerate(rings):
            if ring.area < best_area and ring.contains(point):
                best_idx, best_area = idx, ring.area
        if best_idx is not None:
            per_ring.setdefault(best_idx, []).append(lines)
    return per_ring


def fingerprint_match(seed_poly: Polygon, rings) -> list[int]:
    """Ring candidates with the same shape (scale-invariant)."""
    nv = len(seed_poly.exterior.coords) - 1
    factor = seed_poly.length**2 / seed_poly.area
    out = []
    for idx, ring in enumerate(rings):
        if abs((len(ring.exterior.coords) - 1) - nv) > 1:
            continue
        ring_factor = ring.length**2 / ring.area
        if abs(ring_factor - factor) / factor < 0.001:
            out.append(idx)
    return out


def order_label_sets(ring: Polygon, label_sets):
    """Order label sets: those whose quoted SQ.M figure matches the ring area first."""
    ring_sqm = ring.area

    def err(lines):
        best = math.inf
        for line in lines:
            m = AREA_PATTERN.search(line)
            if m:
                try:
                    sqm = float(m.group(1).replace(",", ""))
                except ValueError:
                    continue
                best = min(best, abs(sqm - ring_sqm) / ring_sqm)
        return best

    return sorted(label_sets, key=err)


NOISE_NAMES = re.compile(
    r"^(TOILET|SEPTIC TANK|TRANSFORMER|OVER ?HEAD WATER TANK|GUARD|GATE|RAMP|SECURITY)", re.I)


def parse_code_and_name(lines):
    code = None
    name_parts = []
    for line in lines:
        # split slash-packed labels like "I003/RAFFLES/60000 SQ.M"
        for token in re.split(r"[/]", line):
            token = token.strip()
            if not token:
                continue
            m = CODE_PATTERN.match(token)
            if m and code is None:
                suffix = (m.group(3) or "").lower()
                code = f"{m.group(1).lower()}{int(m.group(2)):03d}{suffix}"
                continue
            if AREA_PATTERN.search(token) or re.fullmatch(r"[\d,.]+(\s*HA)?", token, re.I):
                continue
            if token.upper() in ("PLOT",) or NOISE_NAMES.match(token):
                continue
            name_parts.append(token)
    name = " ".join(dict.fromkeys(name_parts)).strip() or None
    if name:
        name = name.title()
    return code, name


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dwg", type=pathlib.Path, default=DEFAULT_DWG)
    parser.add_argument("--seed", type=pathlib.Path, default=DEFAULT_SEED)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    rings, labels = load_model_space(args.dwg)
    ring_labels = assign_labels_to_rings(rings, labels)

    seed = json.loads(args.seed.read_text())

    # ---- pass 1: match rings, derive exact scale -------------------------
    matches: dict[str, int] = {}
    scale_samples = []
    for plot in seed:
        seed_poly = shapely_wkt.loads(plot["boundaryWkt"])
        candidates = fingerprint_match(seed_poly, rings)
        if len(candidates) == 0:
            continue
        # among shape-identical candidates prefer the one with the modal scale
        ratios = [(idx, math.sqrt(rings[idx].area / seed_poly.area)) for idx in candidates]
        idx, ratio = min(ratios, key=lambda t: abs(t[1] - 2.551))  # ~1/0.392 prior
        matches[plot["code"]] = idx
        scale_samples.append(ratio)

    k = statistics.median(scale_samples)
    print(f"Matched {len(matches)}/{len(seed)} parcels; scale factor k = {k:.6f} "
          f"(stdev {statistics.pstdev(scale_samples):.6f})")

    # ---- pass 2: rewrite the seed ----------------------------------------
    used_codes: set[str] = set()
    renamed = 0
    rescaled = 0
    for plot in seed:
        # 2a. true-metre geometry
        boundary = shapely_scale(shapely_wkt.loads(plot["boundaryWkt"]), xfact=k, yfact=k, origin=(0, 0))
        plot["boundaryWkt"] = shapely_wkt.dumps(boundary, rounding_precision=3)
        plot["areaHectares"] = round(boundary.area / 1e4, 4)
        plot["svgPath"] = re.sub(
            r"-?\d+(?:\.\d+)?",
            lambda m: f"{float(m.group(0)) * k:.2f}",
            plot["svgPath"])
        if plot.get("centroid"):
            plot["centroid"]["x"] = round(plot["centroid"]["x"] * k, 2)
            plot["centroid"]["y"] = round(plot["centroid"]["y"] * k, 2)
        rescaled += 1

        # 2b. real code and name — merge every label inside the ring,
        # area-matching label first (the drawing is authoritative)
        idx = matches.get(plot["code"])
        if idx is None or idx not in ring_labels:
            used_codes.add(plot["code"])
            continue
        code, name = None, None
        for lines in order_label_sets(rings[idx], ring_labels[idx]):
            c, n = parse_code_and_name(lines)
            code = code or c
            name = name or n
            if code and name:
                break
        if code:
            base = code
            for suffix in ("", "b", "c", "d"):
                if base + suffix not in used_codes:
                    code = base + suffix
                    break
            else:
                code = plot["code"]
            if code != plot["code"]:
                renamed += 1
            plot["code"] = code
        if name:
            plot["displayName"] = name
        used_codes.add(plot["code"])

    print(f"Rescaled {rescaled} parcels to true metres; real drawing codes applied to {renamed}.")
    for plot in seed[:15]:
        print(f"  {plot['code']:8} {plot['areaHectares']:8.2f} ha  {plot['displayName']}")

    if args.dry_run:
        print("(dry-run: seed not written)")
        return

    args.seed.write_text(json.dumps(seed, indent=2))
    print(f"Wrote {args.seed}")


if __name__ == "__main__":
    main()
