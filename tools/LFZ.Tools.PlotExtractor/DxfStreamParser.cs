namespace LFZ.Tools.PlotExtractor;

/// <summary>
/// Stream-parses an ASCII DXF file (R12/AC1009 as produced by aspose-cad) and yields
/// polygon rings from POLYLINE/VERTEX/SEQEND chains and LWPOLYLINE entities.
///
/// Note: the Aspose DWG->DXF conversion drops the "closed" flag (group 70 bit 1),
/// so closure is detected geometrically: a ring is accepted when it has >= 3 vertices,
/// and a coincident trailing vertex (first == last) is stripped.
/// </summary>
public static class DxfStreamParser
{
    public static IEnumerable<DxfPolygon> ReadPolygonRings(string dxfPath, string? layerFilter = null)
    {
        using var reader = new StreamReader(dxfPath);

        bool inEntities = false;

        // POLYLINE state
        DxfPolygon? current = null;
        bool inVertex = false;
        double vx = 0, vy = 0;
        bool hasVx = false;

        // LWPOLYLINE state
        DxfPolygon? lw = null;
        double lwx = 0;
        bool hasLwx = false;

        string? codeLine;
        while ((codeLine = reader.ReadLine()) is not null)
        {
            var valueLine = reader.ReadLine();
            if (valueLine is null) break;

            if (!int.TryParse(codeLine.Trim(), out int code)) continue;
            var value = valueLine.Trim();

            if (code == 0)
            {
                // LWPOLYLINE terminates at the next entity
                if (lw is not null)
                {
                    var flushedLw = Finalize(lw, layerFilter);
                    lw = null;
                    hasLwx = false;
                    if (flushedLw is not null) yield return flushedLw;
                }

                switch (value)
                {
                    case "ENDSEC":
                        inEntities = false;
                        current = null;
                        inVertex = false;
                        break;

                    case "POLYLINE" when inEntities:
                        // Defensive: flush an unterminated chain
                        if (current is not null)
                        {
                            if (inVertex && hasVx) current.Vertices.Add(new Point2(vx, vy));
                            var stale = Finalize(current, layerFilter);
                            if (stale is not null) yield return stale;
                        }
                        current = new DxfPolygon();
                        inVertex = false;
                        hasVx = false;
                        break;

                    case "VERTEX" when current is not null:
                        if (inVertex && hasVx) current.Vertices.Add(new Point2(vx, vy));
                        inVertex = true;
                        hasVx = false;
                        break;

                    case "SEQEND":
                        if (current is not null)
                        {
                            if (inVertex && hasVx) current.Vertices.Add(new Point2(vx, vy));
                            var done = Finalize(current, layerFilter);
                            if (done is not null) yield return done;
                        }
                        current = null;
                        inVertex = false;
                        break;

                    case "LWPOLYLINE" when inEntities:
                        lw = new DxfPolygon();
                        hasLwx = false;
                        break;

                    default:
                        // Any other entity terminates an open POLYLINE chain defensively
                        if (current is not null && value != "POLYLINE")
                        {
                            if (inVertex && hasVx) current.Vertices.Add(new Point2(vx, vy));
                            var flushed = Finalize(current, layerFilter);
                            if (flushed is not null) yield return flushed;
                            current = null;
                            inVertex = false;
                        }
                        break;
                }
                continue;
            }

            if (code == 2 && value == "ENTITIES")
            {
                inEntities = true;
                continue;
            }

            // ---- POLYLINE / VERTEX attributes ----
            if (current is not null)
            {
                if (!inVertex)
                {
                    if (code == 8) current.Layer = value;
                }
                else
                {
                    if (code == 10 && double.TryParse(value, out double x)) { vx = x; hasVx = true; }
                    else if (code == 20 && double.TryParse(value, out double y)) { vy = y; }
                }
            }

            // ---- LWPOLYLINE attributes ----
            if (lw is not null)
            {
                if (code == 8)
                {
                    lw.Layer = value;
                }
                else if (code == 10 && double.TryParse(value, out double x))
                {
                    lwx = x;
                    hasLwx = true;
                }
                else if (code == 20 && double.TryParse(value, out double y) && hasLwx)
                {
                    lw.Vertices.Add(new Point2(lwx, y));
                    hasLwx = false;
                }
            }
        }
    }

    /// <summary>Accepts rings with >= 3 distinct vertices; strips a coincident trailing vertex.</summary>
    private static DxfPolygon? Finalize(DxfPolygon poly, string? layerFilter)
    {
        if (layerFilter is not null &&
            !string.Equals(poly.Layer, layerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var v = poly.Vertices;
        if (v.Count >= 2 &&
            Math.Abs(v[0].X - v[^1].X) < 1e-6 &&
            Math.Abs(v[0].Y - v[^1].Y) < 1e-6)
        {
            v.RemoveAt(v.Count - 1);
        }

        return v.Count >= 3 ? poly : null;
    }

    /// <summary>Distinct layer names with ring counts (diagnostics).</summary>
    public static Dictionary<string, int> CountRingsPerLayer(string dxfPath)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var poly in ReadPolygonRings(dxfPath))
        {
            counts[poly.Layer] = counts.GetValueOrDefault(poly.Layer) + 1;
        }
        return counts;
    }
}
