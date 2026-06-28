using System.IO;
using System.Linq;
using NetTopologySuite.Geometries;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Models;
using YoloDotNet.Extensions;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloAOIApp.Models;

namespace YoloAOIApp.Services;

public class YoloInferenceService : IDisposable
{
    private Yolo? _model;
    private bool _disposed = false;
    private List<ClassOption> _cachedClasses = new();

    public List<ClassOption> GetModelClasses()
    {
        return _cachedClasses;
    }

    public async Task<bool> LoadModelAsync(string modelPath)
    {
        try
        {
            _model?.Dispose();
            
            // Verify model file exists
            if (!File.Exists(modelPath))
            {
                System.Diagnostics.Debug.WriteLine($"Model file not found at: {Path.GetFullPath(modelPath)}");
                return false;
            }

            var options = new YoloOptions
            {
                ExecutionProvider = new CpuExecutionProvider(modelPath)
            };
            
            _model = new Yolo(options);

            // Try to populate classes from model
            _cachedClasses = new();
            if (_model.OnnxModel.Labels != null)
            {
                foreach (var label in _model.OnnxModel.Labels)
                {
                    _cachedClasses.Add(new ClassOption 
                    { 
                        Id = label.Index, 
                        Name = label.Name 
                    });
                }
            }
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadModelAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<YoloDetectionResult?> DetectAsync(SKBitmap image)
    {
        if (_model == null)
            return null;

        try
        {
            // Lower confidence threshold to 0.15 to be more permissive
            var results = await Task.Run(() => _model.RunSegmentation(image, 0.15));

            if (results.Count == 0)
                return new YoloDetectionResult { TotalDetectedCount = 0 };

            var geometryService = new GeometryService();
            var output = new YoloDetectionResult();

            var classIds = new List<int>();
            var maskCoords = new List<Coordinate[]>();

            output.TotalDetectedCount = results.Count;

            // NEW: Prioritize largest detections to avoid fragments/noise
            var sortedResults = results.OrderByDescending(r => r.GetContourPoints().Count()).ToList();

            // Iterate through each segmentation result
            foreach (var segResult in sortedResults)
            {
                // Access class ID and Name
                int classId = segResult.Label.Index;
                string className = segResult.Label.Name;
                
                classIds.Add(classId);

                // Get the segmentation/mask points using official API
                var contourPoints = segResult.GetContourPoints().ToList();
                int pointCount = contourPoints.Count;
                
                output.DetectedClassNames.Add($"{classId}:{className}({pointCount}pts)");

                // Convert points to coordinates
                var coords = contourPoints.Select(p => new Coordinate((double)p.X, (double)p.Y)).ToArray();
                maskCoords.Add(coords);

                // Since we sorted by size, the first match we find for a class ID is the largest one
                if (!output.Polygons.ContainsKey(classId))
                {
                    var poly = geometryService.MaskToPolygon(coords);
                    if (poly != null)
                    {
                        output.Polygons[classId] = poly;
                        output.Origins[classId] = geometryService.InsidePointOfPolygon(poly);
                    }
                }
            }

            output.ClassIds = classIds.ToArray();
            output.MaskCoordinates = maskCoords.ToArray();

            return output;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DetectAsync error: {ex}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _model?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~YoloInferenceService()
    {
        Dispose();
    }
}


