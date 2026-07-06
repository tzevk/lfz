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
3. **Extent filter.** The drawing covers more than Phase 1A and contains no
   phase-boundary layer (verified against every layer in the drawing — the only
   boundary-named layers are the small “00 Port Boundary” sub-area and a legend
   rectangle). The extent is therefore supplied as a WKT polygon
   (`--extent-wkt phase1a-extent.wkt`): a calibrated concave envelope of the
   verified Phase 1A parcel set (4 parts, 10 m margin) that reproduces the
   parcel set exactly and deterministically. If an official phase-boundary
   polygon becomes available, drop its WKT into the same file — no code change.
   (A legacy `--extent-max-x` centroid cut-off remains as a fallback.)
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
| Within Phase 1A extent polygon | 119 |
| ≥ 0.1 ha (sliver filter) | 66 |
| After deduplication — **seeded parcels** | **55** |

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
| … | 32 more labelled + 13 unlabelled (IND-nnn) | 0.66–4.46 | |

Total seeded area: **219.2 ha** (true metres).

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
dotnet run -- ../../masterplan.dxf --extent-wkt phase1a-extent.wkt
cp out/plots-seed.json ../../src/LFZ.Infrastructure/Seed/plots-seed.json
python3 ../extract_plot_labels.py    # real codes/names + true-metre scale (reads the DWG)
python3 ../extract_hatch_colors.py   # land-use hatch colours (reads the DWG)
python3 ../build_prototype.py        # refresh the standalone prototype
```

Truncate `Plots` (or use a fresh database) before restarting so the seed re-runs.

## Known caveats

1. **Extent polygon provenance.** `phase1a-extent.wkt` is derived from the
   verified parcel set (union + 10 m margin), not from an official phase
   boundary — the DWG does not contain one. It is deterministic and exact for
   the current drawing; replace the file with the official boundary WKT when
   the drawing office provides it.
2. **Labels.** Resolved: real drawing codes/names are recovered from the DWG
   model space by `extract_plot_labels.py` (42 of 55 parcels). The remaining 13
   parcels genuinely carry no ID label in the drawing and keep synthesised
   `IND-nnn` codes; they can be renamed in the Admin UI.
3. **Hatch colours.** The DWG contains **no per-plot colour fills** — the
   PDF's plot colouring is a publishing artefact. The DWG does carry legend
   swatch hatches on the `LU_*` layers; `tools/extract_hatch_colors.py` reads
   them directly from the DWG (aspose-cad object model, ACI → hex via ezdxf)
   and applies them to plots by land-use class:
   `Existing #FF00BF` (LU_Committed Devt), `Infrastructure #FFFF7F` (LU_Roads),
   `Green #BFFF7F` (LU_Green x Open Space). Industrial plots have no legend
   colour in the drawing, so their `HatchColor` stays null and the UI palette
   from `AppSettings` applies.
