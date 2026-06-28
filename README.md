# YOLO AOI Desktop Application - C# WPF

A .NET 8 WPF desktop application for YOLO instance segmentation and distance measurement between objects.

## Features

✅ **YOLO Instance Segmentation** - Using YoloDotNet for fast inference  
✅ **Geometry Operations** - Ray-casting and distance calculations using NetTopologySuite  
✅ **Interactive Setup** - Click-based direction point selection  
✅ **JSON Configuration** - Save and load measurement configurations  
✅ **Cross-platform** - Works on Windows, with Linux/macOS support via .NET 8

## Prerequisites

- **.NET 8 SDK** or later ([download](https://dotnet.microsoft.com/download))
- **YOLO Model in ONNX format** (export from your best.pt using `yolo export format=onnx`)
- **Setup Image** (for calibration)

## Installation

### 1. Clone / Open Project

```bash
cd YoloAOIApp
dotnet restore
```

### 2. Export Model to ONNX

From your Python environment:
```bash
from ultralytics import YOLO
model = YOLO('best.pt')
model.export(format='onnx')
# This creates best.onnx in the same directory
```

### 3. Update Configuration Paths

Edit `MainWindow.xaml.cs` and update these paths:

```csharp
var modelPath = @"C:\path\to\best.onnx";
var setupImagePath = @"C:\path\to\setup_image.jpg";
var configPath = @"C:\path\to\ray_config.json";
```

### 4. Run Application

```bash
dotnet run
```

## Usage

### Setup Tab

1. **Select Classes**: Choose Object A and B from the detected classes
2. **Load Setup Image**: Click "Load/Start Setup" then "Load Setup Image"
3. **Select Direction Points**: 
   - Click on the setup image where Object A's direction should point
   - Click on the setup image where Object B's direction should point
4. **Save Configuration**: Click "Save Config" to save ray directions to JSON

### Inference Tab

1. **Load Configuration**: Click "Load Configuration" to load saved setup
2. **Upload Image**: Click "Select Image" to choose an image for measurement
3. **Run Inference**: Click "Run Inference" to detect and measure distance
4. **View Results**: Annotated image shows hit points and distance measurement

## Project Structure

```
YoloAOIApp/
├── YoloAOIApp.csproj          # Project file with NuGet dependencies
├── App.xaml / App.xaml.cs     # Application entry point
├── MainWindow.xaml / .cs      # Main window with tabs
├── Models/
│   └── CoreModels.cs          # Data classes (RayConfig, ClassOption, etc.)
├── Services/
│   ├── YoloInferenceService.cs  # YOLO inference wrapper
│   ├── GeometryService.cs       # Polygon & ray-casting logic
│   ├── ImageService.cs          # SkiaSharp image operations
│   └── ConfigService.cs         # JSON config I/O
├── ViewModels/
│   ├── SetupViewModel.cs       # Setup tab logic
│   └── InferenceViewModel.cs   # Inference tab logic
└── Views/
    ├── SetupView.xaml / .cs    # Setup UI
    └── InferenceView.xaml / .cs  # Inference UI
```

## Key Technologies

| Component | Library | Purpose |
|-----------|---------|---------|
| YOLO Inference | YoloDotNet 4.2 | Fast instance segmentation |
| Geometry | NetTopologySuite 2.5 | Polygon & ray-casting operations |
| Image Rendering | SkiaSharp 2.88 | Cross-platform image handling |
| UI Framework | WPF | Windows desktop UI |
| MVVM | MVVM Toolkit 8.2 | Data binding & reactive properties |

## Python to C# Port - Key Mappings

### Libraries
- `ultralytics.YOLO` → `YoloDotNet.Yolo`
- `shapely.Polygon` → `NetTopologySuite.Geometries.Polygon`
- `cv2` → `SkiaSharp`
- `gradio` → `WPF`

### Core Logic
- `mask_to_polygon()` → `GeometryService.MaskToPolygon()`
- `inside_point_of_polygon()` → `GeometryService.InsidePointOfPolygon()`
- `ray_first_hit()` → `GeometryService.RayFirstHit()`
- Image rendering → `ImageService.DrawSetupAnnotations()` / `DrawInferenceResult()`

## Configuration File Format

Generated `ray_config.json`:
```json
{
  "objA": {
    "classId": 0,
    "className": "object_a_name",
    "dir": [0.707, 0.707]
  },
  "objB": {
    "classId": 1,
    "className": "object_b_name",
    "dir": [0.0, 1.0]
  },
  "minDist": 10.0,
  "maxDist": 20.0
}
```

## Troubleshooting

### Model won't load
- Ensure model is in ONNX format (not .pt)
- Check model path is correct and file exists
- Verify .NET 8 runtime is installed

### "Ray did not hit mask boundary"
- Ensure direction points are outside the polygon
- Click farther from the object center
- Verify objects are detected correctly in setup

### Image won't load
- Ensure image format is supported (JPG, PNG, BMP)
- Check file path is correct
- File permissions should allow reading

### UI is slow/frozen
- Inference runs on background thread (normal for first run)
- CUDA support can improve inference speed (edit YoloInferenceService.cs)
- Larger models take longer - consider using smaller variants

## Performance Tips

1. **Use CUDA** (if GPU available):
   ```csharp
   Cuda = true  // in YoloInferenceService
   ```

2. **Adjust Model Size**:
   ```csharp
   Performance = YoloPerformance.Fast  // or Medium/Accurate
   ```

3. **Batch Processing**:
   Can extend InferenceViewModel to process multiple images

## Future Enhancements

- [ ] CUDA/TensorRT support configuration UI
- [ ] Batch processing mode
- [ ] Real-time camera feed support
- [ ] Custom annotation overlays
- [ ] Export results to CSV
- [ ] Model training integration

## License

This project is provided as-is for development purposes.

## Support

For issues with:
- **YoloDotNet**: https://github.com/NickSwardh/YoloDotNet
- **NetTopologySuite**: https://github.com/NetTopologySuite/NetTopologySuite
- **.NET/WPF**: Microsoft .NET documentation

---

**Created**: 2026  
**Framework**: .NET 8  
**Language**: C# 12
