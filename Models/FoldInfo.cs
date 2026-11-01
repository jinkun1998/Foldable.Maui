namespace Foldable.Maui.Models;

/// <summary>
/// Snapshot of the foldable device state, posture, and hinge metrics.
/// </summary>
public record FoldInfo
{
    public static FoldInfo Default { get; } = new();

    /// <summary>True if the device possesses a physical hinge or folding display.</summary>
    public bool IsFoldable { get; init; }

    /// <summary>True if the hinge feature creates two distinct visible display areas.</summary>
    public bool IsSeparating { get; init; }

    /// <summary>Current physical posture (Closed, HalfOpened, Flat).</summary>
    public FoldPosture Posture { get; init; } = FoldPosture.Unknown;

    /// <summary>Orientation of the hinge (Vertical or Horizontal).</summary>
    public HingeOrientation Orientation { get; init; } = HingeOrientation.None;

    /// <summary>Live hinge angle in degrees (0° - 360°) when available via hardware sensor.</summary>
    public float? HingeAngle { get; init; }

    /// <summary>Bounding rectangle of the hinge / crease in device-independent pixels (DIPs).</summary>
    public Rect HingeBounds { get; init; } = Rect.Zero;

    /// <summary>
    /// True if the screen is wide enough or in split posture where master-detail or 2-column layout should engage.
    /// </summary>
    public bool ShouldSplit(double width, double height)
    {
        if (IsSeparating) return true;
        return width >= 640 && Math.Min(width, height) >= 550 && (width / height >= 0.70 || width >= 900);
    }
}
