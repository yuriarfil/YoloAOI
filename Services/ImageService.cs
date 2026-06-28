using SkiaSharp;
using System.IO;
using System.Windows.Media.Imaging;
using NetTopologySuite.Geometries;
using YoloAOIApp.Models;

namespace YoloAOIApp.Services;

public class ImageService
{
    public SKBitmap? LoadImage(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
                return null;

            using var stream = File.OpenRead(imagePath);
            return SKBitmap.Decode(stream);
        }
        catch
        {
            return null;
        }
    }

    public BitmapImage? SkBitmapToBitmapImage(SKBitmap bitmap)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    public SKBitmap DrawSetupAnnotations(SKBitmap image, YoloDetectionResult? detection, SetupState state, int targetClassA, int targetClassB, bool useSameObject = false)
    {
        var copy = image.Copy();
        using var canvas = new SKCanvas(copy);

        if (detection?.Polygons != null)
        {
            foreach (var kvp in detection.Polygons)
            {
                var classId = kvp.Key;
                var poly = kvp.Value;
                var pointsF = poly.Coordinates.Select(c => new SKPoint((float)c.X, (float)c.Y)).ToArray();

                // Draw polygon
                using var path = new SKPath();
                path.MoveTo(pointsF[0]);
                for (int j = 1; j < pointsF.Length; j++)
                    path.LineTo(pointsF[j]);
                path.Close();

                bool isA = classId == targetClassA;
                bool isB = classId == targetClassB;

                var color = isA && (useSameObject || !isB) ? SKColors.Red
                          : isB ? SKColors.Green
                          : SKColors.Yellow;

                if (useSameObject && isA)
                    color = SKColors.Magenta;

                using var paint = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 2 };
                canvas.DrawPath(path, paint);

                // Draw center point
                var origin = detection.Origins[classId];
                using var centerPaint = new SKPaint { Color = color };
                canvas.DrawCircle((float)origin.X, (float)origin.Y, 8, centerPaint);

                // Draw class label
                using var textPaint = new SKPaint { Color = color };
                using var font = new SKFont(SKTypeface.Default, 20);
                var label = useSameObject && isA ? $"TARGET (CLS {classId})" : $"CLS {classId}";
                canvas.DrawText(label, (float)origin.X + 10, (float)origin.Y - 10, font, textPaint);
            }
        }

        // Draw user clicks
        if (state.ObjectAClick.HasValue)
        {
            var (x, y) = state.ObjectAClick.Value;
            using var paint = new SKPaint { Color = new SKColor(255, 0, 0, 255) };
            canvas.DrawCircle(x, y, 8, paint);
            using var textPaint = new SKPaint { Color = new SKColor(255, 0, 0, 255) };
            using var font = new SKFont(SKTypeface.Default, 20);
            canvas.DrawText("A_D", x + 10, y - 10, font, textPaint);
        }

        if (state.ObjectBClick.HasValue)
        {
            var (x, y) = state.ObjectBClick.Value;
            using var paint = new SKPaint { Color = new SKColor(255, 255, 0, 255) };
            canvas.DrawCircle(x, y, 8, paint);
            using var textPaint = new SKPaint { Color = new SKColor(255, 255, 0, 255) };
            using var font = new SKFont(SKTypeface.Default, 20);
            canvas.DrawText("B_D", x + 10, y - 10, font, textPaint);
        }

        return copy;
    }

    public SKBitmap DrawInferenceResult(SKBitmap image, InferenceOutput output)
    {
        var copy = image.Copy();
        using var canvas = new SKCanvas(copy);

        float textY = 40;
        using var font = new SKFont(SKTypeface.Default, 24);

        foreach (var res in output.Results)
        {
            // ── GEOMETRY DRAWING ──────────────────────────────────────────────────────
            // Skip for perpendicular component in "Both" mode (already drawn by companion IsXOffset result).
            if (!res.SuppressGeometryDrawing)
            {
                if (res.IsXOffset && res.CentroidA != null && res.CentroidB != null && res.Point1 != null && res.Point2 != null)
                {
                // ── ROTATION-AWARE OFFSET DRAWING ───────────────────────────────────────────
                //  Point1     = centroidA                         (red dot)
                //  Point2     = projPt  (foot of ⊥ on B's axis)   (yellow dot)
                //  CentroidB  = centroid of B                     (blue dot)
                //  CenterlineStart/End = B's principal axis reference

                var lineColor  = res.Status == "OK" ? SKColors.LimeGreen : SKColors.OrangeRed;

                // -- 1. B's principal-axis reference line (long dashed cyan) -----------
                if (res.CenterlineStart != null && res.CenterlineEnd != null)
                {
                    using var axPaint = new SKPaint
                    {
                        Color      = new SKColor(0, 200, 255, 140),
                        StrokeWidth = 1.5f,
                        IsStroke   = true,
                        IsAntialias = true,
                        PathEffect = SKPathEffect.CreateDash(new float[] { 14f, 6f }, 0)
                    };
                    canvas.DrawLine(
                        (float)res.CenterlineStart.X, (float)res.CenterlineStart.Y,
                        (float)res.CenterlineEnd.X,   (float)res.CenterlineEnd.Y,
                        axPaint);
                }

                // -- 2. Measurement segment: centroidB → projPt (solid, along B's axis) -
                using var measPaint = new SKPaint
                {
                    Color       = lineColor,
                    StrokeWidth = 2.5f,
                    IsStroke    = true,
                    IsAntialias = true
                };
                canvas.DrawLine(
                    (float)res.CentroidB.X, (float)res.CentroidB.Y,
                    (float)res.Point2.X,    (float)res.Point2.Y,
                    measPaint);

                // -- 3. Tick marks at both ends, perpendicular to B's axis -------------
                float measDx = (float)(res.Point2.X - res.CentroidB.X);
                float measDy = (float)(res.Point2.Y - res.CentroidB.Y);
                float measLen = MathF.Sqrt(measDx * measDx + measDy * measDy);
                using var tickPaint = new SKPaint { Color = lineColor, StrokeWidth = 2.5f, IsStroke = true, IsAntialias = true };
                if (measLen > 0.1f)
                {
                    // perpendicular unit vector to the measurement direction
                    float nx = -measDy / measLen;
                    float ny =  measDx / measLen;
                    const float tickLen = 12f;

                    // tick at centroidB
                    canvas.DrawLine(
                        (float)res.CentroidB.X + nx * tickLen, (float)res.CentroidB.Y + ny * tickLen,
                        (float)res.CentroidB.X - nx * tickLen, (float)res.CentroidB.Y - ny * tickLen,
                        tickPaint);

                    // tick at projPt
                    canvas.DrawLine(
                        (float)res.Point2.X + nx * tickLen, (float)res.Point2.Y + ny * tickLen,
                        (float)res.Point2.X - nx * tickLen, (float)res.Point2.Y - ny * tickLen,
                        tickPaint);
                }

                // -- 4. Perpendicular indicator: centroidA → projPt (dashed orange) ---
                using var perpPaint = new SKPaint
                {
                    Color       = new SKColor(255, 180, 0, 200),
                    StrokeWidth = 1.8f,
                    IsStroke    = true,
                    IsAntialias = true,
                    PathEffect  = SKPathEffect.CreateDash(new float[] { 7f, 4f }, 0)
                };
                canvas.DrawLine(
                    (float)res.Point1.X, (float)res.Point1.Y,
                    (float)res.Point2.X, (float)res.Point2.Y,
                    perpPaint);

                // -- 5. Dots: red = A, blue = B, yellow = projection point -------------
                using var dotA = new SKPaint { Color = SKColors.Red,         IsAntialias = true };
                using var dotB = new SKPaint { Color = SKColors.DeepSkyBlue, IsAntialias = true };
                using var dotP = new SKPaint { Color = SKColors.Yellow,      IsAntialias = true };
                canvas.DrawCircle((float)res.Point1.X,   (float)res.Point1.Y,   7, dotA);
                canvas.DrawCircle((float)res.CentroidB.X,(float)res.CentroidB.Y,7, dotB);
                canvas.DrawCircle((float)res.Point2.X,   (float)res.Point2.Y,   5, dotP);

                // -- 6. Label: floated 20 px above midpoint of the measurement segment -
                float lmx  = (float)((res.CentroidB.X + res.Point2.X) / 2.0);
                float lmy  = (float)((res.CentroidB.Y + res.Point2.Y) / 2.0);
                // offset label perpendicular to B's axis (upward in screen space)
                if (measLen > 0.1f)
                {
                    float nx = -measDy / measLen;
                    float ny =  measDx / measLen;
                    // ensure the label lifts "upward" (negative screen-Y direction)
                    if (ny > 0) { nx = -nx; ny = -ny; }
                    lmx += nx * 24f;
                    lmy += ny * 24f;
                }
                else
                {
                    lmy -= 20f;
                }
                using var labelPaint = new SKPaint { Color = SKColors.Yellow, IsAntialias = true };
                using var labelFont  = new SKFont(SKTypeface.Default, 17);
                canvas.DrawText($"{res.DistanceName}: {res.Message}", lmx, lmy, labelFont, labelPaint);
            }
            else if (res.Point1 != null && res.Point2 != null)
            {
                // ── GENERIC LINE DRAWING (DistanceObject / AngleObject) ────────────────────

                // Draw centerline if available
                if (res.CenterlineStart != null && res.CenterlineEnd != null)
                {
                    using var centerlinePaint = new SKPaint
                    {
                        Color = SKColors.Cyan,
                        StrokeWidth = 2,
                        IsStroke = true,
                        PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0)
                    };
                    canvas.DrawLine(
                        (float)res.CenterlineStart.X, (float)res.CenterlineStart.Y,
                        (float)res.CenterlineEnd.X, (float)res.CenterlineEnd.Y,
                        centerlinePaint
                    );
                }

                // Draw circles at hit points
                using var redPaint = new SKPaint { Color = SKColors.Red };
                canvas.DrawCircle((float)res.Point1.X, (float)res.Point1.Y, 6, redPaint);

                using var bluePaint = new SKPaint { Color = SKColors.Blue };
                canvas.DrawCircle((float)res.Point2.X, (float)res.Point2.Y, 6, bluePaint);
                if (res.IsPerpendicularOffset && res.CentroidB != null)
                    canvas.DrawCircle((float)res.CentroidB.X, (float)res.CentroidB.Y, 6, bluePaint);

                // Draw line between points (dashed orange for perpendicular offset results)
                if (res.IsPerpendicularOffset)
                {
                    using var perpPaint = new SKPaint
                    {
                        Color = new SKColor(255, 180, 0, 200),
                        StrokeWidth = 2.5f,
                        IsStroke = true,
                        IsAntialias = true,
                        PathEffect = SKPathEffect.CreateDash(new float[] { 7f, 4f }, 0)
                    };
                    canvas.DrawLine(
                        (float)res.Point1.X, (float)res.Point1.Y,
                        (float)res.Point2.X, (float)res.Point2.Y,
                        perpPaint);
                }
                else
                {
                    using var greenLine = new SKPaint { Color = SKColors.Green, StrokeWidth = 2, IsStroke = true };
                    if (res.Status == "NOK") greenLine.Color = SKColors.OrangeRed;
                    canvas.DrawLine((float)res.Point1.X, (float)res.Point1.Y, (float)res.Point2.X, (float)res.Point2.Y, greenLine);
                }

                // Draw distance label near the line midpoint
                var midX = (res.Point1.X + res.Point2.X) / 2;
                var midY = (res.Point1.Y + res.Point2.Y) / 2;
                using var labelPaint = new SKPaint { Color = SKColors.Yellow, IsAntialias = true };
                using var labelFont = new SKFont(SKTypeface.Default, 18);
                canvas.DrawText(res.DistanceName, (float)midX, (float)midY, labelFont, labelPaint);
            }
            } // end if (!res.SuppressGeometryDrawing)

            // Draw distance text summary on top left
            var statusIcon = res.Status == "OK" ? "✓" : "✗";
            var color = res.Status == "OK" ? SKColors.Green : (res.Status == "Error" ? SKColors.Gray : SKColors.Red);
            using var textPaint = new SKPaint { Color = color, IsAntialias = true };
            
            canvas.DrawText($"{statusIcon} {res.DistanceName}: {res.Message}", 20, textY, font, textPaint);
            textY += 30;
        }

        return copy;
    }

    public SKBitmap? LoadFromByteArray(byte[] imageBytes)
    {
        try
        {
            return SKBitmap.Decode(imageBytes);
        }
        catch
        {
            return null;
        }
    }

    public byte[]? SkBitmapToByteArray(SKBitmap bitmap)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
