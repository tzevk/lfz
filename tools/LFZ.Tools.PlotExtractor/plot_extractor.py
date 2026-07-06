#!/usr/bin/env python3
"""Extract LFZ plot seed rows from the master-plan DWG.

The script expects Aspose.CAD to be installed because modern DWG files do not
reliably expose text labels as plain strings. It extracts candidate text labels,
filters them by a configurable plot-code regex, and writes the seed JSON shape
consumed by AssetRequestApi.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any, Iterable


DEFAULT_CODE_PATTERN = r"\b(?:LFZ[-\s]?)?[A-Z]{1,4}[-\s]?\d{1,4}[A-Z]?\b"


def iter_text_values(value: Any, seen: set[int] | None = None) -> Iterable[str]:
    if seen is None:
        seen = set()

    value_id = id(value)
    if value_id in seen:
        return
    seen.add(value_id)

    if isinstance(value, str):
        stripped = value.strip()
        if stripped:
            yield stripped
        return

    if isinstance(value, (bytes, bytearray, int, float, bool, type(None))):
        return

    if isinstance(value, dict):
        for item in value.values():
            yield from iter_text_values(item, seen)
        return

    if isinstance(value, (list, tuple, set)):
        for item in value:
            yield from iter_text_values(item, seen)
        return

    for name in ("text", "default_value", "value", "name"):
        if hasattr(value, name):
            try:
                yield from iter_text_values(getattr(value, name), seen)
            except Exception:
                pass

    if hasattr(value, "__dict__"):
        for item in vars(value).values():
            yield from iter_text_values(item, seen)


def load_dwg_text(dwg_path: Path) -> list[str]:
    try:
        import aspose.cad as cad
    except ModuleNotFoundError as exc:
        raise RuntimeError(
            "Aspose.CAD is required to read DWG files. Install it with: python3 -m pip install aspose-cad"
        ) from exc

    image = cad.Image.load(str(dwg_path))
    try:
        return sorted(set(iter_text_values(image)))
    finally:
        dispose = getattr(image, "dispose", None)
        if callable(dispose):
            dispose()


def build_seed_rows(text_values: Iterable[str], code_pattern: str) -> list[dict[str, Any]]:
    regex = re.compile(code_pattern, re.IGNORECASE)
    codes: set[str] = set()
    for text in text_values:
        for match in regex.finditer(text):
            code = re.sub(r"\s+", "", match.group(0).upper())
            codes.add(code)

    return [
        {
            "code": code,
            "displayName": code,
            "landUseType": "Unspecified",
            "areaHectares": 0,
            "status": "Available",
            "svgPath": None,
            "centroid": None,
            "isLocked": False,
            "multiTenantBlockEnabled": False,
        }
        for code in sorted(codes)
    ]


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate plots-seed.json from an LFZ master-plan DWG.")
    parser.add_argument("dwg", type=Path, help="Path to the master-plan DWG")
    parser.add_argument("output", type=Path, help="Path to write plots-seed.json")
    parser.add_argument("--code-pattern", default=DEFAULT_CODE_PATTERN, help="Regex used to identify plot codes")
    args = parser.parse_args()

    if not args.dwg.exists():
        parser.error(f"DWG file does not exist: {args.dwg}")

    try:
        text_values = load_dwg_text(args.dwg)
        rows = build_seed_rows(text_values, args.code_pattern)
    except Exception as exc:
        print(f"plot_extractor: {exc}", file=sys.stderr)
        return 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(rows, indent=2) + "\n")
    print(f"Wrote {len(rows)} plot seed rows to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())