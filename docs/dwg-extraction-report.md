# DWG extraction report — LFZ master plan

**Source drawing:** `Masterplan-LFZ-1001-LFZ-A-MP-DWG-R00 (002).dwg 15-12-25 L(with hatch).dwg`
**Reference:** `LFZ Master Plan .pdf` (legend used to seed plot names)
**Pipeline:** `tools/convert_dwg_to_dxf.py` (aspose-cad) → `tools/LFZ.Tools.PlotExtractor` (.NET 8)
**Extraction date:** 2026-07-07

## Pipeline

1. **DWG → DXF.** The DWG is converted with `aspose-cad` to an ASCII DXF
   (R12 / AC1009, ~541 MB). The conversion is faithful for geometry but lossy in
   two documented ways:
   - the *closed* flag (group 70 bit 1) of POLYLINE entities is dropped, and
   - text labels (plot codes) are stripped.
2. **Stream parse.** `LFZ.Tools.PlotExtractor` stream-parses the DXF
   (constant memory) and collects every POLYLINE/LWPOLYLINE ring with ≥ 3
   vertices on the **PLOT AREA** layer. Because the closed flag is lost,
   closure is detected geometrically (coincident first/last vertex is stripped;
   remaining rings are treated as closed polygons).
3. **Coverage & phase tagging.** Extraction is **zone-wide** (no extent filter):
   every developable parcel in the drawing is seeded. Phases are tagged from
   the DWG's own **development-phase colour fills**: the plan carries SOLID
   hatches per phase whose colours are fixed by the legend swatch column
   (ACI 9 = Phase 1A, 153 = Phase 1B, 50/2 = Phase 2, 11 = Phase 3 — the same
   grey/blue/yellow/salmon scheme visible in the published PDF). Hatches with
   embedded boundary paths are used directly (Phase 2: 8.5 + 32.4 + 80.6 ha
   ≈ legend 122 ha; Phase 3: 37 + 52.6 + 115.9 + 132 ha ≈ legend 246 ha);
   associative hatches (Phase 1A/1B) are resolved to the phase-scale boundary
   ring containing their seed point (276.4 ha ≈ legend 278; northern strip
   167 ha). Each parcel gets the tightest containing fill.
4. **Sliver filter.** Parcels below **0.1 ha** are drawing artefacts and removed.
5. **Deduplication.** Duplicate rings (identical rounded centroid + area,
   overdrawn outlines) are collapsed.
6. **Code assignment.** Plot codes and names are recovered from the DWG itself
   by `tools/extract_plot_labels.py`: the DWG model space carries the true-scale
   `PLOT AREA` rings and the plot ID labels (MTEXT/TEXT such as
   `I 004 / 54961 SQ.M`). Each seeded parcel is matched to its model ring by a
   scale-invariant shape fingerprint (vertex count + isoperimetric factor);
   the label inside the ring supplies the drawing code and display name.
   42 of 55 parcels carry real drawing codes (duplicated codes on the plan get
   a `b`/`c` suffix); the 13 parcels with no label in the drawing keep
   synthesised `IND-nnn` codes.

7. **Scale correction.** Matching also revealed that the aspose DXF export
   scales geometry by ≈ 0.3917 (k = 2.5533 back to true metres, stdev 0.015,
   53 matched parcels). The label recovery step rescales `svgPath`,
   `centroid`, `boundaryWkt` and `areaHectares` to true metres — confirmed by
   the drawing's own quoted areas (e.g. ring labelled `338259 SQ.M` → 33.83 ha).

## Results

| Stage | Count |
| --- | ---: |
| Rings on layer `PLOT AREA` (whole drawing) | **414** |
| ≥ 0.1 ha (sliver filter) | 137 |
| After deduplication — **seeded parcels (complete zone)** | **124** |
| — Phase 1A | 40 |
| — Phase 1B | 22 |
| — Phase 2 | 29 |
| — Phase 3 | 33 |
| Real drawing codes recovered | 91 |
| Land-use classified from code prefix (i/l/u/c/m/cp/t) | 124 |
| Locked (infrastructure/utility) | 8 |

