using Foldable.Maui.Models;
using Foldable.Maui.Services;

namespace Foldable.Maui.Platforms.iOS;

public class FoldableServiceImplementation : IFoldableService
{
    public FoldInfo CurrentInfo { get; private set; } = FoldInfo.Default;

    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    public void StartListening()
    {
    }

    public void StopListening()
    {
    }
}
