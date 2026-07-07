#!/usr/bin/env python3
"""Builds LFZ-prototype.html by injecting the real extracted plot geometry
(src/LFZ.Infrastructure/Seed/plots-seed.json) into the prototype template."""
import json
import pathlib

root = pathlib.Path(__file__).resolve().parent.parent
seed = json.loads((root / "src/LFZ.Infrastructure/Seed/plots-seed.json").read_text())

# Drop the WKT (only needed server-side) to keep the file lean
for item in seed:
    item.pop("boundaryWkt", None)

template = (root / "tools/prototype-template.html").read_text()
html = template.replace("/*__PLOTS_JSON__*/[]", json.dumps(seed, separators=(",", ":")))

context_path = root / "src/LFZ.Web/wwwroot/map-context.json"
context = context_path.read_text() if context_path.exists() else "{}"
html = html.replace("/*__CONTEXT_JSON__*/{}", context)

out = root / "LFZ-prototype.html"
out.write_text(html)
print(f"Wrote {out} ({out.stat().st_size / 1024:.0f} KB, {len(seed)} plots)")
