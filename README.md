# Foldable.Maui

A modern, lightweight .NET MAUI library for detecting foldable & dual-screen device postures, live hinge angles, and rendering responsive center-split layouts.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

---

## Features

- **Hardware Posture Detection**: Detect `Closed`, `HalfOpened` (tabletop / book mode), and `Flat` states via AndroidX `WindowInfoTracker`.
- **Live Hinge Angle Sensor**: Read real-time hinge angles (0°–360°) via Android hardware sensor (Type 36).
- **Hinge & Crease Metrics**: Bounding rectangle and separation state of the fold crease.
- **Center-Split Responsive Engine (`FoldableLayout`)**:
  - **Master-Detail**: 50/50 exact center split across the hinge on foldables (`640 <= width < 900`). Fixed 320dp sidebar on large displays (`>= 900`). Single-column on phones.
  - **Grid Lists**: Automatic 2-column center split (`Span = 2`) on foldables.
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
        .UseFoldable(); // Wires native WindowInfoTracker & hinge sensors

    return builder.Build();
}
```

### 2. Reading Fold Posture & Angle
Inject or access via `FoldableService.Current`:

```csharp
using Foldable.Maui.Services;

var info = FoldableService.Current.CurrentInfo;
Console.WriteLine($"IsFoldable: {info.IsFoldable}, Posture: {info.Posture}, Angle: {info.HingeAngle}°");

FoldableService.Current.FoldInfoChanged += (sender, e) =>
{
    var newInfo = e.Info;
    if (newInfo.Posture == FoldPosture.HalfOpened)
    {
        // Tabletop mode: adjust media player or camera preview
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

## Supported Platforms

| Platform | Posture / Hinge Rect | Hinge Angle | Split Layouts |
|---|---|---|---|
| **Android** (API 21+) | Yes (`WindowInfoTracker`) | Yes (Sensor 36) | Yes |
| **iOS** (15.0+) | Fallback (Split View / Stage Manager) | N/A | Yes |
| **Mac Catalyst** | Fallback | N/A | Yes |

---

## License

MIT © [Quoc Thao (JIN)](https://github.com/jinkun1998)