Total seeded area: **509.9 ha** (true metres).

Largest parcels (codes/names from the drawing labels; locked = non-selectable):

| Code | Name | Area (ha) | Locked |
| --- | --- | ---: | :-: |
| l005 | L2E Tower (Inland Port Logistics) | 33.83 | ✔ |
| i011 | Future Industry | 25.03 | |
| i009 | LFZ Camp | 8.32 | |
| i003 | Raffles | 6.02 | |
| i004 | TG Arla | 6.00 | |
| i004b | TG Colgate Phase I | 5.50 | |
| c002 | Carton Factory | 5.00 | |
| i028 | Heavy Vehicle Park | 4.86 | ✔ |
| l004 | Customs CP1 | 4.78 | ✔ |
| i002 | Customs CP2 | 4.57 | ✔ |
| … | 81 more labelled + 33 unlabelled (IND-nnn) | 0.1–4.5 | |

(See the results table above for zone-wide counts.)

## Units and coordinate spaces

- Raw DXF-export units are **centimetres at 0.3917× scale** (the aspose export
  shrinks the drawing); `extract_plot_labels.py` rescales the seed to **true
  plan metres** using the model-space rings. Polygon area / 10⁴ = hectares.
- `Plots.Boundary` (WKT → SQL `geometry`): plan **metres**, original CAD
  orientation, SRID 0.
- `Plots.SvgPath`: plan metres, **y-flipped** (`y' = maxY − y`) and normalised
  to the extraction bounding box so browsers render it with no client-side
  transform.

## Outputs

- `plots-seed.json` — consumed by `SeedData` at first startup (includes `boundaryWkt`).
- `plots-seed.sql` — direct SQL Server insert script (`geometry::STGeomFromText`).
- `preview.svg` — visual QA of the extracted parcels.

## Re-running (data-only maintenance)

```bash
.venv-cad/bin/python tools/convert_dwg_to_dxf.py "<new drawing>.dwg" masterplan.dxf
cd tools/LFZ.Tools.PlotExtractor
dotnet run -- ../../masterplan.dxf --codes none        # complete zone
cp out/plots-seed.json ../../src/LFZ.Infrastructure/Seed/plots-seed.json
python3 ../tag_phases.py             # phase tagging from extent WKTs
python3 ../extract_plot_labels.py    # real codes/names/land-use + true-metre scale (reads the DWG)
python3 ../extract_hatch_colors.py   # land-use hatch colours (reads the DWG)
python3 ../build_prototype.py        # refresh the standalone prototype
```

Truncate `Plots` (or use a fresh database) before restarting so the seed re-runs.

## Known caveats

1. **Phase gross vs parcel areas.** Legend figures are gross phase areas
   (including roads/water); the seeded parcel-area sums per phase are lower
   (1A 134 ha, 1B 85 ha, 2 129 ha, 3 159 ha). Parcels straddling a fill edge
   are attributed by centroid — verify edge cases against the published PDF.
2. **Labels.** Real drawing codes/names are recovered from the DWG model space
   by `extract_plot_labels.py` (91 of 124 parcels). The remaining 33 parcels
   carry no ID label in the drawing and keep synthesised `IND-nnn` codes; they
   can be renamed in the Admin UI.
3. **Hatch colours.** The DWG contains **no per-plot colour fills** — the
   PDF's plot colouring is a publishing artefact. The DWG does carry legend
   swatch hatches on the `LU_*` layers; `tools/extract_hatch_colors.py` reads
   them directly from the DWG (aspose-cad object model, ACI → hex via ezdxf)
   and applies them to plots by land-use class:
   `Existing #FF00BF` (LU_Committed Devt), `Infrastructure #FFFF7F` (LU_Roads),
   `Green #BFFF7F` (LU_Green x Open Space). Industrial plots have no legend
   colour in the drawing, so their `HatchColor` stays null and the UI palette
   from `AppSettings` applies.
