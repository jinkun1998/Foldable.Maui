using Android.Content;
using Android.Content.Res;
using Android.Hardware;
using Android.Runtime;
using AndroidX.Core.Util;
using AndroidX.Window.Java.Layout;
using AndroidX.Window.Layout;
using Foldable.Maui.Models;
using Foldable.Maui.Services;
using Microsoft.Maui.ApplicationModel;

namespace Foldable.Maui.Platforms.Android;

public class FoldableServiceImplementation : Java.Lang.Object, IFoldableService, ISensorEventListener, IConsumer, IComponentCallbacks
{
    private readonly SensorManager? _sensorManager;
    private readonly Sensor? _hingeSensor;
    private readonly Sensor? _gravitySensor;
    private WindowInfoTrackerCallbackAdapter? _trackerAdapter;
    private float? _lastAngle;
    private float _lastGravityZ = 9.8f;
    private int? _lastDeviceState;
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
            ?? _sensorManager?.GetSensorList((SensorType)36)?.FirstOrDefault()
            ?? _sensorManager?.GetSensorList(SensorType.All)?.FirstOrDefault(s => (int)s.Type == 36 || s.StringType == "android.sensor.hinge_angle");

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

        try
        {
            global::Android.App.Application.Context.RegisterComponentCallbacks(this);
        }
        catch
        {
        }

        RegisterDeviceStateManager();

        var activity = Platform.CurrentActivity;
        if (activity != null)
        {
            try
            {
                _trackerAdapter = new WindowInfoTrackerCallbackAdapter(WindowInfoTracker.Companion.GetOrCreate(activity));
                var executor = AndroidX.Core.Content.ContextCompat.GetMainExecutor(activity);
                if (executor != null)
                {
                    _trackerAdapter.AddWindowLayoutInfoListener(activity, executor, this);
                }
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("FoldableMaui", $"WindowInfoTracker error: {ex.Message}");
            }
        }

