# Rotation-Aware Object Length Measurement (Implemented & Refined)

## Status: Done
The system now uses **Polygon Area Moments** for maximum stability.

### Refinements added:
- **Area-based PCA**: Uses Green's Theorem for orientation, making it immune to point density variations.
- **Centroid Origin**: Uses the true center of mass as the ray origin for jitter-free measurements.

1. selecting an object class,
2. clicking two endpoints during setup,
3. saving the direction relative to the object,
4. reusing that direction during inference even if the object rotates.

This approach is designed so the measurement line follows the object’s rotated pose instead of staying fixed in screen coordinates.

---

## Core idea

The system does not store the click direction as a fixed image-space vector.

Instead, it:
- finds an origin point inside the object,
- estimates the object’s main axis,
- converts the click direction into the object’s local coordinate frame,
- stores that local direction,
- reconstructs the direction from the object’s current pose during inference.

This makes the line rotation-aware.

---

## Why this works

For a long thin object like a NTC , the main direction of the shape changes when the object rotates.

If the direction is stored in image coordinates only, the line can become incorrect after rotation.

If the direction is stored relative to the object’s own axis, the line rotates with the object and stays aligned with its length.

---

## Geometry building blocks

### 1. Inside point
Use a point guaranteed to lie inside the polygon.

Purpose:
- gives a stable origin for rays,
- avoids using a point outside the object.

### 2. Main axis
Use PCA on the polygon contour points to estimate the dominant direction.

Purpose:
- finds the long direction of the object,
- is usually smoother than using a rotated rectangle edge.

### 3. Local direction
Convert the user click direction from image space into object-local space.

Purpose:
- saves the meaning of the click relative to the object,
- allows the same rule to work after rotation.

### 4. Ray intersection
Cast a ray from the inside origin in the reconstructed direction.

Purpose:
- find the boundary point on the selected side,
- use that point for the final length measurement.

---

## Setup workflow

For each object rule:

1. Detect the object mask.
2. Build a polygon from the mask contour.
3. Compute an inside origin using a point-on-surface / representative-point method.
4. Compute the object main axis using PCA.
5. Ask the user to click the intended endpoint direction.
6. Convert the clicked direction into the object’s local frame.
7. Save the class id, class name, local direction, and distance limits.

---

## Inference workflow

For each saved rule:

1. Detect the object again.
2. Rebuild the polygon from the new mask.
3. Recompute the inside origin.
4. Recompute the current main axis using PCA.
5. Rotate the saved local direction back into image space.
6. Cast a ray from the origin using that reconstructed direction.
7. Find the boundary hit point.
8. Measure the distance between matched boundary points.
9. Compare the result with the min/max threshold.

---

## Why PCA was the final improvement

The rotated rectangle method can work, but it may become unstable when the mask shifts, the contour is noisy, or the object rotates significantly.

PCA improved the result because it:
- uses all contour points,
- estimates the dominant spread direction,
- is usually more stable for long thin objects,
- follows the marker’s length more consistently.


Important:
- `dir_local` is the key idea.
- The direction is not stored in fixed screen coordinates.
- It is stored relative to the object’s axis.

---

## C# / WPF rebuild notes

In a WPF C# implementation, the same logic can be reproduced with:
- polygon contour points,
- a point-inside-polygon method,
- PCA for orientation,
- vector rotation into local space,
- vector rotation back into image space,
- ray casting against the polygon boundary.

Recommended structure:
- `PolygonModel` for contour data,
- `OrientationHelper` for PCA axis estimation,
- `DirectionHelper` for rotation and normalization,
- `RayCaster` for boundary intersection,
- `RuleModel` for saved configuration.

---

## Expected behavior

If the object rotates:
- the measurement line should rotate with it,
- the line should still follow the long side of the object,
- the result should remain consistent as long as the mask stays stable.

If the object is partly hidden or the mask becomes noisy:
- the axis estimate may become unstable,
- the ray may drift,
- the measurement can become less accurate.

---

## Summary

The successful approach is:

1. use an inside point as the origin,
2. use PCA to estimate the object axis,
3. store the click direction in local object space,
4. restore that direction at inference,
5. cast the ray on the current rotated mask.

This is the version that finally made the line follow the rotated marker correctly.