using System.Collections.ObjectModel;
using NetTopologySuite.Geometries;

namespace YoloAOIApp.Models;

public class RayConfig
{
    public List<MeasurementRule> Rules { get; set; } = new();
}

public class MeasurementRule
{
    public string DistanceName { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public ObjectConfig? ObjA { get; set; }
    public ObjectConfig? ObjB { get; set; }
    public double MinDist { get; set; } = 10.0;
    public double MaxDist { get; set; } = 20.0;
    public bool UseSameObject { get; set; }
    public string CalculationMethod { get; set; } = "DistanceObject";
    public string OffsetMeasurement { get; set; } = "AlongAxis";
}

public class ObjectConfig
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public double[] Dir { get; set; } = new double[2]; // Screen-fixed direction (Legacy)
    public double[] LocalDir { get; set; } = new double[2]; // Rotation-aware local direction
}

public class YoloDetectionResult
{
    public Dictionary<int, Polygon> Polygons { get; set; } = new();
    public Dictionary<int, Coordinate> Origins { get; set; } = new();
    public Polygon? PolygonA { get; set; } // Legacy or per-rule? Let's keep for now or refactor
    public Polygon? PolygonB { get; set; }
    public Coordinate? OriginA { get; set; }
    public Coordinate? OriginB { get; set; }
    public int[] ClassIds { get; set; } = Array.Empty<int>();
    public Coordinate[][] MaskCoordinates { get; set; } = Array.Empty<Coordinate[]>();
    public int TotalDetectedCount { get; set; }
    public List<string> DetectedClassNames { get; set; } = new();
}

public class InferenceOutput
{
    public byte[]? AnnotatedImageBytes { get; set; }
    public List<MeasurementResult> Results { get; set; } = new();
    public string SummaryMessage { get; set; } = string.Empty;
}

public class MeasurementResult
{
    public string DistanceName { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public double Distance { get; set; }
    public string Status { get; set; } = string.Empty; // OK, NOK, Error
    public string Message { get; set; } = string.Empty;
    public Coordinate? Point1 { get; set; }
    public Coordinate? Point2 { get; set; }
    public Coordinate? CenterlineStart { get; set; }
    public Coordinate? CenterlineEnd { get; set; }
    /// <summary>True when this result uses the X-axis offset method; used to draw the horizontal offset line.</summary>
    public bool IsXOffset { get; set; }
    /// <summary>True when this result is the perpendicular component of an OffsetObject measurement.</summary>
    public bool IsPerpendicularOffset { get; set; }
    /// <summary>When true, skip geometry drawing (geometry already drawn by a companion result).</summary>
    public bool SuppressGeometryDrawing { get; set; }
    /// <summary>Centroid of Object A (origin of the offset line).</summary>
    public Coordinate? CentroidA { get; set; }
    /// <summary>Centroid of Object B (reference for X projection).</summary>
    public Coordinate? CentroidB { get; set; }
}

public class ClassOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName => $"{Id}: {Name}";
}

public class SetupState
{
    public ClassOption? SelectedObjectA { get; set; }
    public ClassOption? SelectedObjectB { get; set; }
    public (int X, int Y)? ObjectAClick { get; set; }
    public (int X, int Y)? ObjectBClick { get; set; }
    public string Stage { get; set; } = "A_dir"; // A_dir, B_dir, done
    public YoloDetectionResult? LastDetection { get; set; }
}