        RecomputeState();
    }

    private void RegisterDeviceStateManager()
    {
        try
        {
            var dsm = global::Android.App.Application.Context.GetSystemService("device_state");
            if (dsm == null) return;

            var dsmClass = dsm.Class;
            var methods = dsmClass.GetMethods();
            var registerMethod = methods.FirstOrDefault(m => m.Name == "registerCallback" || m.Name == "registerDeviceStateCallback");
            if (registerMethod == null) return;

            var pParams = registerMethod.GetParameterTypes();
            var callbackType = pParams.Last();

            var handler = new DeviceStateInvocationHandler(state =>
            {
                global::Android.Util.Log.Info("FoldableMaui", $"DeviceStateManager live state: {state}");
                _lastDeviceState = state;
                RecomputeState();
            });

            var proxy = Java.Lang.Reflect.Proxy.NewProxyInstance(
                callbackType.ClassLoader,
                new Java.Lang.Class[] { callbackType },
                handler);

            var executor = new DirectExecutor();
            if (pParams.Length == 2)
            {
                registerMethod.Invoke(dsm, new Java.Lang.Object[] { executor, (Java.Lang.Object)proxy });
            }
            else
            {
                registerMethod.Invoke(dsm, new Java.Lang.Object[] { (Java.Lang.Object)proxy });
            }
            global::Android.Util.Log.Info("FoldableMaui", "Successfully hooked DeviceStateManager callback.");
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("FoldableMaui", $"DeviceStateManager registration: {ex.Message}");
        }
    }

    private class DirectExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        public void Execute(Java.Lang.IRunnable? command)
        {
            command?.Run();
        }
    }

    private class DeviceStateInvocationHandler : Java.Lang.Object, Java.Lang.Reflect.IInvocationHandler
    {
        private readonly Action<int> _onState;

        public DeviceStateInvocationHandler(Action<int> onState)
        {
            _onState = onState;
        }

        public Java.Lang.Object? Invoke(Java.Lang.Object? proxy, Java.Lang.Reflect.Method? method, Java.Lang.Object[]? args)
        {
            string name = method?.Name ?? "";
            if (name == "onDeviceStateChanged" || name == "onStateChanged")
            {
                if (args != null && args.Length > 0 && args[0] != null)
                {
                    try
                    {
                        var arg = args[0];
                        if (arg is Java.Lang.Integer intVal)
                        {
                            _onState(intVal.IntValue());
                        }
                        else
                        {
                            var getIdentifier = arg.Class.GetMethod("getIdentifier");
                            var idObj = getIdentifier?.Invoke(arg, null);
                            if (idObj is Java.Lang.Integer idInt)
                            {
                                _onState(idInt.IntValue());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        global::Android.Util.Log.Warn("FoldableMaui", $"Proxy extract: {ex.Message}");
                    }
                }
            }
            else if (name == "hashCode")
            {
                return Java.Lang.Integer.ValueOf(GetHashCode());
            }
            else if (name == "equals" && args != null && args.Length == 1)
            {
                return Java.Lang.Boolean.ValueOf(proxy == args[0]);
            }
            else if (name == "toString")
            {
                return new Java.Lang.String("DeviceStateCallbackProxy");
            }
            return null;
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

        try
        {
            global::Android.App.Application.Context.UnregisterComponentCallbacks(this);
        }
        catch
        {
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

        int sensorType = (int)e.Sensor.Type;
        if (sensorType == 36 || e.Sensor.StringType == "android.sensor.hinge_angle")
        {
            _lastAngle = e.Values[0];
            RecomputeState();
        }
        else if (e.Sensor.Type == SensorType.Gravity || e.Sensor.Type == SensorType.Accelerometer)
        {
            float z = e.Values[2];
            if (Math.Abs(z - _lastGravityZ) > 1.5f)
            {
                _lastGravityZ = z;
                RecomputeState();
            }
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) { }

    public void OnConfigurationChanged(Configuration newConfig)
    {
        RecomputeState();
    }

    public void OnLowMemory() { }

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

    private static bool IsInnerScreen()
    {
        var context = Platform.CurrentActivity ?? (Context?)global::Android.App.Application.Context;
        var config = context?.Resources?.Configuration;
        if (config != null && config.SmallestScreenWidthDp >= 600)
        {
            return true;
        }

        var metrics = context?.Resources?.DisplayMetrics;
        if (metrics != null)
        {
            float minDp = Math.Min(metrics.WidthPixels, metrics.HeightPixels) / metrics.Density;
            if (minDp >= 550f)
            {
                return true;
            }
        }

        return false;
    }

    private void RecomputeState()
    {
        bool isFoldable = _hingeSensor != null || _hasHingeFeature || _lastDeviceState.HasValue;
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

        var context = Platform.CurrentActivity ?? (Context?)global::Android.App.Application.Context;
        var config = context?.Resources?.Configuration;
        bool isLandscape = config?.Orientation == global::Android.Content.Res.Orientation.Landscape;
        HingeOrientation defaultOrientation = isLandscape ? HingeOrientation.Horizontal : HingeOrientation.Vertical;
        HingeOrientation orientation = _lastOrientation != HingeOrientation.None ? _lastOrientation : defaultOrientation;

        bool isInner = IsInnerScreen();
        FoldPosture posture;
        float angle;

        if (_lastDeviceState.HasValue)
        {
            // Android DeviceStateManager: 0=CLOSED, 1=TENT, 2=HALF_OPENED, 3=OPENED
            switch (_lastDeviceState.Value)
            {
                case 0:
                    posture = FoldPosture.Closed;
                    angle = (_lastAngle.HasValue && _lastAngle.Value <= 15f) ? _lastAngle.Value : 0f;
                    break;
                case 1:
                    posture = FoldPosture.Tent;
                    angle = (_lastAngle.HasValue && _lastAngle.Value > 15f && _lastAngle.Value < 165f) ? _lastAngle.Value : 80f;
                    break;
                case 2:
                    bool isTent = Math.Abs(_lastGravityZ) < 3.0f && !isInner;
                    posture = isTent ? FoldPosture.Tent : FoldPosture.HalfOpened;
                    angle = (_lastAngle.HasValue && _lastAngle.Value > 15f && _lastAngle.Value < 165f)
                        ? _lastAngle.Value
                        : (isTent ? 80f : 90f);
                    break;
                case 3:
                default:
                    posture = FoldPosture.Flat;
                    angle = (_lastAngle.HasValue && _lastAngle.Value >= 165f) ? _lastAngle.Value : 180f;
                    break;
            }
        }
        else if (_hasHingeFeature)
        {
            if (_lastFoldState == FoldingFeatureState.HalfOpened)
            {
                bool isTent = !isInner && Math.Abs(_lastGravityZ) < 4.0f;
                posture = isTent ? FoldPosture.Tent : FoldPosture.HalfOpened;
                angle = (_lastAngle.HasValue && _lastAngle.Value > 15f && _lastAngle.Value < 165f)
                    ? _lastAngle.Value
                    : (isTent ? 80f : 90f);
            }
            else
            {
                posture = FoldPosture.Flat;
                angle = (_lastAngle.HasValue && _lastAngle.Value >= 165f) ? _lastAngle.Value : 180f;
            }
        }
        else
        {
            if (isInner)
            {
                posture = FoldPosture.Flat;
                angle = (_lastAngle.HasValue && _lastAngle.Value >= 165f) ? _lastAngle.Value : 180f;
            }
            else
            {
                bool isTent = Math.Abs(_lastGravityZ) < 4.0f;
                posture = isTent ? FoldPosture.Tent : FoldPosture.Closed;
                angle = isTent
                    ? ((_lastAngle.HasValue && _lastAngle.Value > 15f && _lastAngle.Value < 165f) ? _lastAngle.Value : 80f)
                    : ((_lastAngle.HasValue && _lastAngle.Value <= 15f) ? _lastAngle.Value : 0f);
            }
        }

        bool isSeparating = posture == FoldPosture.HalfOpened;

        Rect bounds = _lastBounds;
        if (bounds == Rect.Zero && isInner)
        {
            var metrics = context?.Resources?.DisplayMetrics;
            float density = metrics?.Density ?? 2.625f;
            float widthDp = (metrics?.WidthPixels ?? 1812) / density;
            float heightDp = (metrics?.HeightPixels ?? 2176) / density;
            bounds = isLandscape
                ? new Rect(0, heightDp / 2, widthDp, 0)
                : new Rect(widthDp / 2, 0, 0, heightDp);
        }

        DispatchUpdate(new FoldInfo
        {
            IsFoldable = true,
            IsSeparating = isSeparating,
            Posture = posture,
            Orientation = orientation,
            HingeAngle = angle,
            HingeBounds = bounds
        });
    }

    private void DispatchUpdate(FoldInfo newInfo)
    {
        if (newInfo == CurrentInfo) return;
        CurrentInfo = newInfo;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FoldInfoChanged?.Invoke(this, new FoldInfoChangedEventArgs(newInfo));
        });
    }
}
