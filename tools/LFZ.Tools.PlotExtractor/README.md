# LFZ.Tools.PlotExtractor

Generates `AssetRequestApi/Seed/plots-seed.json` from the LFZ master-plan DWG.

```bash
python3 LFZ.Tools.PlotExtractor/plot_extractor.py \
  "Masterplan-LFZ-1001-LFZ-A-MP-DWG-R00 (002).dwg 15-12-25 L(with hatch).dwg" \
  AssetRequestApi/Seed/plots-seed.json
```

Install the CAD binding first when it is not already available:

```bash
python3 -m pip install aspose-cad
```

The generated JSON is intentionally reviewed and committed as the first-deploy seed artifact. The DWG remains the single source of truth; rerun the extractor whenever the master plan changes.
