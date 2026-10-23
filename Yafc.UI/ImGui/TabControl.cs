using System;
using System.Collections.Generic;
using System.Linq;

namespace Yafc.UI;

/// <summary>
/// Provides a tab control. Tab controls draw a set of tabs that can be clicked and the content of the active tab.
/// The tab buttons will be split into multiple rows if necessary.
/// </summary>
public sealed class TabControl {
    private readonly TabPage[] tabPages;

    private int activePage;
    private readonly List<TabRow> rows = [];
    private float layoutWidth;
    private ImGui gui = null!;

    /// <summary>
    /// Sets the active page index. If this is not a valid index into the array of tab pages, no tab will be drawn selected, and no page content will be drawn.
    /// The tabs will still be drawn, and the user may select any tab to make it active.
    /// </summary>
    /// <param name="pageIdx">The index of the newly active page, or an invalid index if no page should be active.</param>
    public void SetActivePage(int pageIdx) => activePage = pageIdx;

    /// <summary>
    /// Gets or sets the (minimum) padding between the left and right ends of the text and the left and right edges of the tab. Must not be negative.
    /// </summary>
    public float HorizontalTextPadding {
        get;
        set {
            if (field != value) {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                field = value;
                InvalidateLayout();
            }
        }
    } = .5f;

    /// <summary>
    /// Gets or sets the blank space left between two adjacent tabs. Must not be negative.
    /// </summary>
    public float HorizontalTabSeparation {
        get;
        set {
            if (field != value) {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                field = value;
                InvalidateLayout();
            }
        }
    } = .8f;

    /// <summary>
    /// Gets or sets the smallest horizontal scale factor that may be applied to title text before creating a new row of tabs. Must not be negative or greater than 1.
    /// Smaller values permit narrower tabs. <c>1</c> means no horizontal squishing is permitted (unless the tab doesn't fit even when given in its own row),
    /// and <c>0.67f</c> allows text to be drawn using as little as 67% of its natural width.
    /// If a tab's title text does not fit on a single row, it will be compressed to fit regardless of this setting.
    /// </summary>
    public float MaximumTextCompression {
        get;
        set {
            if (field != value) {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
                field = value;
                InvalidateLayout();
            }
        }
    } = .75f;

    /// <summary>
    /// Gets or sets the vertical spacing between rows of tabs. Must not be negative.
    /// </summary>
    // Does not invalidate layout. The layout is only concerned about widths, not heights.
    public float VerticalTabSeparation {
        get;
        set {
            if (field != value) {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                field = value;
            }
        }
    } = .25f;

