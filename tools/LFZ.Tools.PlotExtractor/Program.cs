using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LFZ.Tools.PlotExtractor;

internal record SeedCode(int Rank, string Code, string Name, bool IsLocked, string? LandUseType = null);

/// <summary>Matches AssetRequestApi SeedData.PlotSeedItem (plus BoundaryWkt).</summary>
internal record PlotSeedItem(
    string Code,
    string DisplayName,
    string LandUseType,
    decimal AreaHectares,
    string Status,
    string SvgPath,
    Centroid Centroid,
    bool IsLocked,
    bool MultiTenantBlockEnabled,
    string BoundaryWkt);

internal record Centroid(double X, double Y);

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string dxfPath = args[0];
        string layer = GetOption(args, "--layer") ?? "PLOT AREA";
        double minAreaHa = ParseDouble(GetOption(args, "--min-area-ha"), 0.1);
        double? extentMaxX = GetOption(args, "--extent-max-x") is { } s ? ParseDouble(s, 0) : null;
        string? extentWktPath = GetOption(args, "--extent-wkt");
        string outDir = GetOption(args, "--out") ?? "out";
        string? codesPath = GetOption(args, "--codes");
        bool listLayers = args.Contains("--list-layers");

        if (!File.Exists(dxfPath))
        {
            Console.Error.WriteLine($"DXF not found: {dxfPath}");
            return 1;
        }

        if (listLayers)
        {
            Console.WriteLine("Polygon rings per layer:");
            foreach (var (name, count) in DxfStreamParser.CountRingsPerLayer(dxfPath)
                         .OrderByDescending(kv => kv.Value).Take(40))
            {
                Console.WriteLine($"  {name,-50} {count}");
            }
            return 0;
        }

        // ------------------------------------------------------------------
        // 1) Extract polygon rings on the target layer
        // ------------------------------------------------------------------
        var raw = DxfStreamParser.ReadPolygonRings(dxfPath, layer).ToList();
        Console.WriteLine($"Polygon rings on layer '{layer}': {raw.Count}");

        // ------------------------------------------------------------------
        // 2) Phase-extent filter: WKT boundary polygon (preferred) or X cutoff
        // ------------------------------------------------------------------
        List<DxfPolygon> inExtent;
        if (extentWktPath is not null)
        {
            if (!File.Exists(extentWktPath))
            {
                Console.Error.WriteLine($"Extent WKT not found: {extentWktPath}");
                return 1;
            }

            var extent = WktPolygon.Parse(File.ReadAllText(extentWktPath));
            inExtent = raw.Where(p => extent.Contains(p.Centroid())).ToList();
            Console.WriteLine($"Within extent polygon '{Path.GetFileName(extentWktPath)}': {inExtent.Count}");
        }
        else if (extentMaxX is not null)
        {
            inExtent = raw.Where(p => p.Centroid().X <= extentMaxX.Value).ToList();
            Console.WriteLine($"Within extent (centroid X <= {extentMaxX.Value}): {inExtent.Count}");
        }
        else
        {
            inExtent = raw;
        }

        // ------------------------------------------------------------------
        // 3) Remove sliver artefacts below the area threshold
        // ------------------------------------------------------------------
        var sized = inExtent.Where(p => p.AreaHectares() >= minAreaHa).ToList();
        Console.WriteLine($"After sliver filter (>= {minAreaHa} ha): {sized.Count}");

        // ------------------------------------------------------------------
        // 4) Deduplicate (identical rounded centroid + area)
        // ------------------------------------------------------------------
        var deduped = sized
            .GroupBy(p =>
            {
                var c = p.Centroid();
                return (X: Math.Round(c.X / 100), Y: Math.Round(c.Y / 100), A: Math.Round(p.AreaHectares(), 3));
            })
            .Select(g => g.First())
            .OrderByDescending(p => p.AreaHectares())
            .ToList();
        Console.WriteLine($"After dedupe: {deduped.Count}");

        if (deduped.Count == 0)
        {
            Console.Error.WriteLine("No parcels extracted; check --layer / --extent-wkt / --extent-max-x.");
            return 1;
        }

        // ------------------------------------------------------------------
        // 5) Assign stable codes (seeded names for the largest parcels)
        // ------------------------------------------------------------------
        var seedCodes = LoadSeedCodes(codesPath);

        double minX = deduped.SelectMany(p => p.Vertices).Min(v => v.X);
        double maxY = deduped.SelectMany(p => p.Vertices).Max(v => v.Y);

        var items = new List<PlotSeedItem>();
        int synthCounter = 1;
        for (int i = 0; i < deduped.Count; i++)
        {
            var poly = deduped[i];
            var seed = seedCodes.FirstOrDefault(sc => sc.Rank == i + 1);

            string code, name, landUse;
            bool isLocked;
            if (seed is not null)
            {
                (code, name, isLocked) = (seed.Code, seed.Name, seed.IsLocked);
                landUse = seed.LandUseType ?? "Industrial";
            }
            else
            {
                code = $"IND-{synthCounter:D3}";
                name = $"Industrial Plot {synthCounter:D3}";
                landUse = "Industrial";
                isLocked = false;
                synthCounter++;
            }

            var centroid = poly.Centroid();
            items.Add(new PlotSeedItem(
                Code: code,
                DisplayName: name,
                LandUseType: landUse,
                AreaHectares: (decimal)Math.Round(poly.AreaHectares(), 4),
                Status: isLocked ? "Unavailable" : "Available",
                SvgPath: BuildSvgPath(poly, minX, maxY),
                Centroid: new Centroid(
                    Math.Round((centroid.X - minX) / 100.0, 2),
                    Math.Round((maxY - centroid.Y) / 100.0, 2)),
                IsLocked: isLocked,
                MultiTenantBlockEnabled: false,
                BoundaryWkt: BuildWkt(poly)));
        }

        // ------------------------------------------------------------------
        // 6) Emit artefacts
        // ------------------------------------------------------------------
        Directory.CreateDirectory(outDir);

        var jsonPath = Path.Combine(outDir, "plots-seed.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        var sqlPath = Path.Combine(outDir, "plots-seed.sql");
        File.WriteAllText(sqlPath, BuildSeedSql(items));

        var svgPath = Path.Combine(outDir, "preview.svg");
        File.WriteAllText(svgPath, BuildPreviewSvg(items, deduped, minX, maxY));

        Console.WriteLine();
        Console.WriteLine($"Wrote {items.Count} plots:");
        Console.WriteLine($"  {jsonPath}");
        Console.WriteLine($"  {sqlPath}");
        Console.WriteLine($"  {svgPath}");
        Console.WriteLine();
        Console.WriteLine("Largest parcels:");
        foreach (var p in items.Take(12))
        {
            Console.WriteLine($"  {p.Code,-8} {p.AreaHectares,8:F2} ha  {(p.IsLocked ? "[locked] " : "")}{p.DisplayName}");
        }

        return 0;
    }

    private static double ParseDouble(string? value, double fallback) =>
        value is not null ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;

    private static List<SeedCode> LoadSeedCodes(string? path)
    {
        path ??= File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "seed-codes.json"))
            ? Path.Combine(Directory.GetCurrentDirectory(), "seed-codes.json")
            : Path.Combine(AppContext.BaseDirectory, "seed-codes.json");

        if (!File.Exists(path))
        {
            Console.WriteLine("No seed-codes.json found; all plots get synthesised IND-nnn codes.");
            return new List<SeedCode>();
        }

        return JsonSerializer.Deserialize<List<SeedCode>>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SeedCode>();
    }

    /// <summary>
    /// SVG path in plan metres with the y-axis flipped for direct screen rendering:
    /// x' = (x - minX) / 100, y' = (maxY - y) / 100.
    /// </summary>
    private static string BuildSvgPath(DxfPolygon poly, double minX, double maxY)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < poly.Vertices.Count; i++)
        {
            var v = poly.Vertices[i];
            sb.Append(i == 0 ? "M" : "L");
            sb.Append(((v.X - minX) / 100.0).ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(((maxY - v.Y) / 100.0).ToString("F2", CultureInfo.InvariantCulture));
        }
        sb.Append('Z');
        return sb.ToString();
    }

    /// <summary>WKT polygon in plan metres (raw cm / 100), original CAD orientation.</summary>
    private static string BuildWkt(DxfPolygon poly)
    {
        var sb = new StringBuilder("POLYGON ((");
        void Append(Point2 v)
        {
            sb.Append((v.X / 100.0).ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append((v.Y / 100.0).ToString("F3", CultureInfo.InvariantCulture));
        }

        for (int i = 0; i < poly.Vertices.Count; i++)
        {
            Append(poly.Vertices[i]);
            sb.Append(", ");
        }
        Append(poly.Vertices[0]); // WKT rings must close explicitly
        sb.Append("))");
        return sb.ToString();
    }

    private static string BuildSeedSql(List<PlotSeedItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Auto-generated by LFZ.Tools.PlotExtractor. Re-run the extractor instead of editing.");
        sb.AppendLine("-- SQL Server dialect: Boundary is GEOMETRY (plan metres, SRID 0).");
        sb.AppendLine();
        foreach (var p in items)
        {
            var name = p.DisplayName.Replace("'", "''");
            sb.AppendLine(
                "INSERT INTO Plots (Code, DisplayName, LandUseType, AreaHectares, Status, SvgPath, Centroid, IsLocked, MultiTenantBlockEnabled, Boundary) VALUES (" +
                $"'{p.Code}', '{name}', '{p.LandUseType}', {p.AreaHectares.ToString(CultureInfo.InvariantCulture)}, '{p.Status}', " +
                $"'{p.SvgPath}', geometry::STGeomFromText('POINT ({p.Centroid.X.ToString(CultureInfo.InvariantCulture)} {p.Centroid.Y.ToString(CultureInfo.InvariantCulture)})', 0), " +
                $"{(p.IsLocked ? 1 : 0)}, {(p.MultiTenantBlockEnabled ? 1 : 0)}, " +
                $"geometry::STGeomFromText('{p.BoundaryWkt}', 0));");
        }
        return sb.ToString();
    }

    private static string BuildPreviewSvg(List<PlotSeedItem> items, List<DxfPolygon> polys, double minX, double maxY)
    {
        double maxX = polys.SelectMany(p => p.Vertices).Max(v => v.X);
        double minY = polys.SelectMany(p => p.Vertices).Min(v => v.Y);
        double width = (maxX - minX) / 100.0;
        double height = (maxY - minY) / 100.0;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width:F0} {height:F0}\">");
        sb.AppendLine("  <style>path{stroke:#333;stroke-width:2;fill:#5BBF72;fill-opacity:.6}path.locked{fill:#9CA3AF;pointer-events:none}</style>");
        foreach (var p in items)
        {
            var cls = p.IsLocked ? " class=\"locked\"" : string.Empty;
            sb.AppendLine($"  <path id=\"{p.Code}\"{cls} d=\"{p.SvgPath}\"><title>{p.Code} — {p.DisplayName} ({p.AreaHectares:F2} ha)</title></path>");
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string? GetOption(string[] args, string name)
    {
        int idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            LFZ.Tools.PlotExtractor — extracts plot polygons from the LFZ master-plan DXF.

            Usage:
              LFZ.Tools.PlotExtractor <plan.dxf> [options]

            Options:
              --layer <name>          Source layer (default: "PLOT AREA")
              --min-area-ha <n>       Sliver threshold in hectares (default: 0.1)
              --extent-wkt <file>     Phase extent: WKT POLYGON in raw drawing units (preferred)
              --extent-max-x <units>  Phase extent fallback: keep parcels whose centroid X <= this value
              --out <dir>             Output directory (default: ./out)
              --codes <file>          Seed-code JSON (default: seed-codes.json next to the exe)
              --list-layers           Print ring counts per layer and exit
            """);
    }
}
