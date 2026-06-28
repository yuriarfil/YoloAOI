using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Numerics;
using YoloAOIApp.Models;

namespace YoloAOIApp.Services;

public class GeometryService
{
    private static readonly GeometryFactory Factory = new();

    public Polygon? MaskToPolygon(Coordinate[] maskCoordinates)
    {
        // Must have at least 3 points to form a polygon area
        if (maskCoordinates.Length < 3)
            return null;

        try
        {
            // NetTopologySuite requires the first and last point to be identical to close the ring
            var coordsList = maskCoordinates.ToList();
            if (coordsList.Count > 0 && !coordsList[0].Equals2D(coordsList[^1]))
            {
                coordsList.Add(new Coordinate(coordsList[0].X, coordsList[0].Y));
            }

            // Need at least 4 coordinates (3 unique + 1 closing) to form a linear ring
            if (coordsList.Count < 4)
                return null;

            var linear = Factory.CreateLinearRing(coordsList.ToArray());
            var polygon = Factory.CreatePolygon(linear);
            return polygon.IsEmpty ? null : polygon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MaskToPolygon error: {ex.Message}");
            return null;
        }
    }

    public Coordinate InsidePointOfPolygon(Polygon polygon)
    {
        if (polygon == null || polygon.IsEmpty)
            return new Coordinate(0, 0);

        try
        {
            var coords = polygon.ExteriorRing.Coordinates;
            if (coords.Length < 3) return polygon.InteriorPoint.Coordinate;

            // Use Area Moments to find Centroid
            double area = 0;
            double m10 = 0;
            double m01 = 0;

            for (int i = 0; i < coords.Length - 1; i++)
            {
                var p1 = coords[i];
                var p2 = coords[i + 1];
                double common = p1.X * p2.Y - p2.X * p1.Y;

                area += common;
                m10 += common * (p1.X + p2.X);
                m01 += common * (p1.Y + p2.Y);
            }

            area /= 2.0;
            if (Math.Abs(area) > 1e-6)
            {
                double cx = m10 / (6.0 * area);
                double cy = m01 / (6.0 * area);
                var centroid = new Coordinate(cx, cy);

                // For convex-ish shapes like leads/solder, centroid is ideal.
                // We verify it's inside before using it.
                var point = Factory.CreatePoint(centroid);
                if (polygon.Contains(point))
                {
                    return centroid;
                }
            }
        }
        catch
        {
            // Fallback to InteriorPoint on error
        }

        var representative = polygon.InteriorPoint;
        return new Coordinate(representative.X, representative.Y);
    }

    public double GetMainAxisAngle(Polygon polygon)
    {
        if (polygon == null || polygon.IsEmpty) return 0;

        try
        {
            var coords = polygon.ExteriorRing.Coordinates;
            if (coords.Length < 3) return 0;

            // Calculate Polygon Moments using Green's Theorem
            // This is more stable than Point PCA because it's independent of point density
            double area = 0;
            double m10 = 0;
            double m01 = 0;
            double m20 = 0;
            double m02 = 0;
            double m11 = 0;

            for (int i = 0; i < coords.Length - 1; i++)
            {
                var p1 = coords[i];
                var p2 = coords[i + 1];
                double common = p1.X * p2.Y - p2.X * p1.Y;

                area += common;
                m10 += common * (p1.X + p2.X);
                m01 += common * (p1.Y + p2.Y);
                m20 += common * (p1.X * p1.X + p1.X * p2.X + p2.X * p2.X);
                m02 += common * (p1.Y * p1.Y + p1.Y * p2.Y + p2.Y * p2.Y);
                m11 += common * (2 * p1.X * p1.Y + p1.X * p2.Y + p2.X * p1.Y + 2 * p2.X * p2.Y);
            }

            area /= 2.0;
            if (Math.Abs(area) < 1e-6) return 0;

            double cx = m10 / (6.0 * area);
            double cy = m01 / (6.0 * area);

            // Central moments (normalized by area)
            double mu20 = m20 / (12.0 * area) - cx * cx;
            double mu02 = m02 / (12.0 * area) - cy * cy;
            double mu11 = m11 / (24.0 * area) - cx * cy;

            // The angle of the principal axis
            return 0.5 * Math.Atan2(2 * mu11, mu20 - mu02);
        }
        catch
        {
            return 0;
        }
    }

