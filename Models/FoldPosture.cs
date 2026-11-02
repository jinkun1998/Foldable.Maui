namespace Foldable.Maui.Models;

/// <summary>
/// Represents the physical posture of a foldable or dual-screen device.
/// </summary>
public enum FoldPosture
{
    /// <summary>Device is not foldable or posture could not be determined.</summary>
    Unknown = 0,

    /// <summary>The foldable device is fully closed.</summary>
    Closed = 1,

    /// <summary>The foldable device is partially opened in tabletop posture (typically 90° - 150°, resting on base).</summary>
    HalfOpened = 2,

    /// <summary>The foldable device is completely flat / unfolded (typically ~180°).</summary>
    Flat = 3,

    /// <summary>The foldable device is partially opened in tent posture (standing on edges in an inverted-V shape).</summary>
    Tent = 4
}
