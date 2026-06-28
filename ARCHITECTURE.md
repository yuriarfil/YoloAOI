# Architecture Diagram & Data Flow

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     WPF Desktop Application                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────────────────────┬──────────────────────────────┐ │
│  │       SETUP TAB              │     INFERENCE TAB            │ │
│  ├──────────────────────────────┼──────────────────────────────┤ │
│  │ • Class Dropdowns            │ • Image Upload Button        │ │
│  │ • Load/Start Setup Button    │ • Run Inference Button       │ │
│  │ • Interactive Image Click    │ • Result Display             │ │
│  │ • Point Visualization        │ • Distance Measurement       │ │
│  │ • Save Config Button         │ • OK/NOK Status              │ │
│  └──────────────────────────────┴──────────────────────────────┘ │
│                                                                   │
├─────────────────────────────────────────────────────────────────┤
│                    VIEWMODELS (MVVM Binding)                     │
├─────────────────────────────────────────────────────────────────┤
│  SetupViewModel              │        InferenceViewModel        │
│  • Class selection logic     │        • Config loading         │
│  • Image click handling      │        • Inference execution    │
│  • Point tracking            │        • Distance calculation   │
│  • Config generation         │        • Result visualization   │
│  • Async operations          │        • Async operations       │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     SERVICES LAYER (Business Logic)              │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  YoloInferenceService                                   │    │
│  │  • Model initialization (YoloDotNet wrapper)            │    │
│  │  • Segmentation inference                               │    │
│  │  • Mask extraction                                      │    │
│  │  • Class mapping                                        │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  GeometryService (NetTopologySuite)                     │    │
│  │  • MaskToPolygon()          - Coordinate array → NTS Polygon      │
│  │  • InsidePointOfPolygon()   - Centroid (area moments) or InteriorPoint │
│  │  • GetMainAxisAngle()       - Area Moments PCA for orientation    │
│  │  • RayFirstHit()            - Ray-polygon intersection            │
│  │  • Distance()               - Euclidean distance calc             │
│  │  • MeasureXOffset()         - Signed along-axis projection of A onto B's axis │
│  │  • GetXOffsetLinePoints()   - Returns centroidA, centroidB, projPt, axisAngle │
│  │  • MeasureAngle()           - Principal-axis angle diff (degrees) │
│  │  • RotateVector()           - Local-to-Global rotation            │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  ImageService (SkiaSharp)                               │    │
│  │  • LoadImage()           - File I/O with SKBitmap       │    │
│  │  • DrawSetupAnnotations() - Polygon & point rendering   │    │
│  │  • DrawInferenceResult()  - Ray visualization           │    │
│  │  • SKBitmapToBitmapImage() - WPF interop               │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  ConfigService (System.Text.Json)                       │    │
│  │  • LoadConfig()         - Load JSON configuration       │    │
│  │  • SaveConfig()         - Save ray directions to JSON   │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      EXTERNAL LIBRARIES                          │
├─────────────────────────────────────────────────────────────────┤
│  YoloDotNet 4.2       │  NetTopologySuite 2.5  │  SkiaSharp 2.88  │
│  ONNX Runtime         │  NTS Geometry Engine   │  Image Rendering │
│  Segmentation         │  Polygon Operations    │  Cross-platform  │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow Diagram

### Setup Workflow

