# YoloDotNet WPF Application - Quick Start Guide

## Step 1: Prerequisites

Install .NET 8 SDK:
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- Verify: `dotnet --version`

## Step 2: Prepare Your Model

Convert your YOLOv8 model from .pt to ONNX:

```python
from ultralytics import YOLO

# Load your trained model
model = YOLO('path/to/your/best.pt')

# Export to ONNX format
results = model.export(format='onnx', imgsz=640)
print(f"Model exported to: {results}")
```

**Output**: `best.onnx` will be created

## Step 3: Setup Paths

Edit `MainWindow.xaml.cs`:

```csharp
var modelPath = @"C:\YourProject\best.onnx";
var setupImagePath = @"C:\YourProject\09_L2.jpg";
var configPath = @"C:\YourProject\ray_config.json";
```

## Step 4: Build & Run

```bash
# Restore dependencies
dotnet restore

# Run application
dotnet run --configuration Release
```

**First Run**: May take 30-60 seconds to load model (normal)

## Step 5: Setup Workflow

### Phase 1: Configuration Setup
1. Launch application → click "Setup" tab
2. Click "Load / Start Setup" button
3. Click "Load Setup Image" button
4. Image appears with detected objects as colored circles

### Phase 2: Set Direction Points
1. **Object A**: Click on the image where you want Object A's ray direction
   - Status: "Object A click saved. Now click direction point for Object B."
2. **Object B**: Click on the image where you want Object B's ray direction
   - Status: "Object B click saved. Click Save Config."

### Phase 3: Save Configuration
1. Click "Save Config" button
2. Configuration saved to: `ray_config.json`
3. Status shows: "Saved to C:\YourProject\ray_config.json"

### Phase 4: Run Inference
1. Switch to "Inference" tab
2. Click "Load Configuration" button
3. Click "Select Image" to choose a test image
4. Click "Run Inference" button
5. Result shows:
   - Annotated image with hit points
   - Measured distance in pixels
   - "OK" or "NOK" based on configured min/max distance

## Example ray_config.json

```json
{
  "objA": {
    "classId": 0,
    "className": "PCB",
    "dir": [0.707, 0.707]
  },
  "objB": {
    "classId": 1,
    "className": "Connector",
    "dir": [0.0, 1.0]
  },
  "minDist": 50.0,
  "maxDist": 100.0
}
```

## Common Issues & Solutions

### Issue: "Failed to load model"
**Solution**: 
- Check file exists at modelPath
- Verify it's a .onnx file (not .pt)
- Try absolute path instead of relative

### Issue: Objects not detected
**Solution**:
- Verify model was trained on your classes
- Check image quality and lighting
- Reduce confidence threshold (modify `YoloInferenceService.cs`)

### Issue: "Ray did not hit mask boundary"
**Solution**:
- Click direction point OUTSIDE the polygon boundary
- Click farther from center of object
- Verify objects were detected in setup phase

### Issue: Application crashes on startup
**Solution**:
- Verify .NET 8 installed: `dotnet --version`
- Delete bin/obj folders and rebuild: `dotnet clean && dotnet restore`
- Check Event Viewer for detailed error

## Architecture Overview

```
User Interface (WPF)
    ↓
ViewModels (MVVM Toolkit)
    ↓
Services Layer
    ├── YoloInferenceService (YoloDotNet)
    ├── GeometryService (NetTopologySuite)
    ├── ImageService (SkiaSharp)
    └── ConfigService (System.Text.Json)
    ↓
Data Models
```

## Performance Optimization

### For Faster Inference
Edit `YoloInferenceService.cs`:
```csharp
new YoloOptions
{
    ModelPath = modelPath,
    Cuda = true,  // Enable GPU (requires NVIDIA CUDA)
    Performance = YoloPerformance.Fast  // Use smaller model variant
}
```

### For Better Accuracy
```csharp
Performance = YoloPerformance.Accurate  // Use larger model variant
```

## Customization Examples

### Change Detection Confidence Threshold
In `YoloInferenceService.cs`, modify `RunSegmentationAsync()`:
```csharp
var results = await Task.Run(() => _model.RunSegmentationAsync(image, confidence: 0.3f));
```

### Add More Classes
The UI automatically populates classes from model:
```csharp
var classes = _inferenceService.GetModelClasses();
// Automatically shows all available classes in dropdowns
```

### Customize Distance Thresholds
Edit `ray_config.json`:
```json
{
  ...
  "minDist": 25.0,
  "maxDist": 75.0
}
```

## Keyboard Shortcuts (Potential Future Feature)
- `Ctrl+L`: Load image
- `Ctrl+S`: Save config
- `Ctrl+R`: Run inference
- `Ctrl+Z`: Reset points

## Next Steps

1. ✅ Run setup to calibrate
2. ✅ Generate ray_config.json
3. ✅ Test inference on sample images
4. ✅ Adjust min/max distances for your use case
5. ✅ Integrate into production system

## Support Files Included

- `YoloAOIApp.csproj` - Project configuration with NuGet packages
- `Models/CoreModels.cs` - Data structures
- `Services/` - Business logic layer
- `ViewModels/` - Presentation logic
- `Views/` - XAML UI definitions
- `README.md` - Full documentation

---

**Questions?** Check the README.md or examine the source code comments.
