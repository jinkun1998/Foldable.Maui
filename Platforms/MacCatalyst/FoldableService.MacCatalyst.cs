using Foldable.Maui.Models;
using Foldable.Maui.Services;

namespace Foldable.Maui.Platforms.MacCatalyst;

public class FoldableServiceImplementation : IFoldableService
{
    public FoldInfo CurrentInfo { get; } = FoldInfo.Default;
    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged
    {
        add { }
        remove { }
    }
    public void StartListening() { }
    public void StopListening() { }
}
