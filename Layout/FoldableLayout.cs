using Foldable.Maui.Models;
using Foldable.Maui.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace Foldable.Maui.Layout;

/// <summary>
/// Layout engine providing responsive 50/50 center-split and posture-adaptive layouts for foldable and wide screens.
/// </summary>
public static class FoldableLayout
{
    public static readonly BindableProperty IsFullWidthProperty =
        BindableProperty.CreateAttached(
            "IsFullWidth",
            typeof(bool),
            typeof(FoldableLayout),
            false);

    public static bool GetIsFullWidth(BindableObject view) => (bool)view.GetValue(IsFullWidthProperty);
    public static void SetIsFullWidth(BindableObject view, bool value) => view.SetValue(IsFullWidthProperty, value);

    private static readonly BindableProperty LayoutColumnsProperty =
        BindableProperty.CreateAttached(
            "LayoutColumns",
            typeof(int),
            typeof(FoldableLayout),
            -1);

    /// <summary>
    /// Master-Detail: 50/50 center-split on portrait foldables (1812x2176); compact 280dp sidebar + 2-col list on landscape (2176x1812); single col on phone.
    /// </summary>
    public static void ApplyMasterDetail(
        double width,
        double height,
        ColumnDefinition? leftCol,
        View? leftSidebar,
        View? phoneSearchBar,
        GridItemsLayout? listLayout = null)
    {
        if (width <= 0 || height <= 0) return;

        double shortestSide = Math.Min(width, height);
        bool isWideOrFoldable = width >= 640 && shortestSide >= 550 && (width / height >= 0.70 || width >= 900);
        bool isLandscape = isWideOrFoldable && width > height && width >= 750;

        if (leftSidebar != null && leftCol != null)
        {
            leftSidebar.IsVisible = isWideOrFoldable;

            if (!isWideOrFoldable)
            {
                leftSidebar.WidthRequest = 0;
                leftCol.Width = new GridLength(0);
            }
            else if (isLandscape || width >= 900)
            {
                // Landscape foldable (2176x1812) or desktop: compact 280dp sidebar
                double sidebarWidth = width >= 1000 ? 320 : 280;
                leftSidebar.WidthRequest = sidebarWidth;
                leftCol.Width = new GridLength(sidebarWidth);
            }
            else
            {
                // Portrait foldable (1812x2176): exact 50/50 center split down vertical crease
                leftSidebar.WidthRequest = -1;
                leftCol.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        if (phoneSearchBar != null)
        {
            phoneSearchBar.IsVisible = !isWideOrFoldable;
        }

        if (listLayout != null)
        {
            int targetSpan = isLandscape ? 2 : 1;
            if (listLayout.Span != targetSpan)
            {
                listLayout.Span = targetSpan;
            }
        }
    }

    /// <summary>
    /// Standalone lists: 1 col on normal phone (shortest dimension &lt; 550dp), 2 cols on portrait foldable (1812x2176), 3+ cols on landscape (2176x1812).
    /// </summary>
    public static void ApplyCenterSplitGrid(double width, double height, GridItemsLayout? listLayout, int breakpoint = 640)
    {
        if (listLayout == null) return;

        double shortestSide;
        if (width > 0 && height > 0)
        {
            shortestSide = Math.Min(width, height);
        }
        else
        {
            var main = DeviceDisplay.Current.MainDisplayInfo;
            double density = main.Density > 0 ? main.Density : 1.0;
            shortestSide = Math.Min(main.Width, main.Height) / density;
            if (width <= 0) width = main.Width / density;
            if (height <= 0) height = main.Height / density;
        }

        // Normal phone (any phone with shortest dimension < 550dp): ALWAYS 1 column
        if (shortestSide < 550)
        {
            if (listLayout.Span != 1)
            {
                listLayout.Span = 1;
            }
            return;
        }

        // Foldables unfolded & tablets (shortestSide >= 550dp):
        bool isLandscape = width > height && width >= 750;
        int newSpan = isLandscape ? Math.Max(3, (int)(width / 260)) : (width >= breakpoint ? 2 : 1);

        if (listLayout.Span != newSpan)
        {
            listLayout.Span = newSpan;
        }
    }

    public static void ApplyCenterSplitGrid(double width, GridItemsLayout? listLayout, int breakpoint = 640)
        => ApplyCenterSplitGrid(width, 0, listLayout, breakpoint);

    /// <summary>
    /// Adapts a form grid into 2 columns on wide/foldable screens, respecting IsFullWidth.
    /// </summary>
    public static void ApplyResponsiveForm(Grid? grid, double width, int breakpoint = 560)
    {
        if (grid == null || width <= 0) return;

        int targetColumns = width >= breakpoint ? 2 : 1;
        int currentColumns = (int)grid.GetValue(LayoutColumnsProperty);

        if (currentColumns == targetColumns) return;

        grid.SetValue(LayoutColumnsProperty, targetColumns);

        grid.ColumnDefinitions.Clear();
        for (int i = 0; i < targetColumns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        int currentRow = 0;
        int currentCol = 0;

        foreach (View child in grid.Children.OfType<View>())
        {
            bool fullWidth = targetColumns == 1 || GetIsFullWidth(child);

            if (fullWidth)
            {
                if (currentCol > 0)
                {
                    currentRow++;
                    currentCol = 0;
                }

                Grid.SetRow(child, currentRow);
                Grid.SetColumn(child, 0);
                Grid.SetColumnSpan(child, targetColumns);
                currentRow++;
            }
            else
            {
                Grid.SetRow(child, currentRow);
                Grid.SetColumn(child, currentCol);
                Grid.SetColumnSpan(child, 1);
                currentCol++;

                if (currentCol >= targetColumns)
                {
                    currentCol = 0;
                    currentRow++;
                }
            }
        }

        if (currentCol > 0) currentRow++;

        grid.RowDefinitions.Clear();
        for (int r = 0; r < currentRow; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

#if DEBUG
    public static bool RunSelfCheck()
    {
        ColumnDefinition col = new();
        Border sidebar = new();
        Border searchBar = new();
        GridItemsLayout itemsLayout = new(ItemsLayoutOrientation.Vertical);

        // 1. Normal phone portrait (390x844dp) -> 1 column
        ApplyCenterSplitGrid(390, 844, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        // 2. Normal phone landscape (844x390dp) -> 1 column
        ApplyCenterSplitGrid(844, 390, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        // 3. Fold4 cover screen (344x882dp) -> 1 column
        ApplyCenterSplitGrid(344, 882, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        // 4. Portrait Foldable (690x829dp = 1812x2176 px) -> 50/50 master-detail, 2 cols standalone
        ApplyMasterDetail(690, 829, col, sidebar, searchBar, itemsLayout);
        System.Diagnostics.Debug.Assert(sidebar.IsVisible == true);
        System.Diagnostics.Debug.Assert(col.Width.IsStar && col.Width.Value == 1);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        ApplyCenterSplitGrid(690, 829, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 2);

        // 5. Landscape Foldable (829x690dp = 2176x1812 px) -> 280dp filter, 3 cols standalone
        ApplyMasterDetail(829, 690, col, sidebar, searchBar, itemsLayout);
        System.Diagnostics.Debug.Assert(sidebar.IsVisible == true);
        System.Diagnostics.Debug.Assert(col.Width.Value == 280);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 2);

        ApplyCenterSplitGrid(829, 690, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 3);

        // 6. Tent mode and Closed posture do not split master-detail
        FoldInfo tentInfo = new() { Posture = FoldPosture.Tent, IsSeparating = true };
        System.Diagnostics.Debug.Assert(!tentInfo.ShouldSplit(829, 690));

        FoldInfo closedInfo = new() { Posture = FoldPosture.Closed, IsSeparating = false };
        System.Diagnostics.Debug.Assert(!closedInfo.ShouldSplit(829, 690));

        return true;
    }
#endif
}
