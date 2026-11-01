using Android.Content;
using Android.Hardware;
using Android.Runtime;
using AndroidX.Core.Util;
using AndroidX.Window.Java.Layout;
using AndroidX.Window.Layout;
using Foldable.Maui.Models;
using Foldable.Maui.Services;
using Microsoft.Maui.ApplicationModel;

namespace Foldable.Maui.Platforms.Android;

public class FoldableServiceImplementation : Java.Lang.Object, IFoldableService, ISensorEventListener, IConsumer
{
    private readonly SensorManager? _sensorManager;
    private readonly Sensor? _hingeSensor;
    private WindowInfoTrackerCallbackAdapter? _trackerAdapter;
    private float? _lastAngle;
    private bool _isListening;

    public FoldInfo CurrentInfo { get; private set; } = FoldInfo.Default;

    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    public FoldableServiceImplementation()
    {
        Context? context = global::Android.App.Application.Context;
        _sensorManager = context?.GetSystemService(Context.SensorService) as SensorManager;
        // Sensor.TypeHingeAngle = 36 (Android 11+)
        _hingeSensor = _sensorManager?.GetDefaultSensor((SensorType)36);
    }

    public void StartListening()
    {
        if (_isListening) return;
        _isListening = true;

        if (_hingeSensor != null && _sensorManager != null)
        {
            _sensorManager.RegisterListener(this, _hingeSensor, SensorDelay.Normal);
        }

        var activity = Platform.CurrentActivity;
        if (activity != null)
        {
            try
            {
                _trackerAdapter = new WindowInfoTrackerCallbackAdapter(WindowInfoTracker.Companion.GetOrCreate(activity));
                var executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor();
                if (executor != null)
                {
                    _trackerAdapter.AddWindowLayoutInfoListener(activity, executor, this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowInfoTracker initialization error: {ex.Message}");
            }
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;
        _isListening = false;

        if (_hingeSensor != null && _sensorManager != null)
        {
            _sensorManager.UnregisterListener(this, _hingeSensor);
        }

        if (_trackerAdapter != null)
        {
            try
            {
                _trackerAdapter.RemoveWindowLayoutInfoListener(this);
            }
            catch
            {
            }
            _trackerAdapter = null;
        }
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e == null || e.Values == null || e.Values.Count == 0) return;

        float angle = e.Values[0];
        _lastAngle = angle;

        FoldPosture posture = angle switch
        {
            <= 10f => FoldPosture.Closed,
            >= 170f and <= 190f => FoldPosture.Flat,
            > 10f and < 170f => FoldPosture.HalfOpened,
            _ => CurrentInfo.Posture
        };

        UpdateInfo(CurrentInfo with
        {
            IsFoldable = true,
            HingeAngle = angle,
            Posture = posture
        });
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
    {
    }

    public void Accept(Java.Lang.Object? obj)
    {
        if (obj is not WindowLayoutInfo layoutInfo) return;

        bool hasHinge = false;
        bool isSeparating = false;
        FoldPosture posture = FoldPosture.Flat;
        HingeOrientation orientation = HingeOrientation.None;
        Rect bounds = Rect.Zero;

        foreach (var feature in layoutInfo.DisplayFeatures)
        {
            if (feature is IFoldingFeature fold)
            {
                hasHinge = true;
                isSeparating = fold.IsSeparating;

                if (fold.State == FoldingFeatureState.Flat)
                {
                    posture = FoldPosture.Flat;
                }
                else if (fold.State == FoldingFeatureState.HalfOpened)
                {
                    posture = FoldPosture.HalfOpened;
                }
                else
                {
                    posture = FoldPosture.Unknown;
                }

                orientation = fold.Orientation == FoldingFeatureOrientation.Vertical
                    ? HingeOrientation.Vertical
                    : HingeOrientation.Horizontal;

                var r = fold.Bounds;
                float density = Platform.CurrentActivity?.Resources?.DisplayMetrics?.Density ?? 1.0f;
                bounds = new Rect(r.Left / density, r.Top / density, r.Width() / density, r.Height() / density);
                break;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateInfo(new FoldInfo
            {
                IsFoldable = hasHinge || _hingeSensor != null,
                IsSeparating = isSeparating,
                Posture = posture,
                Orientation = orientation,
                HingeAngle = _lastAngle,
                HingeBounds = bounds
            });
        });
    }

    private void UpdateInfo(FoldInfo newInfo)
    {
        CurrentInfo = newInfo;
        FoldInfoChanged?.Invoke(this, new FoldInfoChangedEventArgs(newInfo));
    }
}