```
┌─────────────────┐
│  Load YOLO      │
│  Model          │
│  (best.onnx)    │
└────────┬────────┘
         ↓
    ┌────────────────────┐
    │  Extract Class     │
    │  Names from Model  │
    └────────┬───────────┘
             ↓
      ┌─────────────────┐
      │ Populate Class  │
      │ Dropdowns A & B │
      └────────┬────────┘
               ↓
    ┌─────────────────────┐
    │  Load Setup Image   │
    │  (JPG/PNG)          │
    └────────┬────────────┘
             ↓
    ┌─────────────────────────────────────┐
    │  Run YOLO Segmentation Inference    │
    │  Extract masks for each detection   │
    └────────┬────────────────────────────┘
             ↓
    ┌─────────────────────────────────────┐
    │  Convert Masks to NTS Polygons      │
    │  (Mask coordinates → Polygon)       │
    └────────┬────────────────────────────┘
             ↓
    ┌─────────────────────────────────────┐
    │  Visualize on Image                 │
    │  • Draw polygon outlines            │
    │  • Mark polygon centers (circles)   │
    │  • Label with class IDs             │
    └────────┬────────────────────────────┘
             ↓
    ┌──────────────────────────┐
    │  User Clicks on Image    │
    │  Point A Direction       │
    └────────┬─────────────────┘
             ↓
    ┌──────────────────────────┐
    │  User Clicks on Image    │
    │  Point B Direction       │
    └────────┬─────────────────┘
             ↓
    ┌──────────────────────────┐
    │  Calculate Ray           │
    │  Directions:             │
    │  dirA = normalize(      │
    │    clickA - originA)    │
    │  dirB = normalize(      │
    │    clickB - originB)    │
    └────────┬─────────────────┘
             ↓
    ┌──────────────────────────┐
    │  Iteratively Add Rule to │
    │  MeasurementRules List   │
    └────────┬─────────────────┘
             ↓
    ┌──────────────────────────────┐
    │  Save to ray_config.json     │
    │  {                           │
    │    rules: [                  │
    │      {distance_name, ...},   │
    │      ...                     │
    │    ]                         │
    │  }                           │
    └──────────────────────────────┘
```

### Inference Workflow

```
┌──────────────────────────┐
│  Load ray_config.json    │
│  Extract list of rules   │
└────────┬─────────────────┘
         ↓
┌──────────────────────────┐
│  User Uploads Image      │
└────────┬─────────────────┘
         ↓
┌────────────────────────────────────────┐
│  Run YOLO Segmentation once            │
│  Store ALL class polygons/origins      │
└────────┬─────────────────────────────────┘
         ↓
┌────────────────────────────────────────────────────────┐
│  Loop Through Every Rule (CalculationMethod dispatch): │
│                                                        │
│  DistanceObject:                                       │
│  • Cast Ray A → hit point p1 on A's boundary          │
│  • Cast Ray B → hit point p2 on B's boundary          │
│  • Result = Euclidean Distance(p1, p2)                 │
│                                                        │
│  OffsetObject (rotation-aware):                        │
│  • Compute B's axis: B̂ = (cos θ_B, sin θ_B)          │
│  • signed offset = dot(centroidA − centroidB, B̂)      │
│  • Projection foot P = centroidB + offset × B̂         │
│  • Threshold check: |offset| vs MinDist/MaxDist        │
│  • Overlay: axis line, measurement seg B→P, ⊥ drop A→P│
│                                                        │
│  AngleObject:                                          │
│  • Compute angle of A's and B's principal axes        │
│  • Result = |angleA − angleB| normalized to [0°,90°]  │
│                                                        │
│  • Determine OK/NOK status                             │
└────────┬───────────────────────────────────────────────┘
         ↓
┌────────────────────────────────────────┐
│  Visualize Results                     │
│  • Draw all rays/lines and hit points  │
│  • OffsetObject: dimension-line overlay│
│  • List all results on screen          │
└────────────────────────────────────────┘
```

## Class Diagram

