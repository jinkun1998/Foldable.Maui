using Foldable.Maui.Services;
using Microsoft.Maui.Controls;

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
    /// Applies 50/50 center-split on foldables (640-899dp) and fixed-sidebar on large screens (&gt;=900dp).
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

        bool isWideOrFoldable = width >= 640 && Math.Min(width, height) >= 550 && (width / height >= 0.70 || width >= 900);

        if (leftSidebar != null && leftCol != null)
        {
            leftSidebar.IsVisible = isWideOrFoldable;

            if (!isWideOrFoldable)
            {
                leftSidebar.WidthRequest = 0;
                leftCol.Width = new GridLength(0);
            }
            else if (width >= 900)
            {
                // Desktop / large tablet: fixed 320dp sidebar
                leftSidebar.WidthRequest = 320;
                leftCol.Width = new GridLength(320);
            }
            else
            {
                // Foldable unfolded: exact 50/50 center split down the crease
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
            int targetSpan = (!isWideOrFoldable || width < 1200) ? 1 : 2;
            if (listLayout.Span != targetSpan)
            {
                listLayout.Span = targetSpan;
            }
        }
    }

    /// <summary>
    /// Divides a CollectionView into 2 equal center-split columns on foldables and 3 on tablets.
    /// </summary>
    public static void ApplyCenterSplitGrid(double width, GridItemsLayout? listLayout, int breakpoint = 640)
    {
        if (listLayout == null || width <= 0) return;

        int newSpan = width >= 1100 ? 3 : (width >= breakpoint ? 2 : 1);
        if (listLayout.Span != newSpan)
        {
            listLayout.Span = newSpan;
        }
    }

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

        // 1. Foldable width (750dp) -> 50/50 center split
        ApplyMasterDetail(750, 800, col, sidebar, searchBar, itemsLayout);
        System.Diagnostics.Debug.Assert(sidebar.IsVisible == true);
        System.Diagnostics.Debug.Assert(searchBar.IsVisible == false);
        System.Diagnostics.Debug.Assert(col.Width.IsStar && col.Width.Value == 1);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        // 2. Phone width (400dp) -> single column, popup filter
        ApplyMasterDetail(400, 800, col, sidebar, searchBar, itemsLayout);
        System.Diagnostics.Debug.Assert(sidebar.IsVisible == false);
        System.Diagnostics.Debug.Assert(searchBar.IsVisible == true);
        System.Diagnostics.Debug.Assert(col.Width.Value == 0);

        // 3. Desktop / large tablet (1000dp) -> fixed 320dp sidebar
        ApplyMasterDetail(1000, 800, col, sidebar, searchBar, itemsLayout);
        System.Diagnostics.Debug.Assert(sidebar.IsVisible == true);
        System.Diagnostics.Debug.Assert(col.Width.Value == 320);

        // 4. Center-split standalone list
        ApplyCenterSplitGrid(750, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 2);
        ApplyCenterSplitGrid(400, itemsLayout);
        System.Diagnostics.Debug.Assert(itemsLayout.Span == 1);

        return true;
    }
#endif

}
