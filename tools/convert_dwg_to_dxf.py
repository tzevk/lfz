#!/usr/bin/env python3
"""
DWG -> DXF converter for the LFZ master plan.

Uses aspose-cad to load the authoritative AutoCAD DWG and export an ASCII DXF
that LFZ.Tools.PlotExtractor can stream-parse.

Usage:
    python convert_dwg_to_dxf.py <input.dwg> <output.dxf>
"""
import sys

import aspose.cad as cad


def convert(input_path: str, output_path: str) -> None:
    print(f"Loading DWG: {input_path}")
    image = cad.Image.load(input_path)

    options = cad.imageoptions.DxfOptions()
    print(f"Exporting DXF: {output_path}")
    image.save(output_path, options)
    print("Done.")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    convert(sys.argv[1], sys.argv[2])
