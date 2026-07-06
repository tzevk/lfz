using System.Globalization;
using System.Text.RegularExpressions;

namespace LFZ.Tools.PlotExtractor;

/// <summary>
/// Minimal WKT POLYGON / MULTIPOLYGON reader with point-in-polygon test
/// (exterior rings only). Used for the phase extent filter; coordinates are
/// raw drawing units.
/// </summary>
public class WktPolygon
{
    private readonly List<List<Point2>> _rings;

    private WktPolygon(List<List<Point2>> rings) => _rings = rings;

    public static WktPolygon Parse(string wkt)
    {
        // Capture the exterior ring of every polygon: the first (...) group inside each ((...))
        var rings = new List<List<Point2>>();
        foreach (Match match in Regex.Matches(wkt, @"\(\(([^()]+)\)", RegexOptions.IgnoreCase))
        {
            var ring = new List<Point2>();
            foreach (var pair in match.Groups[1].Value.Split(','))
            {
                var parts = pair.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                ring.Add(new Point2(
                    double.Parse(parts[0], CultureInfo.InvariantCulture),
                    double.Parse(parts[1], CultureInfo.InvariantCulture)));
            }

            if (ring.Count >= 4)
            {
                rings.Add(ring);
            }
        }

        if (rings.Count == 0)
        {
            throw new FormatException("Expected WKT POLYGON ((...)) or MULTIPOLYGON (((...)), ...).");
        }

        return new WktPolygon(rings);
    }

    /// <summary>True when the point lies inside any polygon part (ray casting).</summary>
    public bool Contains(Point2 point)
    {
        foreach (var ring in _rings)
        {
            bool inside = false;
            int j = ring.Count - 1;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
                j = i;
            }
            if (inside)
            {
                return true;
            }
        }
        return false;
    }
}
