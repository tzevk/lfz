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
6. **Code assignment.** Parcels are ranked by area. The largest receive stable
   codes/names seeded from the PDF legend
   (`tools/LFZ.Tools.PlotExtractor/seed-codes.json`); the rest receive
   synthesised `IND-nnn` codes that can be renamed once a label-bearing DXF
   export is available.

## Results

| Stage | Count |
| --- | ---: |
| Rings on layer `PLOT AREA` (whole drawing) | **414** |
| Within Phase 1A extent polygon | 119 |
| ≥ 0.1 ha (sliver filter) | 66 |
| After deduplication — **seeded parcels** | **55** |

Named parcels (locked = non-selectable infrastructure):

| Code | Name | Area (ha) | Locked |
| --- | --- | ---: | :-: |
| e001 | Existing Development | 5.19 | ✔ |
| i001 | Insignia | 3.84 | |
| r001 | Raffles | 1.28 | |
| k001 | Kellogg's | 0.92 | |
| a001 | TG Arla | 0.92 | |
| c001 | TG Colgate Phase I | 0.84 | |
| c002 | TG Colgate Phase II | 0.77 | |
| h001 | Heavy Vehicle Park | 0.75 | ✔ |
| cp01 | Customs CP1 | 0.73 | ✔ |
| cp02 | Customs CP2 | 0.70 | ✔ |
| IND-001…IND-045 | Industrial plots | 0.10–0.68 | |

## Units and coordinate spaces

- Raw drawing units are **centimetres** (1 unit = 0.01 m). Polygon area / 10⁸ = hectares.
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
python3 ../extract_hatch_colors.py   # land-use hatch colours (reads the DWG directly)
python3 ../build_prototype.py        # refresh the standalone prototype
```

Truncate `Plots` (or use a fresh database) before restarting so the seed re-runs.

## Known caveats

1. **Extent polygon provenance.** `phase1a-extent.wkt` is derived from the
   verified parcel set (union + 10 m margin), not from an official phase
   boundary — the DWG does not contain one. It is deterministic and exact for
   the current drawing; replace the file with the official boundary WKT when
   the drawing office provides it.
2. **Labels.** Plot codes for unnamed parcels are synthesised (`IND-nnn`).
   Re-ingesting a label-bearing DXF export lets them be renamed via the Admin
   UI or a fresh extraction (codes are stable per run, ranked by area).
3. **Hatch colours.** The DWG contains **no per-plot colour fills** — the
   PDF's plot colouring is a publishing artefact. The DWG does carry legend
   swatch hatches on the `LU_*` layers; `tools/extract_hatch_colors.py` reads
   them directly from the DWG (aspose-cad object model, ACI → hex via ezdxf)
   and applies them to plots by land-use class:
   `Existing #FF00BF` (LU_Committed Devt), `Infrastructure #FFFF7F` (LU_Roads),
   `Green #BFFF7F` (LU_Green x Open Space). Industrial plots have no legend
   colour in the drawing, so their `HatchColor` stays null and the UI palette
   from `AppSettings` applies.
