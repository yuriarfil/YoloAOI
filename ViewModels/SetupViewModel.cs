using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetTopologySuite.Geometries;
using SkiaSharp;
using YoloAOIApp.Models;
using YoloAOIApp.Services;

namespace YoloAOIApp.ViewModels;

public class SetupViewModel : ObservableObject
{
    private readonly YoloInferenceService _inferenceService;
    private readonly GeometryService _geometryService;
    private readonly ImageService _imageService;
    private readonly ConfigService _configService;

    private string _setupImagePath = string.Empty;
    private string _modelPath = string.Empty;
    private string _configPath = string.Empty;
    private SKBitmap? _setupImageBitmap;
    private ObservableCollection<ClassOption> _availableClasses = new();
    private ClassOption? _selectedObjectA;
    private ClassOption? _selectedObjectB;
    private bool _useSameObject;
    private string _statusMessage = "Ready. Load model and image first.";
    private System.Windows.Media.Imaging.BitmapImage? _displayImage;
    private SetupState _state = new();
    private YoloDetectionResult? _lastDetection;
    private ObservableCollection<MeasurementRule> _measurementRules = new();

    public SetupViewModel() : this(string.Empty, string.Empty, string.Empty) { }

    public SetupViewModel(string modelPath, string setupImagePath, string configPath)
    {
        _inferenceService = new YoloInferenceService();
        _geometryService = new GeometryService();
        _imageService = new ImageService();
        _configService = new ConfigService();

        _modelPath = modelPath;
        _setupImagePath = setupImagePath;
        _configPath = configPath;

        SetupCommand = new AsyncRelayCommand<string>(Setup);
        LoadImageCommand = new AsyncRelayCommand(LoadImage);
        ResetPointsCommand = new RelayCommand(ResetPoints);
        AddRuleCommand = new RelayCommand(AddRule);
        DeleteRuleCommand = new RelayCommand<MeasurementRule>(DeleteRule);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfig);