    /// <summary>
    /// If <see cref="true"/>, tab buttons (not title text) will be stretched horizontally to fill complete rows.
    /// </summary>
    public bool FullWidthTabRows {
        get;
        set {
            if (field != value) {
                field = value;
                InvalidateLayout();
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets the height of the tab buttons. Must not be smaller than the height of a line of text.
    /// </summary>
    // Does not invalidate layout. The layout is only concerned about widths, not heights.
    // Because gui might not be available yet (and a different gui might change the text height?) this is not validated until rendering.
    public float TabHeight { get; set; } = 2.25f;

    /// <summary>
    /// Gets the non-text space required for the first tab in a row.
    /// </summary>
    private float FirstTabSpacing => HorizontalTextPadding * 2;
    /// <summary>
    /// Gets the non-text space required when adding an additional tab to a row.
    /// </summary>
    private float AdditionalTabSpacing => HorizontalTabSeparation + HorizontalTextPadding * 2;

    /// <summary>
    /// Constructs a new <see cref="TabControl"/> for displaying the specified <see cref="TabPage"/>s.
    /// </summary>
    /// <param name="tabPages">An array of <see cref="TabPage"/>s to be drawn.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tabPages"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tabPages"/> is an empty array.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="tabPages"/> contains a <see langword="null"/> value.</exception>
    public TabControl(params TabPage[] tabPages) {
        ArgumentNullException.ThrowIfNull(tabPages);
        ArgumentOutOfRangeException.ThrowIfZero(tabPages.Length);

        if (tabPages.Contains(null)) {
            throw new ArgumentException("tabPages must not contain null.", nameof(tabPages));
        }

        // Prevent changes to the array now that we've verified it. Any writable portions of the individual pages (if/when added) can still be changed.
        // This could be used for an IsEnabled property, for example.
        this.tabPages = [.. tabPages];
    }

    // Measure the tab titles and assign the tabs to rows.
    private void PerformLayout() {
        #region Assign tab buttons to rows and calculate the horizontal squish factors.

        rows.Clear();
        layoutWidth = gui.statePosition.Width;
        float rowTextWidth = GetTitleWidth(tabPages[0]);
        float rowPaddingWidth = FirstTabSpacing;
        int nextTabToAssign = 0;

        for (int i = 1; i < tabPages.Length; i++) {
            float textWidth = GetTitleWidth(tabPages[i]);

            rowPaddingWidth += AdditionalTabSpacing;
            rowTextWidth += textWidth;

            if (rowPaddingWidth + rowTextWidth * MaximumTextCompression > layoutWidth) {
                // Adding this tab to the current row takes too much squishing. Complete the current row and add this tab to the next one.
                rowPaddingWidth -= AdditionalTabSpacing;
                rowTextWidth -= textWidth;

                rows.Add((nextTabToAssign, i, Math.Min(1, (layoutWidth - rowPaddingWidth) / rowTextWidth)));
                nextTabToAssign = i;

                rowPaddingWidth = FirstTabSpacing;
                rowTextWidth = textWidth;
            }
        }
        // Complete the final row.
        rows.Add((nextTabToAssign, tabPages.Length, Math.Min(1, (layoutWidth - rowPaddingWidth) / rowTextWidth)));

        // Rows are tentatively assigned and the row count is fixed. Try to reduce horizontal compression by bumping row-final tabs to the next row:
        // 1: [Sixth]
        // 0: [First] [Second] [Third] [Fourth] [Fifth]
        // will become
        // 1: [Fifth] [Sixth]
        // 0: [First] [Second] [Third] [Fourth]
        // and then
        // 1: [Fourth] [Fifth] [Sixth]
        // 0: [First] [Second] [Third]
        // provided row 0 was experiencing horizontal compression.
        for (int sourceRow = 0; sourceRow < rows.Count - 1; sourceRow++) {
            if (rows[sourceRow].Compression >= 1) {
                continue;
            }
            int destinationRow = sourceRow + 1;

            float destinationCompression = GetRequiredCompression(tabPages[rows[destinationRow].AbsorbedRange]);
            if (destinationCompression > rows[sourceRow].Compression) {
                // After provisionally bumping the tab, destinationRow still has less compression than sourceRow had. Make the bump official.
                float sourceCompression = GetRequiredCompression(tabPages[rows[sourceRow].RejectedRange]);
                TabRow.BumpTab(rows[sourceRow], sourceCompression, rows[destinationRow], destinationCompression);

                // A tab was successfully bumped from this row, so we want to try bumping a tab into this row on the next loop iteration.
                // (The next iteration will be sourceRow = sourceRow - 2 + 1)
                // Except when this is row 0, in which case we'll just repeat row 0 and try to bump another tab out instead.
                sourceRow = Math.Max(-1, sourceRow - 2);
            }
        }

        #endregion

        #region Calculations for full-width rows

        // Remove results from previous layout passes.
        foreach (TabPage tabPage in tabPages) {
            tabPage.ButtonWidth = null;
        }

        if (FullWidthTabRows) {
            foreach ((int start, int end, _) in rows.Where(r => r.Compression == 1)) {
                // Do the math as if there's an extra options.HorizontalTabSeparation that has to be allocated after the final tab.
                float easyMathWidth = layoutWidth + HorizontalTabSeparation;
                List<(TabPage, float Width)> pages = [.. tabPages[start..end].Select(p => (p, GetTitleWidth(p) + AdditionalTabSpacing))];

                float desiredTabWidth = easyMathWidth / pages.Count;
                (TabPage, float Width) widestTab = pages.MaxBy(p => p.Width);
                while (widestTab.Width > desiredTabWidth) {
                    // The widest tab is too wide for equal allocation of the (remaining) space. Reserve its full required width and remove it from consideration.
                    easyMathWidth -= widestTab.Width;
                    pages.Remove(widestTab);
                    desiredTabWidth = easyMathWidth / pages.Count;
                    widestTab = pages.MaxBy(p => p.Width);
                }

                // The (remaining) tabs get equal allocation of the (remaining) space.
                foreach ((TabPage page, _) in pages) {
                    page.ButtonWidth = desiredTabWidth - HorizontalTabSeparation;
                }
            }
        }

        #endregion

        // Reverse the rows so the render can just use ROW_HEIGHT*rowIdx to draw the tabs from bottom up:
        // 0: [Seventh] [Eighth] [Ninth]
        // 1: [Fourth] [Fifth] [Sixth]
        // 2: [First] [Second] [Third]
        rows.Reverse();
    }

    private PageDrawer? drawer;

    /// <summary>
    /// Call to draw this <see cref="TabControl"/> and its active page.
    /// </summary>
    public void Build(ImGui gui) {
        // Implementation note: TabPage.Drawer is permitted to be null. Accommodating a null GuiBuilder was easy and useful for testing.
        // On the other hand, there's no reason to do that in real life, so the accommodation is not acknowledged in the nullable annotations.

        float minTabHeight = gui.GetTextDimensions(out _, "test").Y;
        if (TabHeight < minTabHeight) {
            throw new InvalidOperationException($"{nameof(TabHeight)} must be at least the height of a line of text, {minTabHeight}, but it was {TabHeight}.");
        }

        this.gui = gui;

        if (gui.statePosition.Width != layoutWidth) {
            PerformLayout();
        }

        #region Draw Tabs

        // Rotate the active page to the end of the list
        if (activePage >= 0 && activePage < tabPages.Length) {
            while (rows[^1].Start > activePage || rows[^1].End <= activePage) {
                rows.Add(rows[0]);
                rows.RemoveAt(0);
            }
        }

        for (int currentRow = 0; currentRow < rows.Count; currentRow++) {
            float startingX = gui.statePosition.Left;
            float startingY = gui.statePosition.Top + (TabHeight + VerticalTabSeparation) * currentRow;
            foreach (TabPage tabPage in tabPages[rows[currentRow].Range]) {
                float textWidth = GetTitleWidth(tabPage, rows[currentRow].Compression);
                Rect buttonRect = new(startingX, startingY, textWidth + HorizontalTextPadding * 2, TabHeight);
                Rect textRect = new(startingX + HorizontalTextPadding, buttonRect.Top, textWidth, TabHeight);
                RectAlignment textAlignment = RectAlignment.MiddleFullRow;
                if (tabPage.ButtonWidth.HasValue) {  // also, Compression == 1 (aka no horizontal squishing)
                    buttonRect.Width = tabPage.ButtonWidth.Value;
                    textRect = buttonRect;
                    textAlignment = RectAlignment.Middle;
                }

                if (Array.IndexOf(tabPages, tabPage) == activePage) {
                    // Draw the active tab as just a rectangle
                    gui.DrawRectangle(buttonRect, SchemeColor.Secondary);
                }
                // and all the other tabs as buttons
                else if (gui.BuildButton(buttonRect, SchemeColor.Primary, SchemeColor.PrimaryAlt)) {
                    activePage = Array.IndexOf(tabPages, tabPage);
                }

                gui.DrawText(textRect, tabPage.Title, textAlignment);
                startingX += buttonRect.Width + HorizontalTabSeparation;
            }

            // On every row except the last, draw a Primary bar across the bottom of the buttons, to make them look more tab-like.
            if (currentRow != rows.Count - 1) {
                float top = startingY + TabHeight;
                float bottom = top + VerticalTabSeparation / 2;
                // If we aren't using the full width, the connector stops half a HorizontalTabSeparation past the last tab button.
                // (equivalently, half a HorizontalTabSeparation before where the next button would be drawn, if it existed.)
                float right = Math.Min(startingX - HorizontalTabSeparation / 2, gui.statePosition.Right);
                gui.DrawRectangle(Rect.SideRect(gui.statePosition.Left, right, top, bottom), SchemeColor.Primary);
            }
        }

        gui.AllocateRect(0, (TabHeight + VerticalTabSeparation) * rows.Count - VerticalTabSeparation + .25f, 0); // Allocate space (vertical) for the tabs
        // On the last row, draw a Secondary bar across the bottom of the buttons, to connect the active tab to the controls that will be drawn.
        // Unlike the Primary bars, this one is always full-width.
        gui.DrawRectangle(new(gui.statePosition.X, gui.statePosition.Y - .25f, layoutWidth, .25f), SchemeColor.Secondary);

        #endregion

        #region Allocate and draw tab content

        using var controller = gui.StartOverlappingAllocations(false);

        drawer = new(gui, controller, tabPages, activePage);
        while (drawer.DrawNextPage()) { }
        drawer = null;
        #endregion
    }

    /// <summary>
    /// Requests the tab control report its remaining available content height. As a side effect, the active tab page will pause drawing until
    /// all other tabs have been drawn. It is not advisable to draw tab content taller than the height returned by this method.
    /// </summary>
    /// <remarks>It is possible for multiple tabs to call this method. If that happens, tabs that call this method earlier will get more accurate
    /// results. That is, if Tab A calls this method, Tab B draws normally, and Tab C calls this method, Tab C will get a response based on the
    /// height of Tab B, and can (but should not) further increase the content height. Tab A will then get a response based on the taller of tabs
    /// B and C. Like tab C, A can (but also should not) again increase the content height. If A does, it will defeat tab C's attempt to use all
    /// available vertical space.</remarks>
    /// <param name="minimumHeight">The minimum height that the remaining content needs. The return value will not be smaller than this
    /// parameter.</param>
    /// <returns>The available content height, based on all tabs that did not call this method and any tabs that called this method after the
    /// current tab.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this <see cref="TabControl"/> is not actively drawing tab pages.</exception>
    public float GetRemainingContentHeight(float minimumHeight = 0) {
        if (drawer == null) {
            throw new InvalidOperationException($"{nameof(GetRemainingContentHeight)} must only be called from a {nameof(GuiBuilder)} that is currently building a {nameof(TabPage)}.");
        }

        using (drawer.RememberState()) {
            drawer.gui.AllocateRect(0, minimumHeight);
            while (drawer.DrawNextPage()) { }
        }
        return drawer.GetHeight();
    }

    /// <summary>
    /// Call if you make as-yet-undetected changes that require recalculating the tab widths or tab row assignments.<br/>
    /// After confirming this call fixes things, update the property setters or body of <see cref="Build"/> detect that change and re-layout without an explicit call.
    /// </summary>
    public void InvalidateLayout() => layoutWidth = 0;

    private float GetTitleWidth(TabPage tabPage, float compression = 1) => gui.GetTextDimensions(out _, tabPage.Title).X * compression;

    // Measure the titles for the tab pages in this (potential) row and calculate the horizontal compression.
    private float GetRequiredCompression(IEnumerable<TabPage> tabPages) {
        float rowTextWidth = 0;
        float rowPaddingWidth = -HorizontalTabSeparation;
        foreach (TabPage tabPage in tabPages) {
            rowPaddingWidth += AdditionalTabSpacing;
            rowTextWidth += GetTitleWidth(tabPage);
        }

        return Math.Min(1, (layoutWidth - rowPaddingWidth) / rowTextWidth);
    }

    private record TabRow(int Start, int End, float Compression) {
        public int Start { get; private set; } = Start;
        public int End { get; private set; } = End;
        public float Compression { get; private set; } = Compression;

        public Range Range => Start..End;
        public Range AbsorbedRange => (Start - 1)..End;
        public Range RejectedRange => Start..(End - 1);

        internal static void BumpTab(TabRow sourceRow, float sourceCompression, TabRow destinationRow, float destinationCompression) {
            sourceRow.End--;
            sourceRow.Compression = sourceCompression;
            destinationRow.Start--;
            destinationRow.Compression = destinationCompression;
        }

        public static implicit operator TabRow((int Start, int End, float Compression) value) => new TabRow(value.Start, value.End, value.Compression);
    }

    /// <summary>
    /// Tracks the necessary details to allow <see cref="GetRemainingContentHeight"/> to start drawing a second tab while preserving the drawing
    /// state of the current tab. Each call to <see cref="GetRemainingContentHeight"/> will interrupt the current tab drawer and the current
    /// <c>while (drawer.DrawNextPage()) { }</c> loop and start a new loop. The new loop will drawing the remaining tabs (unless interrupted
    /// itself) and <see cref="GetRemainingContentHeight"/> will return the height available for use by the calling tab drawer.
    /// </summary>
    private sealed class PageDrawer(ImGui gui, ImGui.OverlappingAllocations controller, TabPage[] tabPages, int activePage) {
        public ImGui gui { get; } = gui;
        private int i = -1;
        public bool DrawNextPage() {
            if (++i >= tabPages.Length) {
                return false;
            }
            controller.StartNextAllocatePass(i == activePage);
            tabPages[i].Drawer?.Invoke(gui);

            return true;
        }

        public float GetHeight() => controller.maximumBottom - gui.statePosition.Top;

        public IDisposable RememberState() => new State(gui, controller, i == activePage);

        /// <summary>
        /// Saves and restores the current state when <see cref="GetRemainingContentHeight"/> needs to interrupt the current tab drawing.
        /// </summary>
        private sealed class State(ImGui gui, ImGui.OverlappingAllocations controller, bool drawing) : IDisposable {
            private readonly float initialTop = controller.currentTop;
            public void Dispose() {
                controller.StartNextAllocatePass(drawing);
                gui.AllocateRect(0, initialTop - controller.currentTop);
            }
        }
    }
}

/// <summary>
/// A description of a page to be drawn as part of a tab control.
/// </summary>
/// <param name="title">The text to be drawn on the tab button.</param>
/// <param name="drawer">The <see cref="GuiBuilder"/> to be called when drawing content for the tab page (if active) or allocating space (if not active).</param>
public sealed class TabPage(string title, GuiBuilder drawer) {
    /// <summary>
    /// Gets the text to be drawn in the tab button.
    /// </summary>
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));
    /// <summary>
    /// Gets the <see cref="GuiBuilder"/> that allocates space and draws content for the tab page.
    /// </summary>
    public GuiBuilder Drawer { get; } = drawer;

    /// <summary>
    /// Stores the width of the tab button, if extra width is required to accommodate <see cref="ImGuiUtils.TabControlOptions.FullWidthTabRows"/>
    /// </summary>
    internal float? ButtonWidth { get; set; }

    public static implicit operator TabPage((string Title, GuiBuilder Drawer) value) => new TabPage(value.Title, value.Drawer);
}
