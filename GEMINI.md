# YOLO AOI Project Instructions

## Architecture Overview
- **Framework**: .NET 8 WPF with MVVM Toolkit.
- **Inference**: YoloDotNet 4.2.0 (ONNX Runtime).
- **Geometry**: NetTopologySuite 2.5.0.
- **Imaging**: SkiaSharp.

## Critical Technical Patterns

### 1. YOLO Inference (YoloDotNet 4.2.0)
- **Initialization**: Use `CpuExecutionProvider` (or `CudaExecutionProvider` if GPU available).
- **Inference**: Use `RunSegmentation(image, confidence)`.
- **Detection Pattern**: `DetectAsync` captures the largest detection (highest contour point count) for *every* class and stores them in `Polygons` and `Origins` dictionaries indexed by class ID.
- **Multi-Rule Configuration**: `RayConfig` contains a `Rules` list. Each `MeasurementRule` defines a unique measurement (e.g. `distance1`) with its own class pair, ray directions, user-defined `MinDist`/`MaxDist` thresholds, and a `CalculationMethod` (such as `DistanceObject`, `OffsetObject`, or `AngleObject`).

### 2. Geometry & Polygon Formation
- **NetTopologySuite Compatibility**: `MaskToPolygon` must ensure the coordinate ring is closed (first point == last point).
- **Inside Point**: Use **Centroid** (calculated via Area Moments) as the primary origin point for stability. Fallback to `polygon.InteriorPoint` if the centroid is outside the polygon.
- **Ray-Casting**: Use `ray.Intersection(polygon.Boundary)` to find edge hit points. Iterate through all rules in inference.
- **Rotation Awareness**: Objects compute their "Main Axis" via **Polygon Area Moments** (using Green's Theorem). This method is independent of point density and provides a more stable orientation than point-based PCA. Directions are stored as `LocalDir` (relative to this axis) to ensure measurement rays follow the object's orientation if it rotates.
- **OffsetObject Method (rotation-aware)**: Does NOT use a fixed screen X-axis. Instead, it projects `(centroidA − centroidB)` onto **Object B's principal axis unit vector** `B̂ = (cos θ_B, sin θ_B)`. This means the measurement follows B's own orientation and is identical to `centroidA.X − centroidB.X` only when B is perfectly horizontal. Formula: `offset = dot(centroidA − centroidB, B̂)` (signed). The projection foot P on B's axis is `P = centroidB + offset × B̂`, and the lateral gap from A to P (the perpendicular component) is rendered as a dashed orange indicator line.

### 3. Coordinate Mapping
- **UI to Pixel**: When capturing clicks on an `Image` control with `Stretch="Uniform"`, always map to original pixels:
  `pixelX = (uiX / ActualWidth) * Source.Width`

## Asset Management
- Assets are stored in the `Assets/` root folder.
- The `.csproj` is configured to copy `Assets/` to the output directory on build.
- Always resolve paths relative to `AppDomain.CurrentDomain.BaseDirectory`.

## Calculation Methods Reference

### `DistanceObject` (ray-cast gap)
- Casts a ray from `originA` in `dirA` (rotated by A's main axis angle if `LocalDir` is set) to hit A's boundary → `p1`.
- Casts a ray from `originB` in `dirB` similarly → `p2`.
- **Result**: `Distance(p1, p2)` (always positive).
- **Overlay**: solid green/red line from `p1` to `p2`, circles at hit points.

### `OffsetObject` (rotation-aware along-axis projection)
- **NEVER** subtract raw screen X coordinates. Always use the projection method.
- Computes B's principal-axis unit vector: `B̂ = (cos θ_B, sin θ_B)` via `GetMainAxisAngle(polyB)`.
- **Signed offset** = `dot(centroidA − centroidB, B̂)` → positive means A is ahead along B's axis, negative means A is behind.
- **Threshold check** uses `Math.Abs(offset)` vs `MinDist`/`MaxDist`.
- **Projection foot** `P = centroidB + offset × B̂` (where the perpendicular from A meets B's axis).
- **`MeasurementResult` fields used**:
  - `IsXOffset = true`
  - `CentroidA` = centroid of A (red dot)
  - `CentroidB` = centroid of B (blue dot)
  - `Point1` = centroidA
  - `Point2` = projPt P (yellow dot)
  - `CenterlineStart/End` = B's full axis reference line (drawn dashed cyan)
- **Overlay legend**:
  - Dashed cyan line  = B's principal axis (reference)
  - Solid green/red   = measurement segment centroidB → P (along B's axis)
  - Tick marks        = dimension-line style, perpendicular to B's axis
  - Dashed orange     = perpendicular gap from centroidA → P
  - 🔴 Red dot       = centroidA
  - 🔵 Blue dot      = centroidB
  - 🟡 Yellow dot    = projection foot P
  - Yellow label      = `{DistanceName}: {signed_value}px` (e.g. `+12.3px`)
- **Key methods**: `GeometryService.MeasureXOffset()`, `GeometryService.GetXOffsetLinePoints()`.

### `AngleObject` (principal-axis angle difference)
- Computes the angle between A's and B's principal axes.
- Normalized to `[0°, 90°]` because area-moment axes have 180° symmetry.
- **Result**: angle in degrees.
- **Overlay**: line from `originA` to `originB`.