        _statusMessage = "Ready. Please click 'Load / Start Setup' to begin.";
        OnPropertyChanged(nameof(StatusMessage));
    }

    public ObservableCollection<ClassOption> AvailableClasses
    {
        get => _availableClasses;
        set => SetProperty(ref _availableClasses, value);
    }

    public ClassOption? SelectedObjectA
    {
        get => _selectedObjectA;
        set
        {
            if (SetProperty(ref _selectedObjectA, value))
            {
                _state.SelectedObjectA = value;
                StatusMessage = $"Target Object A set to: {value?.Name ?? "None"}";
                RefreshDisplay();
            }
        }
    }

    public ClassOption? SelectedObjectB
    {
        get => _selectedObjectB;
        set
        {
            if (SetProperty(ref _selectedObjectB, value))
            {
                _state.SelectedObjectB = value;
                if (!UseSameObject)
                    StatusMessage = $"Target Object B set to: {value?.Name ?? "None"}";
                RefreshDisplay();
            }
        }
    }

    public bool UseSameObject
    {
        get => _useSameObject;
        set
        {
            if (SetProperty(ref _useSameObject, value))
            {
                StatusMessage = value ? "Mode: Measuring features on the SAME object." : "Mode: Measuring distance between TWO DIFFERENT objects.";
                RefreshDisplay();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value ?? "NULL STATUS");
    }

    public System.Windows.Media.Imaging.BitmapImage? DisplayImage
    {
        get => _displayImage;
        set => SetProperty(ref _displayImage, value);
    }

    public ObservableCollection<MeasurementRule> MeasurementRules
    {
        get => _measurementRules;
        set => SetProperty(ref _measurementRules, value);
    }

    private string _selectedCalculationMethod = "DistanceObject";

    public string SelectedCalculationMethod
    {
        get => _selectedCalculationMethod;
        set
        {
            if (SetProperty(ref _selectedCalculationMethod, value))
            {
                OnPropertyChanged(nameof(IsOffsetObjectSelected));
                if (value == "OffsetObject" || value == "AngleObject")
                {
                    StatusMessage = $"{value} mode selected. Click 'Add Measurement Rule' directly (no clicks needed).";
                }
                else
                {
                    StatusMessage = $"Mode: {value} selected. Click on the image to define directions.";
                }
            }
        }
    }

    public bool IsOffsetObjectSelected => SelectedCalculationMethod == "OffsetObject";

    public ObservableCollection<string> AvailableCalculationMethods { get; } = new()
    {
        "DistanceObject",
        "OffsetObject",
        "AngleObject"
    };

    public ObservableCollection<string> OffsetMeasurementOptions { get; } = new()
    {
        "AlongAxis",
        "Perpendicular",
        "Both"
    };

    private string _selectedOffsetMeasurement = "AlongAxis";

    public string SelectedOffsetMeasurement
    {
        get => _selectedOffsetMeasurement;
        set => SetProperty(ref _selectedOffsetMeasurement, value);
    }

    public IAsyncRelayCommand<string> SetupCommand { get; }
    public IAsyncRelayCommand LoadImageCommand { get; }
    public IRelayCommand ResetPointsCommand { get; }
    public IRelayCommand AddRuleCommand { get; }
    public IRelayCommand<MeasurementRule> DeleteRuleCommand { get; }
    public IAsyncRelayCommand SaveConfigCommand { get; }

    private async Task Setup(string? parameter)
    {
        try
        {
            StatusMessage = "Initialising YOLO model...";
            var loaded = await _inferenceService.LoadModelAsync(_modelPath);
            if (!loaded)
            {
                StatusMessage = $"ERROR: Model not found at {Path.GetFileName(_modelPath)}";
                return;
            }

            var classes = _inferenceService.GetModelClasses();
            AvailableClasses = new(classes);

            if (classes.Count > 0) SelectedObjectA = classes[0];
            if (classes.Count > 1) SelectedObjectB = classes[1];

            StatusMessage = $"Model Loaded. Found {classes.Count} classes. Now click 'Load Setup Image'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR during Setup: {ex.Message}";
        }
    }

    private async Task LoadImage()
    {
        try
        {
            if (!File.Exists(_setupImagePath))
            {
                StatusMessage = $"ERROR: Image not found at {_setupImagePath}";
                return;
            }

            StatusMessage = "Detecting objects in setup image...";
            _setupImageBitmap = _imageService.LoadImage(_setupImagePath);

            if (_setupImageBitmap == null)
            {
                StatusMessage = "ERROR: Failed to decode image.";
                return;
            }

            _lastDetection = await _inferenceService.DetectAsync(_setupImageBitmap);

            if (_lastDetection == null || _lastDetection.TotalDetectedCount == 0)
            {
                StatusMessage = "WARNING: No objects detected. Please check your model or image.";
                RefreshDisplay();
                return;
            }

            _state.LastDetection = _lastDetection;
            RefreshDisplay();

            _state.Stage = "A_dir";
            var detectedList = string.Join(", ", _lastDetection.DetectedClassNames.Distinct());
            var guide = UseSameObject ? "CLICK two points on the same object." : "CLICK direction point for Object A.";
            StatusMessage = $"Success: Found {_lastDetection.TotalDetectedCount} objects [{detectedList}]. {guide}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR during LoadImage: {ex.Message}";
        }
    }

    public void HandleImageClick((int X, int Y) point)
    {
        if (_state.Stage == "A_dir")
        {
            _state.ObjectAClick = point;
            _state.Stage = "B_dir";
            StatusMessage = UseSameObject ? "First point saved. CLICK the second point." : "Object A point saved. CLICK direction for Object B.";
        }
        else if (_state.Stage == "B_dir")
        {
            _state.ObjectBClick = point;
            _state.Stage = "done";
            StatusMessage = "Both points saved. Click 'Add Measurement Rule' to register this pair.";
        }
        else
        {
            StatusMessage = "Points already set. Click 'Reset Points' or 'Add Rule'.";
        }

        RefreshDisplay();
    }

    private void ResetPoints()
    {
        _state.ObjectAClick = null;
        _state.ObjectBClick = null;
        _state.Stage = "A_dir";
        RefreshDisplay();
        StatusMessage = "Points reset. Please click on the image again.";
    }

    private void AddRule()
    {
        bool isRayBased = SelectedCalculationMethod != "OffsetObject" && SelectedCalculationMethod != "AngleObject";

        if (isRayBased && (_state.ObjectAClick == null || _state.ObjectBClick == null))
        {
            StatusMessage = "ERROR: You must click two points on the image first.";
            return;
        }

        var originA = _lastDetection?.Origins.GetValueOrDefault(_selectedObjectA?.Id ?? -1);
        var polyA = _lastDetection?.Polygons.GetValueOrDefault(_selectedObjectA?.Id ?? -1);

        var originB = UseSameObject ? originA : _lastDetection?.Origins.GetValueOrDefault(_selectedObjectB?.Id ?? -1);
        var polyB = UseSameObject ? polyA : _lastDetection?.Polygons.GetValueOrDefault(_selectedObjectB?.Id ?? -1);

        if (originA == null || polyA == null || (originB == null && !UseSameObject) || (polyB == null && !UseSameObject))
        {
            StatusMessage = "ERROR: Target classes not detected in this image. Cannot compute measurements.";
            return;
        }

        Vector2 dirA_screen = Vector2.Zero;
        Vector2 dirA_local = Vector2.Zero;
        Vector2 dirB_screen = Vector2.Zero;
        Vector2 dirB_local = Vector2.Zero;

        if (isRayBased)
        {
            var clickA = _state.ObjectAClick!.Value;
            var clickB = _state.ObjectBClick!.Value;

            // Object A: Screen direction
            dirA_screen = NormalizeVector(
                ((float)clickA.X - (float)originA.X,
                 (float)clickA.Y - (float)originA.Y)
            );

            // Object A: Local direction (Rotation-aware)
            double angleA = _geometryService.GetMainAxisAngle(polyA);
            dirA_local = _geometryService.RotateVector(dirA_screen, -angleA);

            if (originB == null || polyB == null)
            {
                StatusMessage = "ERROR: Target classes not detected in this image. Cannot compute rays.";
                return;
            }

            // Object B: Screen direction
            dirB_screen = NormalizeVector(
                ((float)clickB.X - (float)originB.X,
                 (float)clickB.Y - (float)originB.Y)
            );

            // Object B: Local direction (Rotation-aware)
            double angleB = _geometryService.GetMainAxisAngle(polyB);
            dirB_local = _geometryService.RotateVector(dirB_screen, -angleB);
        }

        int ruleIndex = MeasurementRules.Count + 1;
        var rule = new MeasurementRule
        {
            DistanceName = $"distance{ruleIndex}",
            RuleName = UseSameObject ? $"{_selectedObjectA?.Name}-Same" : $"{_selectedObjectA?.Name}-{_selectedObjectB?.Name}",
            UseSameObject = UseSameObject,
            CalculationMethod = SelectedCalculationMethod,
            OffsetMeasurement = SelectedCalculationMethod == "OffsetObject" ? SelectedOffsetMeasurement : "AlongAxis",
            ObjA = new ObjectConfig
            {
                ClassId = _selectedObjectA?.Id ?? 0,
                ClassName = _selectedObjectA?.Name ?? string.Empty,
                Dir = new[] { (double)dirA_screen.X, (double)dirA_screen.Y },
                LocalDir = new[] { (double)dirA_local.X, (double)dirA_local.Y }
            },
            ObjB = new ObjectConfig
            {
                ClassId = UseSameObject ? (_selectedObjectA?.Id ?? 0) : (_selectedObjectB?.Id ?? 1),
                ClassName = UseSameObject ? (_selectedObjectA?.Name ?? string.Empty) : (_selectedObjectB?.Name ?? string.Empty),
                Dir = new[] { (double)dirB_screen.X, (double)dirB_screen.Y },
                LocalDir = new[] { (double)dirB_local.X, (double)dirB_local.Y }
            }
        };

        MeasurementRules.Add(rule);
        ResetPoints();
        StatusMessage = $"SUCCESS: Added {rule.DistanceName} ({SelectedCalculationMethod}). You can add more or Save.";
    }

    private void DeleteRule(MeasurementRule? rule)
    {
        if (rule != null)
        {
            MeasurementRules.Remove(rule);
            StatusMessage = $"Deleted {rule.DistanceName}.";
        }
    }

    private async Task SaveConfig()
    {
        try
        {
            if (MeasurementRules.Count == 0)
            {
                StatusMessage = "ERROR: No rules added. Add at least one rule first.";
                return;
            }

            var config = new RayConfig
            {
                Rules = MeasurementRules.ToList()
            };

            var saved = await Task.Run(() => _configService.SaveConfig(_configPath, config));
            StatusMessage = saved ? $"SUCCESS: Config with {MeasurementRules.Count} rules saved." : "ERROR: Failed to save config.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR during SaveConfig: {ex.Message}";
        }
    }

    private void RefreshDisplay()
    {
        if (_setupImageBitmap == null)
            return;

        var annotated = _imageService.DrawSetupAnnotations(
            _setupImageBitmap,
            _lastDetection,
            _state,
            _selectedObjectA?.Id ?? 0,
            _selectedObjectB?.Id ?? 1,
            UseSameObject
        );

        DisplayImage = _imageService.SkBitmapToBitmapImage(annotated);
    }

    private static Vector2 NormalizeVector((float X, float Y) v)
    {
        var vec = new Vector2(v.X, v.Y);
        var length = vec.Length();
        return length == 0 ? Vector2.Zero : vec / length;
    }
}