```
┌─────────────────────────────────┐
│         MODELS LAYER             │
├─────────────────────────────────┤
│  RayConfig                       │
│  └─ Rules: List<MeasurementRule> │
└─────────────────────────────────┘
         ↑                 ↑
         │                 │
    ┌────┴───────────┐   ┌────┴───────────┐
    │ MeasurementRule│   │ ObjectConfig   │
    ├────────────────┤   ├────────────────┤
    │ DistanceName   │   │ ClassId        │
    │ RuleName       │   │ ClassName      │
    │ ObjA/ObjB      │   │ Dir: double[]  │
    │ MinDist        │   └────────────────┘
    │ MaxDist        │
    │ CalculationMeth│
    └────────────────┘


┌──────────────────────────────┐
│   DETECTION MODELS            │
├──────────────────────────────┤
│  YoloDetectionResult         │
│  ├─ Polygons: Dict<int, Poly> │
│  ├─ Origins: Dict<int, Coord> │
│  └─ TotalDetectedCount: int   │
│                               │
│  InferenceOutput             │
│  └─ Results: List<ResultRow>  │
└──────────────────────────────┘


┌──────────────────────────────┐
│   VIEWMODEL BINDING            │
├──────────────────────────────┤
│  SetupViewModel              │
│  ├─ MeasurementRules (List)   │
│  ├─ AvailableClasses         │
│  └─ Commands:                │
│      ├─ LoadImageCommand     │
│      ├─ AddRuleCommand       │
│      └─ SaveConfigCommand    │
│                               │
│  InferenceViewModel          │
│  ├─ ResultImage              │
│  ├─ MeasurementText (Batch)   │
│  └─ RunInferenceCommand       │
└──────────────────────────────┘
```

## Technology Stack

```
        ┌──────────────────────────────────────┐
        │      PRESENTATION (WPF XAML)          │
        │  • MVVM Pattern                       │
        │  • Data Binding                       │
        │  • Command Pattern                    │
        └──────────────────────────────────────┘
                      ↓
        ┌──────────────────────────────────────┐
        │      MVVM TOOLKIT (Reactive)          │
        │  • ObservableObject                   │
        │  • RelayCommand                       │
        │  • AsyncRelayCommand                  │
        └──────────────────────────────────────┘
                      ↓
        ┌──────────────────────────────────────┐
        │      SERVICES LAYER                   │
        │  • YoloInferenceService               │
        │  • GeometryService                    │
        │  • ImageService                       │
        │  • ConfigService                      │
        └──────────────────────────────────────┘
                      ↓
        ┌──────────────────────────────────────┐
        │      EXTERNAL LIBRARIES               │
        │  • YoloDotNet (ONNX Runtime)          │
        │  • NetTopologySuite (Geometry)        │
        │  • SkiaSharp (Rendering)              │
        │  • System.Text.Json (Config)          │
        └──────────────────────────────────────┘
```

## File Organization

```
YoloAOIApp/
│
├─ Models/
│  └─ CoreModels.cs
│     ├─ RayConfig
│     ├─ ObjectConfig
│     ├─ YoloDetectionResult
│     ├─ InferenceOutput
│     ├─ ClassOption
│     └─ SetupState
│
├─ Services/
│  ├─ YoloInferenceService.cs ─────→ External: YoloDotNet
│  ├─ GeometryService.cs ────────→ External: NetTopologySuite
│  ├─ ImageService.cs ────────────→ External: SkiaSharp
│  └─ ConfigService.cs ───────────→ External: System.Text.Json
│
├─ ViewModels/
│  ├─ SetupViewModel.cs
│  │  ├─ Uses: YoloInferenceService
│  │  ├─ Uses: GeometryService
│  │  ├─ Uses: ImageService
│  │  └─ Uses: ConfigService
│  └─ InferenceViewModel.cs
│     ├─ Uses: YoloInferenceService
│     ├─ Uses: GeometryService
│     ├─ Uses: ImageService
│     └─ Uses: ConfigService
│
└─ Views/
   ├─ SetupView.xaml / .xaml.cs ──────→ Binds to: SetupViewModel
   ├─ InferenceView.xaml / .xaml.cs ──→ Binds to: InferenceViewModel
   ├─ MainWindow.xaml / .xaml.cs ─────→ Hosts both views in tabs
   └─ App.xaml / .xaml.cs ────────────→ Application entry point
```

---

**This architecture ensures:**
- ✅ Clean separation of concerns
- ✅ Easy to test (services are injectable)
- ✅ Easy to extend (add new services without touching UI)
- ✅ Maintainable (clear data flow)
- ✅ Reusable (services work with any UI framework)
