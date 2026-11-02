# Foldable.Maui

A modern, lightweight .NET MAUI library for detecting foldable & dual-screen device postures, live hinge angles, and rendering responsive center-split layouts.

[![NuGet Version](https://img.shields.io/nuget/v/Foldable.Maui.svg)](https://www.nuget.org/packages/Foldable.Maui)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Foldable.Maui.svg)](https://www.nuget.org/packages/Foldable.Maui)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

---

## Features

- **4-Posture Hardware Detection**:
  - `FoldPosture.Closed`: Device folded shut, outer cover screen active (nominal 0°).
  - `FoldPosture.HalfOpened`: L-shaped Tabletop / Book posture (nominal 90°), ideal for controls on bottom and media on top.
  - `FoldPosture.Tent`: Inverted-V posture standing on device edges (nominal 80°), single-viewport cover display experience.
  - `FoldPosture.Flat`: Fully unfolded expansive inner display (nominal 180°).
- **Multi-Sensor Fusion Engine**:
  - Android `DeviceStateManager` callback hooks for instant OS-level state transitions.
  - AndroidX `WindowInfoTracker` for fold crease bounds and separation metrics.
  - Hardware Hinge Sensor (`SensorType.HingeAngle` / Type 36) for continuous 0°–360° hinge angles.
  - Z-axis Gravity Sensor for distinguishing tabletop resting flat vs. tent standing on edges.
- **Center-Split Responsive Engine (`FoldableLayout`)**:
  - **Master-Detail**: 50/50 exact center split across the hinge on foldables (`640 <= width < 900`). Fixed 320dp sidebar on large displays (`>= 900`). Single-column on cover screens and tent mode.
  - **Grid Lists**: Automatic 2-column center split (`Span = 2`) on unfolded screens.
  - **Responsive Forms**: Auto-adjusts form fields into 2 columns on wide screens, respecting full-width inputs.

---

## Getting Started

### 1. Initialization
In `MauiProgram.cs`:

```csharp
using Foldable.Maui.Extensions;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseFoldable(); // Wires native DeviceStateManager, WindowInfoTracker & hinge sensors

    return builder.Build();
}
```

### 2. Reading Fold Posture & Angle
Access via `FoldableService.Current`:

```csharp
using Foldable.Maui.Models;
using Foldable.Maui.Services;

var info = FoldableService.Current.CurrentInfo;
Console.WriteLine($"IsFoldable: {info.IsFoldable}, Posture: {info.Posture}, Angle: {info.HingeAngle}°");

FoldableService.Current.FoldInfoChanged += (sender, e) =>
{
    var newInfo = e.Info;
    switch (newInfo.Posture)
    {
        case FoldPosture.Flat:
            // Dual-pane or expanded viewport
            break;
        case FoldPosture.HalfOpened:
            // Tabletop mode: controls bottom, camera/media top
            break;
        case FoldPosture.Tent:
            // Tent mode: single cover-screen display
            break;
        case FoldPosture.Closed:
            // Outer cover display
            break;
    }
};
```

### 3. Symmetrical 50/50 Center-Split Layout
In any page with a sidebar/filter and a list:

```csharp
using Foldable.Maui.Layout;

protected override void OnSizeAllocated(double width, double height)
{
    base.OnSizeAllocated(width, height);
    FoldableLayout.ApplyMasterDetail(width, height, leftSidebarCol, leftSidebar, phoneSearchBar, listItemsLayout);
}
```

For card lists without a sidebar:

```csharp
FoldableLayout.ApplyCenterSplitGrid(width, listItemsLayout);
```

---

## Demo Sample App

A complete runnable demo app is located in `samples/Foldable.Maui.Sample`:
- Live telemetry badge (`Closed`, `Tabletop`, `Tent`, `Flat`).
- Real-time hinge angle display and bounding box metrics.
- Adaptive split pane view reacting automatically to fold/unfold motions.
- Manual posture simulation buttons for testing in simulators.

---

## Supported Platforms

| Platform | Postures (Closed, HalfOpened, Tent, Flat) | Hinge Angle | Split Layouts |
|---|---|---|---|
| **Android** (API 21+) | Yes (`DeviceStateManager` + `WindowInfoTracker` + Gravity) | Yes (Sensor 36) | Yes |
| **iOS** (15.0+) | Fallback (Split View / Stage Manager) | N/A | Yes |
| **Mac Catalyst** | Fallback | N/A | Yes |

---

## License

MIT © [Quoc Thao (JIN)](https://github.com/jinkun1998)
