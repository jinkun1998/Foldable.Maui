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
    private bool _isListening;

    public FoldInfo CurrentInfo { get; private set; } = FoldInfo.Default;

    public event EventHandler<FoldInfoChangedEventArgs>? FoldInfoChanged;

    public FoldableServiceImplementation()
    {
        Context? context = global::Android.App.Application.Context;
        _sensorManager = context?.GetSystemService(Context.SensorService) as SensorManager;
        // Sensor.TypeHingeAngle = 36 (Android 11+)
        _hingeSensor = _sensorManager?.GetDefaultSensor((SensorType)36);
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
            float angle = e.Values[0];
            _lastAngle = angle;
            UpdatePostureFromAngle(angle);
        }
        else if (e.Sensor.Type == SensorType.Gravity || e.Sensor.Type == SensorType.Accelerometer)
        {
            // Z-axis value: near 9.8 m/s² when resting flat, near 0 m/s² when upright/vertical (standing on edge in tent mode)
            _lastGravityZ = e.Values[2];
            if (_lastAngle.HasValue && _lastAngle.Value > 10f && _lastAngle.Value < 170f)
            {
                UpdatePostureFromAngle(_lastAngle.Value);
            }
        }
    }

    private void UpdatePostureFromAngle(float angle)
    {
        // ponytail: Tent mode detected when angle is 10°-170° and Z-axis gravity is near zero (|Z| < 3 m/s², standing on edges); upgrade to pitch/roll orientation matrix if inverted tent differentiation needed
        FoldPosture posture = angle switch
        {
            <= 10f => FoldPosture.Closed,
            >= 170f and <= 190f => FoldPosture.Flat,
            > 10f and < 170f => Math.Abs(_lastGravityZ) < 3.0f ? FoldPosture.Tent : FoldPosture.HalfOpened,
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
                    posture = Math.Abs(_lastGravityZ) < 3.0f ? FoldPosture.Tent : FoldPosture.HalfOpened;
                }
                else
                {
                    posture = FoldPosture.Unknown;
                }

                if (posture == FoldPosture.Tent)
                {
                    isSeparating = false;
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
