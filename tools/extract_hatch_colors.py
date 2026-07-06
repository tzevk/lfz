#!/usr/bin/env python3
"""Extracts land-use hatch colours from the LFZ master-plan DWG and writes them
into the HatchColor field of src/LFZ.Infrastructure/Seed/plots-seed.json.

Finding (2026-07-07): the DWG contains no per-plot colour fills — the PDF's
plot colouring is a publishing artefact. What the DWG does carry are the
*legend swatch* hatches on the LU_* layers (SOLID pattern, per-entity ACI
colour). This script reads those swatches, converts ACI -> hex, and applies
the colour to plots by land-use class.

Usage:
    python3 tools/extract_hatch_colors.py [--dwg <path>] [--seed <path>]
"""
import argparse
import collections
import json
import pathlib

import aspose.cad as cad
import aspose.pycore as pycore
from aspose.cad.fileformats.cad import CadImage
from aspose.cad.fileformats.cad.cadobjects.hatch import CadHatch
from ezdxf import colors as dxf_colors

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_DWG = ROOT / "Masterplan-LFZ-1001-LFZ-A-MP-DWG-R00 (002).dwg 15-12-25 L(with hatch).dwg"
DEFAULT_SEED = ROOT / "src/LFZ.Infrastructure/Seed/plots-seed.json"

# Legend layer -> LandUseType values used in the plot seed
LAYER_TO_LANDUSE = {
    "LU_Committed Devt": ["Existing"],
    "LU_Roads": ["Infrastructure"],
    "LU_Green x Open Space": ["Green"],
    "LU_Waterbody Fill": ["Waterbody"],
}

# Neutral/ByLayer colours that are drawing artefacts, not legend swatches
NEUTRAL_ACI = {0, 1, 3, 7, 255, 256}


def collect_legend_colors(dwg_path: pathlib.Path) -> dict[str, str]:
    print(f"Loading DWG: {dwg_path.name}")
    image = cad.Image.load(str(dwg_path))
    cad_image = pycore.cast(CadImage, image)

    aci_per_layer: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    for block in cad_image.block_entities.values:
        for entity in block.entities:
            if not str(entity.type_name).endswith("HATCH"):
                continue
            hatch = pycore.cast(CadHatch, entity)
            layer = hatch.layer_name or ""
            if layer not in LAYER_TO_LANDUSE:
                continue
            true_color = hatch.color_value
            if true_color is not None:
                rgb = int(true_color) & 0xFFFFFF
                aci_per_layer[layer][f"#{rgb:06X}"] += 1
            elif hatch.color_id not in NEUTRAL_ACI:
                r, g, b = dxf_colors.aci2rgb(hatch.color_id)
                aci_per_layer[layer][f"#{r:02X}{g:02X}{b:02X}"] += 1

    legend = {}
    for layer, counter in aci_per_layer.items():
        color, count = counter.most_common(1)[0]
        legend[layer] = color
        print(f"  {layer:<28} -> {color}  (from {count} legend swatch(es))")
    return legend


def apply_to_seed(legend: dict[str, str], seed_path: pathlib.Path) -> None:
    landuse_to_color = {
        landuse: color
        for layer, color in legend.items()
        for landuse in LAYER_TO_LANDUSE[layer]
    }

    seed = json.loads(seed_path.read_text())
    updated = 0
    for plot in seed:
        color = landuse_to_color.get(plot.get("landUseType", ""))
        if color and plot.get("hatchColor") != color:
            plot["hatchColor"] = color
            updated += 1

    seed_path.write_text(json.dumps(seed, indent=2))
    print(f"Updated hatchColor on {updated} of {len(seed)} plots -> {seed_path}")
    print("Land-use colour map:", json.dumps(landuse_to_color))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--dwg", type=pathlib.Path, default=DEFAULT_DWG)
    parser.add_argument("--seed", type=pathlib.Path, default=DEFAULT_SEED)
    args = parser.parse_args()

    legend_colors = collect_legend_colors(args.dwg)
    if not legend_colors:
        raise SystemExit("No legend hatches found on LU_* layers.")
    apply_to_seed(legend_colors, args.seed)
