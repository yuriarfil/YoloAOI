using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetTopologySuite.Geometries;
using SkiaSharp;
using YoloAOIApp.Models;
using YoloAOIApp.Services;

namespace YoloAOIApp.ViewModels;

public class InferenceViewModel : ObservableObject
{
    private readonly YoloInferenceService _inferenceService;
    private readonly GeometryService _geometryService;
    private readonly ImageService _imageService;
    private readonly ConfigService _configService;

    private string _modelPath = string.Empty;
    private string _configPath = string.Empty;
    private SKBitmap? _uploadedImage;
    private string _statusMessage = "Ready. Upload image to run inference.";
    private System.Windows.Media.Imaging.BitmapImage? _resultImage;
    private string _measurementText = string.Empty;
    private RayConfig? _config;

    public InferenceViewModel()
    {
        _inferenceService = new YoloInferenceService();
        _geometryService = new GeometryService();
        _imageService = new ImageService();
        _configService = new ConfigService();
        
        // Initialize command properties to prevent null reference warnings
        LoadConfigCommand = new AsyncRelayCommand<string>(async _ => await Task.CompletedTask);
        RunInferenceCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public System.Windows.Media.Imaging.BitmapImage? ResultImage
    {
        get => _resultImage;
        set => SetProperty(ref _resultImage, value);
    }

    public string MeasurementText
    {
        get => _measurementText;
        set => SetProperty(ref _measurementText, value);
    }

    public IAsyncRelayCommand<string> LoadConfigCommand { get; }
    public IAsyncRelayCommand RunInferenceCommand { get; }

    public InferenceViewModel(string modelPath, string configPath) : this()
    {
        _modelPath = modelPath;
        _configPath = configPath;

        LoadConfigCommand = new AsyncRelayCommand<string>(LoadConfig);
        RunInferenceCommand = new AsyncRelayCommand(RunInference);
    }

    public async Task SetImageAsync(byte[] imageBytes)
    {
        try
        {
            _uploadedImage = _imageService.LoadFromByteArray(imageBytes);
            if (_uploadedImage == null)
            {
                StatusMessage = "Failed to load image";
                return;
            }

            // Immediately display the uploaded image
            ResultImage = _imageService.SkBitmapToBitmapImage(_uploadedImage);
            StatusMessage = "Image loaded. Click Run Inference.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading image: {ex.Message}";
        }
        await Task.CompletedTask;
    }

    private async Task LoadConfig(string? parameter)
    {
        try
        {
            StatusMessage = "Loading configuration and model...";
            _config = await Task.Run(() => _configService.LoadConfig(_configPath));

            if (_config == null)
            {
                StatusMessage = "Failed to load config. Run Setup first.";
                return;
            }

            // Also load the model
            var modelLoaded = await _inferenceService.LoadModelAsync(_modelPath);
            if (!modelLoaded)
            {
                StatusMessage = "Config loaded, but failed to load YOLO model.";
                return;
            }

            StatusMessage = "Config and Model loaded. Ready for inference.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task RunInference()
    {
        try
        {
            if (_uploadedImage == null)
            {
                StatusMessage = "No image uploaded";
                return;
            }

            if (_config == null || _config.Rules.Count == 0)
            {
                StatusMessage = "Config not loaded or has no rules. Load config first.";
                return;
            }

            StatusMessage = "Running inference...";

            var detection = await _inferenceService.DetectAsync(_uploadedImage);

            if (detection == null || detection.TotalDetectedCount == 0)
            {
                StatusMessage = "No objects detected in image.";
                ResultImage = _imageService.SkBitmapToBitmapImage(_uploadedImage);
                return;
            }

            var output = new InferenceOutput();
            var resultLines = new List<string>();

            foreach (var rule in _config.Rules)
            {
                var mResult = new MeasurementResult
                {
                    DistanceName = rule.DistanceName,
                    RuleName = rule.RuleName
                };

                var originA = detection.Origins.GetValueOrDefault(rule.ObjA?.ClassId ?? -1);
                var polyA = detection.Polygons.GetValueOrDefault(rule.ObjA?.ClassId ?? -1);
                
                var originB = detection.Origins.GetValueOrDefault(rule.ObjB?.ClassId ?? -1);
                var polyB = detection.Polygons.GetValueOrDefault(rule.ObjB?.ClassId ?? -1);

                if (originA == null || polyA == null || originB == null || polyB == null)
                {
                    mResult.Status = "Error";
                    mResult.Message = "Target missing";
                }
                else
                {
                    if (rule.CalculationMethod == "OffsetObject")
                    {
                        // Get projection geometry for drawing
                        var (cA, cB, projPt, axisAngle) = _geometryService.GetXOffsetLinePoints(polyA, polyB);

                        var xOffset = _geometryService.MeasureXOffset(polyA, polyB);
                        var perpDistance = _geometryService.Distance(cA, projPt);

                        if (double.IsNaN(xOffset) || double.IsNaN(perpDistance))
                        {
                            mResult.Status = "Error";
                            mResult.Message = "Calc error";
                        }
                        else
                        {
                            var offsetMode = rule.OffsetMeasurement ?? "AlongAxis";

                            if (offsetMode == "AlongAxis" || offsetMode == "Both")
                            {
                                // Along-axis offset: signed projection of (centroidA - centroidB) onto B's axis
                                var absOffset = Math.Abs(xOffset);
                                var status = absOffset >= rule.MinDist && absOffset <= rule.MaxDist ? "OK" : "NOK";
                                mResult.Distance = xOffset;
                                mResult.Status = status;
                                mResult.Message = $"{xOffset:+0.0;-0.0;0.0}px";
                                mResult.IsXOffset = true;

                                mResult.CentroidA = cA;
                                mResult.CentroidB = cB;
                                mResult.Point1    = cA;
                                mResult.Point2    = projPt;

                                // B's full principal-axis reference line
                                double ux = Math.Cos(axisAngle);
                                double uy = Math.Sin(axisAngle);
                                mResult.CenterlineStart = new Coordinate(cB.X - 1500 * ux, cB.Y - 1500 * uy);
                                mResult.CenterlineEnd   = new Coordinate(cB.X + 1500 * ux, cB.Y + 1500 * uy);
                            }

                            if (offsetMode == "Perpendicular" || offsetMode == "Both")
                            {
                                var status = perpDistance >= rule.MinDist && perpDistance <= rule.MaxDist ? "OK" : "NOK";
                                var perpResult = new MeasurementResult
                                {
                                    DistanceName = offsetMode == "Both" ? rule.DistanceName + "_perp" : rule.DistanceName,
                                    RuleName = rule.RuleName,
                                    Distance = perpDistance,
                                    Status = status,
                                    Message = $"{perpDistance:F1}px",
                                    Point1 = cA,
                                    Point2 = projPt,
                                    IsPerpendicularOffset = true,
                                    SuppressGeometryDrawing = offsetMode == "Both"
                                };

                                // Add axis reference line + centroidB dot for "Perpendicular" only mode
                                if (offsetMode == "Perpendicular")
                                {
                                    double ux = Math.Cos(axisAngle);
                                    double uy = Math.Sin(axisAngle);
                                    perpResult.CenterlineStart = new Coordinate(cB.X - 1500 * ux, cB.Y - 1500 * uy);
                                    perpResult.CenterlineEnd   = new Coordinate(cB.X + 1500 * ux, cB.Y + 1500 * uy);
                                    perpResult.CentroidB = cB;
                                }

                                output.Results.Add(perpResult);
                                resultLines.Add($"{perpResult.DistanceName} | {perpResult.RuleName} | {perpResult.Message} | {perpResult.Status}");
                            }
                        }
                    }
                    else if (rule.CalculationMethod == "AngleObject")
                    {
                        var angle = _geometryService.MeasureAngle(polyA, polyB);
                        if (double.IsNaN(angle))
                        {
                            mResult.Status = "Error";
                            mResult.Message = "Calc error";
                        }
                        else
                        {
                            var status = angle >= rule.MinDist && angle <= rule.MaxDist ? "OK" : "NOK";
                            mResult.Distance = angle;
                            mResult.Status = status;
                            mResult.Message = $"{angle:F1}°";
                            mResult.Point1 = originA;
                            mResult.Point2 = originB;
                        }
                    }
                    else
                    {
                        // Use Rotation-Aware Direction if LocalDir is available, otherwise fallback to Legacy Dir
                        Vector2 dirA, dirB;

                        if (rule.ObjA!.LocalDir != null && (rule.ObjA.LocalDir[0] != 0 || rule.ObjA.LocalDir[1] != 0))
                        {
                            var localDirA = new Vector2((float)rule.ObjA.LocalDir[0], (float)rule.ObjA.LocalDir[1]);
                            double angleA = _geometryService.GetMainAxisAngle(polyA);
                            dirA = _geometryService.RotateVector(localDirA, angleA);
                        }
                        else
                        {
                            dirA = new Vector2((float)rule.ObjA!.Dir[0], (float)rule.ObjA.Dir[1]);
                        }

                        if (rule.ObjB!.LocalDir != null && (rule.ObjB.LocalDir[0] != 0 || rule.ObjB.LocalDir[1] != 0))
                        {
                            var localDirB = new Vector2((float)rule.ObjB.LocalDir[0], (float)rule.ObjB.LocalDir[1]);
                            double angleB = _geometryService.GetMainAxisAngle(polyB);
                            dirB = _geometryService.RotateVector(localDirB, angleB);
                        }
                        else
                        {
                            dirB = new Vector2((float)rule.ObjB!.Dir[0], (float)rule.ObjB.Dir[1]);
                        }

                        var p1 = _geometryService.RayFirstHit(originA, dirA, polyA);
                        var p2 = _geometryService.RayFirstHit(originB, dirB, polyB);

                        if (p1 == null || p2 == null)
                        {
                            mResult.Status = "Error";
                            mResult.Message = "Ray miss";
                        }
                        else
                        {
                            var distance = _geometryService.Distance(p1, p2);
                            var status = distance >= rule.MinDist && distance <= rule.MaxDist ? "OK" : "NOK";
                            
                            mResult.Distance = distance;
                            mResult.Status = status;
                            mResult.Message = $"{distance:F1}px";
                            mResult.Point1 = p1;
                            mResult.Point2 = p2;
                        }
                    }
                }

                // Skip mResult for "Perpendicular" only OffsetObject (already added as perpResult above),
                // unless an error occurred (target missing / calc error) — keep those visible.
                if (rule.CalculationMethod != "OffsetObject" || rule.OffsetMeasurement != "Perpendicular" || mResult.Status == "Error")
                {
                    output.Results.Add(mResult);
                    resultLines.Add($"{mResult.DistanceName} | {mResult.RuleName} | {mResult.Message} | {mResult.Status}");
                }
            }

            var annotated = _imageService.DrawInferenceResult(_uploadedImage, output);
            ResultImage = _imageService.SkBitmapToBitmapImage(annotated);
            
            MeasurementText = string.Join("\n", resultLines);
            StatusMessage = $"Inference complete. {output.Results.Count} rules processed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
