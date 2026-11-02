using Foldable.Maui.Models;
using Foldable.Maui.Services;

namespace Foldable.Maui.Sample;

public partial class MainPage : ContentPage
{
    private readonly IFoldableService _foldableService;
    private FoldInfo _currentInfo = FoldInfo.Default;
    private bool _isManualSimulation;

    public MainPage()
    {
        InitializeComponent();
        _foldableService = FoldableService.Current;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _foldableService.FoldInfoChanged += OnFoldInfoChanged;
        UpdateUI(_foldableService.CurrentInfo);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _foldableService.FoldInfoChanged -= OnFoldInfoChanged;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateLayout(width, height);
    }

    private void OnFoldInfoChanged(object? sender, FoldInfoChangedEventArgs e)
    {
        if (_isManualSimulation) return;
        MainThread.BeginInvokeOnMainThread(() => UpdateUI(e.Info));
    }

    private void UpdateUI(FoldInfo info)
    {
        _currentInfo = info;

        // Posture badge & color
        (string postureText, Color badgeColor) = info.Posture switch
        {
            FoldPosture.Flat => ("Flat / Unfolded", Color.FromArgb("#059669")),
            FoldPosture.HalfOpened => ("Tabletop", Color.FromArgb("#D97706")),
            FoldPosture.Tent => ("Tent Mode", Color.FromArgb("#7C3AED")),
            FoldPosture.Closed => ("Closed", Color.FromArgb("#475569")),
            _ => ("Unknown", Color.FromArgb("#3B82F6"))
        };

        PostureLabel.Text = _isManualSimulation ? $"{postureText} (Simulated)" : postureText;
        PostureBadge.Background = new SolidColorBrush(badgeColor);

        // Hinge angle
        AngleLabel.Text = info.HingeAngle.HasValue ? $"{info.HingeAngle.Value:F0}°" : "--°";

        // Metrics
        IsFoldableLabel.Text = info.IsFoldable ? "Yes" : "No";
        IsSeparatingLabel.Text = info.IsSeparating ? "Yes" : "No";
        OrientationLabel.Text = info.Orientation.ToString();
        BoundsLabel.Text = $"X:{info.HingeBounds.X:F0} Y:{info.HingeBounds.Y:F0} W:{info.HingeBounds.Width:F0} H:{info.HingeBounds.Height:F0}";

        bool shouldSplit = info.ShouldSplit(Width, Height);
        ShouldSplitLabel.Text = shouldSplit ? "Yes" : "No";

        // Adaptive description
        PostureHelpText.Text = info.Posture switch
        {
            FoldPosture.Tent => "Tent mode: Inverted-V posture. Single cover-screen active.",
            FoldPosture.HalfOpened => "Tabletop mode: Lower half flat on surface. Controls bottom, content top.",
            FoldPosture.Flat => "Flat mode: Fully unfolded display active.",
            FoldPosture.Closed => "Closed mode: Outer screen active.",
            _ => "Move the foldable device to detect live posture changes."
        };

        UpdateLayout(Width, Height);
    }

    private void UpdateLayout(double width, double height)
    {
        if (width <= 0 || height <= 0) return;

        bool shouldSplit = _currentInfo.ShouldSplit(width, height);

        if (shouldSplit)
        {
            RootGrid.ColumnDefinitions.Clear();
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            RootGrid.RowDefinitions.Clear();
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            Grid.SetColumn(TelemetryCard, 0);
            Grid.SetRow(TelemetryCard, 0);

            Grid.SetColumn(ContentCard, 1);
            Grid.SetRow(ContentCard, 0);

            ContentCard.IsVisible = true;
            AdaptiveModeTitle.Text = "Dual-Pane View (Split)";
        }
        else
        {
            RootGrid.ColumnDefinitions.Clear();
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            RootGrid.RowDefinitions.Clear();
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(TelemetryCard, 0);
            Grid.SetRow(TelemetryCard, 0);

            Grid.SetColumn(ContentCard, 0);
            Grid.SetRow(ContentCard, 1);

            ContentCard.IsVisible = _currentInfo.Posture != FoldPosture.Tent;
            AdaptiveModeTitle.Text = _currentInfo.Posture == FoldPosture.Tent ? "Tent Mode (Cover View)" : "Single-Pane View";
        }
    }

    private void OnSimulateFlatClicked(object? sender, EventArgs e)
    {
        _isManualSimulation = true;
        UpdateUI(new FoldInfo
        {
            IsFoldable = true,
            Posture = FoldPosture.Flat,
            Orientation = HingeOrientation.Vertical,
            HingeAngle = 180f,
            IsSeparating = false
        });
    }

    private void OnSimulateTabletopClicked(object? sender, EventArgs e)
    {
        _isManualSimulation = true;
        UpdateUI(new FoldInfo
        {
            IsFoldable = true,
            Posture = FoldPosture.HalfOpened,
            Orientation = HingeOrientation.Horizontal,
            HingeAngle = 100f,
            IsSeparating = true
        });
    }

    private void OnSimulateTentClicked(object? sender, EventArgs e)
    {
        _isManualSimulation = true;
        UpdateUI(new FoldInfo
        {
            IsFoldable = true,
            Posture = FoldPosture.Tent,
            Orientation = HingeOrientation.Vertical,
            HingeAngle = 80f,
            IsSeparating = false
        });
    }

    private void OnSimulateClosedClicked(object? sender, EventArgs e)
    {
        _isManualSimulation = true;
        UpdateUI(new FoldInfo
        {
            IsFoldable = true,
            Posture = FoldPosture.Closed,
            Orientation = HingeOrientation.None,
            HingeAngle = 0f,
            IsSeparating = false
        });
    }

    private void OnLiveModeClicked(object? sender, EventArgs e)
    {
        _isManualSimulation = false;
        UpdateUI(_foldableService.CurrentInfo);
    }
}
