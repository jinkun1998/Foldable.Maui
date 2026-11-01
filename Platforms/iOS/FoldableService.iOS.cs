using Foldable.Maui.Models;
using Foldable.Maui.Services;
using Microsoft.Maui.ApplicationModel;
using UIKit;

namespace Foldable.Maui.Platforms.iOS;

public class FoldableServiceImplementation : IFoldableService
{
    private IntPtr _interaction;
    private bool _isListening;

    public FoldInfo CurrentInfo { get; private set; } = FoldInfo.Default;

    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    public void StartListening()
    {
        if (_isListening) return;
        _isListening = true;

        if (UIHingeBridge.IsSupported)
        {
            _interaction = UIHingeBridge.CreateInteraction(OnHingeUpdated);

            UIView? rootView = GetRootView();
            if (rootView != null && _interaction != IntPtr.Zero)
            {
                UIHingeBridge.Attach(rootView, _interaction);
            }
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;
        _isListening = false;

        if (_interaction != IntPtr.Zero)
        {
            UIView? rootView = GetRootView();
            if (rootView != null)
            {
                UIHingeBridge.Detach(rootView, _interaction);
            }
            _interaction = IntPtr.Zero;
        }
    }

    private static UIView? GetRootView()
    {
        UIWindow? window = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow);

        return window?.RootViewController?.View;
    }

    private void OnHingeUpdated(int status, double angleInDegrees)
    {
        FoldPosture posture = status switch
        {
            1 => FoldPosture.Closed,
            2 => FoldPosture.HalfOpened,
            3 => FoldPosture.Flat,
            _ => FoldPosture.Unknown
        };

        FoldInfo updated = new()
        {
            IsFoldable = true,
            IsSeparating = posture == FoldPosture.HalfOpened,
            Posture = posture,
            Orientation = HingeOrientation.Vertical,
            HingeAngle = (float)angleInDegrees
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentInfo = updated;
            FoldInfoChanged?.Invoke(this, new FoldInfoChangedEventArgs(updated));
        });
    }
}
