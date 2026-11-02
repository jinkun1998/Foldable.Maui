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
    private readonly Sensor? _gravitySensor;
    private WindowInfoTrackerCallbackAdapter? _trackerAdapter;
    private float? _lastAngle;
    private float _lastGravityZ = 9.8f;
    private bool _hasHingeFeature;
    private FoldingFeatureState? _lastFoldState;
    private HingeOrientation _lastOrientation = HingeOrientation.None;
    private Rect _lastBounds = Rect.Zero;
    private bool _isListening;

    public FoldInfo CurrentInfo { get; private set; } = FoldInfo.Default;

    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    public FoldableServiceImplementation()
    {
        Context? context = global::Android.App.Application.Context;
        _sensorManager = context?.GetSystemService(Context.SensorService) as SensorManager;

        // Sensor.TypeHingeAngle = 36 (Android 11+)
        _hingeSensor = _sensorManager?.GetDefaultSensor((SensorType)36)
            ?? _sensorManager?.GetSensorList((SensorType)36).FirstOrDefault();

        _gravitySensor = _sensorManager?.GetDefaultSensor(SensorType.Gravity)
            ?? _sensorManager?.GetDefaultSensor(SensorType.Accelerometer);
    }

    public void StartListening()
    {
        if (_isListening) return;
        _isListening = true;

        if (_sensorManager != null)
        {
            if (_hingeSensor != null)
                _sensorManager.RegisterListener(this, _hingeSensor, SensorDelay.Normal);
            if (_gravitySensor != null)
                _sensorManager.RegisterListener(this, _gravitySensor, SensorDelay.Normal);
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

        if (_sensorManager != null)
        {
            if (_hingeSensor != null)
                _sensorManager.UnregisterListener(this, _hingeSensor);
            if (_gravitySensor != null)
                _sensorManager.UnregisterListener(this, _gravitySensor);
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
        if (e == null || e.Values == null || e.Values.Count == 0 || e.Sensor == null) return;

        if (e.Sensor.Type == (SensorType)36)
        {
            _lastAngle = e.Values[0];
            RecomputeState();
        }
        else if (e.Sensor.Type == SensorType.Gravity || e.Sensor.Type == SensorType.Accelerometer)
        {
            _lastGravityZ = e.Values[2];
            if (_lastFoldState == FoldingFeatureState.HalfOpened || (_lastAngle > 15f && _lastAngle < 165f))
            {
                RecomputeState();
            }
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) { }

    public void Accept(Java.Lang.Object? obj)
    {
        if (obj is not WindowLayoutInfo layoutInfo) return;

        bool hasHinge = false;
        FoldingFeatureState? foldState = null;
        HingeOrientation orientation = HingeOrientation.None;
        Rect bounds = Rect.Zero;

        foreach (var feature in layoutInfo.DisplayFeatures)
        {
            if (feature is IFoldingFeature fold)
            {
                hasHinge = true;
                foldState = fold.State;
                orientation = fold.Orientation == FoldingFeatureOrientation.Vertical
                    ? HingeOrientation.Vertical
                    : HingeOrientation.Horizontal;

                var r = fold.Bounds;
                float density = Platform.CurrentActivity?.Resources?.DisplayMetrics?.Density ?? 1.0f;
                bounds = new Rect(r.Left / density, r.Top / density, r.Width() / density, r.Height() / density);
                break;
            }
        }

        _hasHingeFeature = hasHinge;
        _lastFoldState = foldState;
        _lastOrientation = orientation;
        _lastBounds = bounds;

        RecomputeState();
    }

    private void RecomputeState()
    {
        bool isFoldable = _hingeSensor != null || _hasHingeFeature;
        if (!isFoldable)
        {
            DispatchUpdate(new FoldInfo
            {
                IsFoldable = false,
                Posture = FoldPosture.Flat,
                Orientation = HingeOrientation.None,
                HingeAngle = 180f,
                IsSeparating = false
            });
            return;
        }

        FoldPosture posture;
        float angle;

        if (_hasHingeFeature)
        {
            // Inner unfolded screen is active
            if (_lastFoldState == FoldingFeatureState.Flat)
            {
                posture = FoldPosture.Flat;
                angle = (_lastAngle.HasValue && _lastAngle.Value >= 165f) ? _lastAngle.Value : 180f;
            }
            else
            {
                // Half-opened: distinguish Tent vs Tabletop using Z-gravity
                bool isTent = Math.Abs(_lastGravityZ) < 4.0f;
                posture = isTent ? FoldPosture.Tent : FoldPosture.HalfOpened;
                angle = (_lastAngle.HasValue && _lastAngle.Value > 15f && _lastAngle.Value < 165f) ? _lastAngle.Value : 90f;
            }
        }
        else
        {
            // App window does not cross a hinge: on a foldable, this is the cover display (closed)
            if (_lastAngle.HasValue)
            {
                if (_lastAngle.Value <= 15f)
                {
                    posture = FoldPosture.Closed;
                    angle = _lastAngle.Value;
                }
                else if (_lastAngle.Value >= 165f)
                {
                    posture = FoldPosture.Flat;
                    angle = _lastAngle.Value;
                }
                else
                {
                    bool isTent = Math.Abs(_lastGravityZ) < 4.0f;
                    posture = isTent ? FoldPosture.Tent : FoldPosture.HalfOpened;
                    angle = _lastAngle.Value;
                }
            }
            else
            {
                posture = FoldPosture.Closed;
                angle = 0f;
            }
        }

        bool isSeparating = posture == FoldPosture.HalfOpened;

        DispatchUpdate(new FoldInfo
        {
            IsFoldable = true,
            IsSeparating = isSeparating,
            Posture = posture,
            Orientation = _lastOrientation,
            HingeAngle = angle,
            HingeBounds = _lastBounds
        });
    }

    private void DispatchUpdate(FoldInfo newInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentInfo = newInfo;
            FoldInfoChanged?.Invoke(this, new FoldInfoChangedEventArgs(newInfo));
        });
    }
}
