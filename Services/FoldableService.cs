using Foldable.Maui.Models;

namespace Foldable.Maui.Services;

public static class FoldableService
{
    private static IFoldableService? _current;

    public static IFoldableService Current => _current ??= CreateService();

    private static IFoldableService CreateService()
    {
#if ANDROID
        return new Platforms.Android.FoldableServiceImplementation();
#elif IOS
        return new Platforms.iOS.FoldableServiceImplementation();
#elif MACCATALYST
        return new Platforms.MacCatalyst.FoldableServiceImplementation();
#else
        return new DefaultFoldableService();
#endif
    }

    private class DefaultFoldableService : IFoldableService
    {
        public FoldInfo CurrentInfo => FoldInfo.Default;
        public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged
        {
            add { }
            remove { }
        }
        public void StartListening() { }
        public void StopListening() { }
    }
}
