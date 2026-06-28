# 🚀 Build & Deploy Checklist

## Prerequisites ✅
- [ ] .NET 8 SDK installed (`dotnet --version`)
- [ ] YOLO model exported to ONNX format (`best.onnx`)
- [ ] Setup image available (JPG/PNG)
- [ ] Visual Studio 2022+ OR Visual Studio Code + .NET extension

## Pre-Build Setup ✅
- [ ] Navigate to: `d:\Uwi\AOI-DeepLearning\YoloAOIApp`
- [ ] Review `MainWindow.xaml.cs` paths:
  - [ ] `modelPath` → points to `best.onnx`
  - [ ] `setupImagePath` → points to your setup image
  - [ ] `configPath` → output path for `ray_config.json`

## Build Steps ✅

### Option 1: Command Line (Recommended)
```bash
cd d:\Uwi\AOI-DeepLearning\YoloAOIApp
dotnet restore
dotnet build --configuration Release
dotnet run --configuration Release
```

### Option 2: Visual Studio
1. Open `YoloAOIApp.csproj` in Visual Studio
2. Right-click Solution → "Restore NuGet Packages"
3. Build → Build Solution (Ctrl+Shift+B)
4. Debug → Start Without Debugging (Ctrl+F5)

### Option 3: Visual Studio Code
1. Open folder: `d:\Uwi\AOI-DeepLearning\YoloAOIApp`
2. Terminal → New Terminal
3. Run: `dotnet restore && dotnet run`

## First Run Checklist ✅
- [ ] Application window appears (Setup tab active)
- [ ] "Ready. Load model and image first." message shows
- [ ] No errors in Visual Studio output/console
- [ ] System doesn't freeze (model loading may take 30-60s)

## Setup Workflow ✅

### Phase 1: Initialize
- [ ] Click "Load / Start Setup" button
- [ ] Wait for model to load (console shows progress)
- [ ] Status changes to "Model loaded. Load image to continue."
- [ ] Click "Load Setup Image" button

### Phase 2: Detection
- [ ] Setup image appears with detected objects as circles
- [ ] Object circles colored differently (red/green/yellow)
- [ ] Status shows "Ready. Click direction point for Object A."

### Phase 3: Direction Points
- [ ] Click somewhere on image for Object A direction
  - Status: "Object A click saved. Now click direction point for Object B."
- [ ] Click somewhere on image for Object B direction
  - Status: "Object B click saved. Click Save Config."
  - You'll see two labeled points: "A_D" (blue) and "B_D" (yellow)

### Phase 4: Save
- [ ] Click "Save Config" button
- [ ] Status shows: "Saved to C:\path\to\ray_config.json"
- [ ] Check that `ray_config.json` exists and contains:
  ```json
  {
    "objA": {...},
    "objB": {...},
    "minDist": 10.0,
    "maxDist": 20.0
  }
  ```

## Inference Workflow ✅

### Phase 1: Load Config
- [ ] Click "Inference" tab
- [ ] Click "Load Configuration" button
- [ ] Status shows: "Config loaded. Ready for inference."

### Phase 2: Upload Image
- [ ] Click "Select Image" button
- [ ] Choose test image (JPG/PNG)
- [ ] Image loads internally

### Phase 3: Run Inference
- [ ] Click "Run Inference" button
- [ ] Wait 1-5 seconds (depends on model size)
- [ ] Result image appears with:
  - [ ] Red circle at point 1
  - [ ] Blue circle at point 2
  - [ ] Green line connecting them
  - [ ] Distance text: "XX.XXpx OK/NOK"
- [ ] Measurement text shows result

## Troubleshooting ✅

### Build Fails
- [ ] Check .NET 8 installed: `dotnet --version`
- [ ] Clean & restore: `dotnet clean && dotnet restore`
- [ ] Check for write permissions on project folder
- [ ] Try "Rebuild Solution" in Visual Studio

### Application Won't Start
- [ ] Check paths in `MainWindow.xaml.cs` are correct
- [ ] Verify `best.onnx` file exists
- [ ] Check console output for detailed error
- [ ] Try: `dotnet run --configuration Debug` for more info

### Model Won't Load
- [ ] Verify file is ONNX format (not .pt)
- [ ] Try absolute path instead of relative
- [ ] Check file permissions (should be readable)
- [ ] Model size should be reasonable (<500MB)

### Objects Not Detected
- [ ] Check setup image quality
- [ ] Verify model trained on your image type
- [ ] Try adjusting lighting/contrast
- [ ] Check image path is correct

### "Ray did not hit mask boundary"
- [ ] Click direction point OUTSIDE polygon
- [ ] Click farther from center of object
- [ ] Ensure object was detected in setup

### UI Freezes During Inference
- [ ] Normal on first inference (ONNX Runtime initialization)
- [ ] Subsequent inferences should be responsive
- [ ] Can add timeout handling if needed

## Testing Verification ✅

### Setup Tab Tests
- [ ] Model loads without errors
- [ ] Image displays correctly
- [ ] Objects detected and shown with circles
- [ ] Click points register and show labels
- [ ] Config saves successfully

### Inference Tab Tests
- [ ] Config loads without errors
- [ ] Image upload works
- [ ] Inference completes without crashes
- [ ] Result shows annotated image
- [ ] Distance measurement displays
- [ ] "OK" or "NOK" status shown correctly

## Performance Optimization ✅

### If Inference is Slow:
- [ ] Edit `YoloInferenceService.cs`:
  ```csharp
  Cuda = true,  // If GPU available
  Performance = YoloPerformance.Fast  // Faster model
  ```
- [ ] Rebuild: `dotnet build --configuration Release`

### If Memory Usage is High:
- [ ] Use smaller YOLO variant (nano/small instead of large)
- [ ] Reduce image resolution
- [ ] Process one image at a time

## Deployment ✅

### Create Release Binary
```bash
dotnet publish -c Release -o ./bin/Release/publish
```
- Creates standalone executable in `./bin/Release/publish/YoloAOIApp.exe`

### Required Files for Distribution
- [ ] `YoloAOIApp.exe` (main executable)
- [ ] All `.dll` files from publish folder
- [ ] `best.onnx` (model file)
- [ ] `ray_config.json` (configuration)

### Install on Another Machine
1. Copy publish folder to target machine
2. Ensure .NET 8 Runtime installed (if not self-contained)
3. Run `YoloAOIApp.exe`

## Documentation Reference

| File | Purpose |
|------|---------|
| README.md | Full technical docs |
| QUICKSTART.md | Setup guide |
| example_ray_config.json | Config template |
| This checklist | Build & test guide |

## Final Verification ✅

- [ ] All files in `d:\Uwi\AOI-DeepLearning\YoloAOIApp\` present
- [ ] .csproj has correct NuGet packages
- [ ] Paths in MainWindow.xaml.cs updated
- [ ] Model exported to ONNX
- [ ] Application builds without errors
- [ ] Setup workflow completes successfully
- [ ] Inference produces correct results
- [ ] Configuration file generated properly

---

## 🎯 Success Criteria

✅ Application launches without errors  
✅ Model loads and detects objects  
✅ Setup workflow creates ray_config.json  
✅ Inference tab measures distance  
✅ Distance calculation matches expected values  

---

**After this checklist, your WPF application is ready for production use!**

For issues, refer to README.md troubleshooting section or examine source code comments.
