namespace Foldable.Maui.Models;

/// <summary>
/// Orientation of the physical hinge or crease across the display.
/// </summary>
public enum HingeOrientation
{
    None = 0,

    /// <summary>Hinge runs vertically (left / right screen split, e.g. Galaxy Z Fold, Pixel Fold).</summary>
    Vertical = 1,

    /// <summary>Hinge runs horizontally (top / bottom screen split, e.g. Galaxy Z Flip, Razr).</summary>
    Horizontal = 2
}
