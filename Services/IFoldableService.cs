using Foldable.Maui.Models;

namespace Foldable.Maui.Services;

/// <summary>
/// Service providing live foldable posture, hinge angle, and display layout events.
/// </summary>
public interface IFoldableService
{
    FoldInfo CurrentInfo { get; }

    event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    void StartListening();
    void StopListening();
}
