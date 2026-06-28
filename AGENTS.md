# AGENTS.md

## Commands

```bash
dotnet restore           # restore NuGet packages
dotnet build             # compile
dotnet run               # debug run
dotnet run --configuration Release
dotnet publish -c Release -o ./dist   # standalone exe
```

No tests, no lint, no typecheck, no CI.

## Project structure

Single .NET 8 WPF project (`net8.0-windows`). MVVM with CommunityToolkit.Mvvm. Entry: `App.xaml` → `MainWindow.xaml` → loads `SetupView` and `InferenceView` via `Frame.Navigate()`.

Key dependencies: YoloDotNet 4.2 (ONNX segmentation), NetTopologySuite 2.5 (geometry), SkiaSharp 3.119 (image rendering).

## Paths are hardcoded

`MainWindow.xaml.cs:16-18` sets three paths relative to `AppDomain.CurrentDomain.BaseDirectory`:
- `Assets/Models/best.onnx`
- `Assets/Images/setup_image.jpg`
- `Assets/Config/ray_config.json`

Update these for different environments. The YOLO model must be ONNX format (export from `.pt` via `model.export(format='onnx')`).

## Key gotchas

- **`example_ray_config.json` is outdated.** It shows the legacy flat format. The current code uses a `Rules: [MeasurementRule]` array with `CalculationMethod`, `LocalDir`, etc. See `CoreModels.cs`.
- **Three calculation methods:** `DistanceObject` (ray-cast → euclidean), `OffsetObject` (rotation-aware projection of A's centroid onto B's principal axis), `AngleObject` (principal axis angle diff normalized to [0°,90°]).
- **`OffsetMeasurement` sub-mode:** `MeasurementRule.OffsetMeasurement` (`"AlongAxis"`, `"Perpendicular"`, `"Both"`) controls which distances the OffsetObject calculation returns. Set in Setup via the "Offset Result:" ComboBox (only enabled when CalculationMethod is OffsetObject).
- **Rotation-aware directions:** `LocalDir` is preferred over `Dir`. Inference rotates `LocalDir` by the polygon's principal axis angle at runtime.
- **Detections sorted by size** (`YoloInferenceService.cs:89`) — largest polygon per class wins.
- **Confidence threshold** hardcoded to 0.15 in `YoloInferenceService.cs:75`.
- **`InferenceViewModel`** has a parameterless constructor with dummy commands (`async _ => await Task.CompletedTask`) for XAML designer support; the real constructor takes `modelPath, configPath`.
- **Image clicks** are mapped from UI to pixel coordinates in `SetupView.xaml.cs:39-40` (ratio-aware).
- **Logging** uses `System.Diagnostics.Debug.WriteLine` only — no structured logging.

## Config format

`ray_config.json` uses `RayConfig` → `List<MeasurementRule>`. Each rule has `ObjA`/`ObjB` (each with `ClassId`, `ClassName`, `Dir`[screen], `LocalDir`[rotation-aware]), `MinDist`, `MaxDist`, `CalculationMethod`, `UseSameObject`.

## Useful references

- `Services/YoloInferenceService.cs` — model load + segmentation
- `Services/GeometryService.cs` — polygon ops, ray casting, axis angle, offset
- `Services/ImageService.cs` — SkiaSharp → WPF interop, annotation drawing
- `Models/CoreModels.cs` — all data classes
- `Converters/` — XAML value converters (if any)
