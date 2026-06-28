# YoloDotNet v4.2.0 API Migration Guide

## Summary of Changes

The YoloDotNet 4.2.0 library has significant API changes from the older version used in the generated code. This document describes the corrections made to the three main files.

## Issues Fixed

### 1. YoloInferenceService.cs

**Original Errors:**
- `'Yolo' does not contain 'ClassNames'`
- `'YoloOptions' not found`
- `'YoloPerformance' not found`
- `'Yolo' does not contain 'RunSegmentationAsync'`

**Root Cause:**
The YoloDotNet 4.2.0 API uses a new execution provider architecture. The old API properties and methods no longer exist.

**Changes Made:**
1. **Removed** `YoloOptions` constructor parameters: `ModelPath`, `Cuda`, `Performance`
2. **Added** execution provider dependency: `YoloDotNet.ExecutionProvider.Cpu` (v1.1.0)
3. **Updated** `YoloOptions` to use `ExecutionProvider` parameter instead of model path
4. **Changed** `RunSegmentationAsync()` to `RunSegmentation()` (no longer async)
5. **Replaced** `_model.ClassNames` with a cached list since class names are no longer directly exposed
6. **Added** reflection-based property access to handle dynamic result structure

**New API Pattern:**
```csharp
_model = new Yolo(new YoloOptions
{
    ExecutionProvider = new CpuExecutionProvider(modelPath)
});

var results = _model.RunSegmentation(image);  // Not async anymore
```

### 2. GeometryService.cs

**Original Error:**
- `'Polygon' does not contain 'RepresentativePoint'`

**Root Cause:**
NetTopologySuite v2.5.0 uses `InteriorPoint` (a property) instead of `RepresentativePoint`.

**Changes Made:**
- **Changed** `polygon.RepresentativePoint` to `polygon.InteriorPoint`
- Note: `InteriorPoint` is a property, not a method, so no parentheses

**Updated Method:**
```csharp
public Coordinate InsidePointOfPolygon(Polygon polygon)
{
    var representative = polygon.InteriorPoint;  // Property, not method
    return new Coordinate(representative.X, representative.Y);
}
```

### 3. SetupViewModel.cs

**Original Errors:**
- Cannot convert type 'float[]' to 'double[]'

**Root Cause:**
The RayConfig's Dir property expects double[], but the normalized vector is float[].

**Changes Made:**
- **Cast** each float value to double in the array initialization
- Changed `Dir = new[] { dirA.X, dirA.Y }` to `Dir = new[] { (double)dirA.X, (double)dirA.Y }`

**Updated Code:**
```csharp
Dir = new[] { (double)dirA.X, (double)dirA.Y }
```

**Bonus Fixes:**
- Initialized command properties in default constructor to fix nullable reference warnings

## Project Configuration Changes

### YoloAOIApp.csproj

**Added:**
```xml
<PackageReference Include="YoloDotNet.ExecutionProvider.Cpu" Version="1.1.0" />
```

This execution provider:
- Requires `YoloDotNet >= 4.0.0`
- Is compatible with `YoloDotNet 4.2.0`
- Provides CPU-based inference using ONNX Runtime
- Is fully portable (Windows, Linux, macOS)

## Important Notes for Further Development

### Runtime Behavior

The `YoloInferenceService.cs` currently uses reflection to access result properties because the exact structure of the Segmentation result type couldn't be definitively determined from documentation. When the actual API is confirmed:

1. Replace the reflection-based property access with direct property/method calls
2. Expected result structure (to be verified):
   - Each result should have a way to access the class ID
   - Each result should have access to segmentation mask points
   - The exact property/method names need to be confirmed from:
     - YoloDotNet source code
     - Runtime debugging output
     - Official examples in the YoloDotNet demo projects

### Model Metadata

The `ClassNames` property is no longer available on the `Yolo` class. Options to implement class name support:

1. Store class names externally (in a config file or metadata file)
2. Populate from model analysis before loading
3. Extract from first detection results if available
4. Use a lookup table keyed by class ID

### Async Changes

`RunSegmentation()` is no longer `async`. The code wraps it in `Task.Run()` for compatibility with the async call pattern expected by the view model.

## Files Modified

1. `YoloAOIApp.csproj` - Added execution provider package
2. `Services/YoloInferenceService.cs` - Updated to new API
3. `Services/GeometryService.cs` - Fixed NetTopologySuite compatibility
4. `ViewModels/SetupViewModel.cs` - Fixed type conversion
5. `ViewModels/InferenceViewModel.cs` - Fixed nullable property initialization

## Build Status

✅ All compilation errors resolved
⚠️ Runtime behavior of YoloInferenceService.DetectAsync() needs verification
   - Uses reflection-based fallback for result property access
   - Should be updated when exact API is confirmed

## Next Steps

1. Test with actual YOLO model to verify result structure
2. Update property access if reflection fallback isn't working
3. Implement proper class name mapping strategy
4. Review and test segmentation mask extraction accuracy
