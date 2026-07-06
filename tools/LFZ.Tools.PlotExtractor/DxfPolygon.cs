namespace LFZ.Tools.PlotExtractor;

/// <summary>A 2D point in raw drawing units (centimetres in the LFZ DWG).</summary>
public readonly record struct Point2(double X, double Y);

/// <summary>A closed polyline extracted from the DXF.</summary>
public class DxfPolygon
{
    public string Layer { get; set; } = string.Empty;
    public List<Point2> Vertices { get; init; } = new();

    /// <summary>Signed shoelace area in raw units² (cm²).</summary>
    public double SignedAreaRaw()
    {
        double sum = 0;
        var v = Vertices;
        for (int i = 0; i < v.Count; i++)
        {
            var a = v[i];
            var b = v[(i + 1) % v.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2.0;
    }

    /// <summary>Area in hectares. Raw units are cm: cm² / 10^8 = ha.</summary>
    public double AreaHectares() => Math.Abs(SignedAreaRaw()) / 1e8;

    public Point2 Centroid()
    {
        double cx = 0, cy = 0, a = 0;
        var v = Vertices;
        for (int i = 0; i < v.Count; i++)
        {
            var p = v[i];
            var q = v[(i + 1) % v.Count];
            double cross = p.X * q.Y - q.X * p.Y;
            a += cross;
            cx += (p.X + q.X) * cross;
            cy += (p.Y + q.Y) * cross;
        }
        a /= 2.0;
        if (Math.Abs(a) < 1e-9)
        {
            // Degenerate: fall back to vertex average
            return new Point2(v.Average(p => p.X), v.Average(p => p.Y));
        }
        return new Point2(cx / (6 * a), cy / (6 * a));
    }
}