    public Vector2 RotateVector(Vector2 v, double angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);
        return new Vector2(
            v.X * cos - v.Y * sin,
            v.X * sin + v.Y * cos
        );
    }

    public Coordinate? RayFirstHit(Coordinate origin, Vector2 direction, Polygon polygon)
    {
        var dirNorm = Normalize(direction);
        if (double.IsNaN(dirNorm.X) || double.IsNaN(dirNorm.Y))
            return null;

        var rayEnd = new Coordinate(
            origin.X + dirNorm.X * 100000.0,
            origin.Y + dirNorm.Y * 100000.0
        );

        var ray = Factory.CreateLineString(new[] { origin, rayEnd });
        var intersection = ray.Intersection(polygon.Boundary);

        if (intersection.IsEmpty)
            return null;

        var candidates = ExtractCandidatePoints(intersection);
        if (candidates.Count == 0)
            return null;

        Coordinate? bestPoint = null;
        double bestT = double.MaxValue;

        foreach (var candidatePoint in candidates)
        {
            var vec = new Vector2(
                (float)(candidatePoint.X - origin.X),
                (float)(candidatePoint.Y - origin.Y)
            );
            var t = Vector2.Dot(vec, dirNorm);

            if (t >= 0 && t < bestT)
            {
                bestT = t;
                bestPoint = candidatePoint;
            }
        }

        return bestPoint;
    }

    private List<Coordinate> ExtractCandidatePoints(Geometry intersection)
    {
        var candidates = new List<Coordinate>();

        if (intersection is Point point)
        {
            candidates.Add(point.Coordinate);
        }
        else if (intersection is MultiPoint multiPoint)
        {
            foreach (var geom in multiPoint.Geometries)
            {
                if (geom is Point p)
                    candidates.Add(p.Coordinate);
            }
        }
        else if (intersection is LineString lineString)
        {
            candidates.Add(lineString.Coordinates[0]);
            candidates.Add(lineString.Coordinates[^1]);
        }
        else if (intersection is MultiLineString multiLineString)
        {
            foreach (var geom in multiLineString.Geometries)
            {
                if (geom is LineString ls)
                {
                    candidates.Add(ls.Coordinates[0]);
                    candidates.Add(ls.Coordinates[^1]);
                }
            }
        }

        return candidates;
    }

    public double Distance(Coordinate p1, Coordinate p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Returns the signed offset of Object A along Object B's principal axis.
    /// <para>
    /// This is rotation-aware: when B is horizontal the result equals
    /// <c>centroidA.X - centroidB.X</c> exactly. When B is rotated the
    /// measurement follows B's own orientation instead of the fixed screen X-axis.
    /// </para>
    /// Positive  → A is in the forward direction of B's axis.<br/>
    /// Negative  → A is behind B along B's axis.
    /// </summary>
    public double MeasureXOffset(Polygon polygonA, Polygon polygonB)
    {
        if (polygonA == null || polygonA.IsEmpty || polygonB == null || polygonB.IsEmpty)
            return double.NaN;

        var centroidA = InsidePointOfPolygon(polygonA);
        var centroidB = InsidePointOfPolygon(polygonB);
        double angleB  = GetMainAxisAngle(polygonB);

        // B's principal-axis unit vector
        double ux = Math.Cos(angleB);
        double uy = Math.Sin(angleB);

        double dx = centroidA.X - centroidB.X;
        double dy = centroidA.Y - centroidB.Y;

        // Signed projection of Δcentroid onto B's axis  (= "local X" of A w.r.t. B)
        return dx * ux + dy * uy;
    }

    /// <summary>
    /// Returns all points needed to draw the rotation-aware offset line:
    /// <list type="bullet">
    ///   <item><c>centroidA</c>  – anchor on Object A (red dot)</item>
    ///   <item><c>centroidB</c>  – anchor on Object B (blue dot)</item>
    ///   <item><c>projPt</c>     – foot of the perpendicular from centroidA onto B's axis (yellow dot)</item>
    ///   <item><c>axisAngle</c>  – B's principal-axis angle in radians (for drawing tick marks)</item>
    /// </list>
    /// Measurement line  : centroidB → projPt  (the measured along-axis offset)<br/>
    /// Perpendicular line: centroidA → projPt  (the lateral gap, shown as context)
    /// </summary>
    public (Coordinate centroidA, Coordinate centroidB, Coordinate projPt, double axisAngle)
        GetXOffsetLinePoints(Polygon polygonA, Polygon polygonB)
    {
        var centroidA = InsidePointOfPolygon(polygonA);
        var centroidB = InsidePointOfPolygon(polygonB);
        double angleB  = GetMainAxisAngle(polygonB);

        double ux = Math.Cos(angleB);
        double uy = Math.Sin(angleB);

        double dx = centroidA.X - centroidB.X;
        double dy = centroidA.Y - centroidB.Y;
        double t  = dx * ux + dy * uy; // along-axis component

        var projPt = new Coordinate(centroidB.X + t * ux, centroidB.Y + t * uy);
        return (centroidA, centroidB, projPt, angleB);
    }

    public double MeasureAngle(Polygon polygonA, Polygon polygonB)
    {
        if (polygonA == null || polygonA.IsEmpty || polygonB == null || polygonB.IsEmpty) return double.NaN;

        var angleA = GetMainAxisAngle(polygonA);
        var angleB = GetMainAxisAngle(polygonB);

        var angleDiff = Math.Abs(angleA - angleB);

        // Normalize angle difference to acute angle [0, PI/2] (0 to 90 degrees)
        // due to principal axes 180-degree symmetry
        if (angleDiff > Math.PI / 2)
        {
            angleDiff = Math.PI - angleDiff;
        }

        return angleDiff * (180 / Math.PI); // Return in degrees
    }

    private static Vector2 Normalize(Vector2 v)
    {
        var length = v.Length();
        return length == 0 ? Vector2.Zero : v / length;
    }
}
