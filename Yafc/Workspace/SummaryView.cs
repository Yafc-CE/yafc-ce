using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class SummaryView : ProjectPageView<Summary> {
    /// <summary>Height of each row</summary>
    private float rowHeight;

    /// <summary>Some padding to have the contents of the first column not 'stick' to the rest of the UI</summary>
    private readonly Padding FirstColumnPadding = new Padding(1f, 1.5f, 0, 0);
    private static float firstColumnWidth;
    private const float DefaultScrollHeight = 10f;

    private class SummaryTabColumn : TextDataColumn<ProjectPage> {
        public SummaryTabColumn() : base(LSs.Page, firstColumnWidth) {
        }

        public override void BuildElement(ImGui gui, ProjectPage page) {
            width = firstColumnWidth;

            if (page?.contentType != typeof(ProductionTable)) {
                return;
            }

            using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f))) {
                gui.spacing = 0.2f;

                if (page.icon != null) {
                    gui.BuildIcon(page.icon.GetIcon(), FirstColumnIconSize);
                }
                else {
                    _ = gui.AllocateRect(0f, FirstColumnIconSize);
                }

                gui.BuildText(page.name);
            }
        }
    }

    private sealed class SummaryDataColumn(SummaryView view) : TextDataColumn<ProjectPage>(LSs.SummaryColumnLinked, float.MaxValue) {
        public override void BuildElement(ImGui gui, ProjectPage page) {
            if (page?.contentType != typeof(ProductionTable)) {
                return;
            }

            ProductionTable table = (ProductionTable)page.content;
            using var grid = gui.EnterInlineGrid(ElementWidth, ElementSpacing);
            foreach ((string name, GoodDetails details, bool enoughProduced) in view.GoodsToDisplay()) {
                ProductionLink? link = table.links.Find(x => x.goods.QualityName() == name);
                grid.Next();
                bool isDrawn = false;

                if (link != null) {
                    if (link.amount != 0f) {
                        DrawProvideProduct(gui, link, page, details, enoughProduced);
                        isDrawn = true;
                    }
                }
                else {
                    if (Array.Exists(table.flow, x => x.goods.QualityName() == name)) {
                        ProductionTableFlow flow = Array.Find(table.flow, x => x.goods.QualityName() == name);

                        if (Math.Abs(flow.amount) > Epsilon) {

                            DrawRequestProduct(gui, flow, enoughProduced);
                            isDrawn = true;
                        }
                    }
                }
                if (!isDrawn) {
                    // Reserve empty space to prevent 'compressing' empty rows
                    _ = gui.AllocateRect(0f, view.rowHeight);
                }
            }
        }

        private static void DrawProvideProduct(ImGui gui, ProductionLink element, ProjectPage page, GoodDetails goodInfo, bool enoughProduced) {
            SchemeColor iconColor;

            if (element.amount > 0 && enoughProduced) {
                // Production matches consumption
                iconColor = SchemeColor.Primary;
            }
            else if (element.amount < 0 && goodInfo.extraProduced == -element.amount) {
                // Extra produced matches input goal
                iconColor = SchemeColor.Primary;
            }
            else {
                // Production and consumption does not match
                iconColor = SchemeColor.Error;
            }

            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            DisplayAmount amount = new(element.amount, element.goods.target.flowUnitOfMeasure);
            GoodsWithAmountEvent evt = gui.BuildFactorioObjectWithEditableAmount(element.goods, amount, ButtonDisplayStyle.ProductionTableScaled(iconColor));

            if (evt == GoodsWithAmountEvent.TextEditing && amount.Value != 0) {
                _ = SetProviderAmount(element, page, amount.Value);
            }
            else if (evt == GoodsWithAmountEvent.LeftButtonClick) {
                _ = SetProviderAmount(element, page, YafcRounding(goodInfo.sum));
            }
        }
        private static void DrawRequestProduct(ImGui gui, ProductionTableFlow flow, bool enoughExtraProduced) {
            SchemeColor iconColor;
            if (flow.amount > Epsilon) {
                if (!enoughExtraProduced) {
                    // Not enough in extra production, while being depended on
                    iconColor = SchemeColor.Error;
                }
                else {
                    // Enough extra product produced
                    iconColor = SchemeColor.Green;
                }
            }
            else {
                // Flow amount negative or (almost, due to rounding errors) zero, just a regular request (nothing to indicate)
                iconColor = SchemeColor.None;
            }

            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            _ = gui.BuildFactorioObjectWithAmount(flow.goods, new(-flow.amount, flow.goods.target.flowUnitOfMeasure), ButtonDisplayStyle.ProductionTableScaled(iconColor));
        }

        internal static Task SetProviderAmount(ProductionLink element, ProjectPage page, float newAmount) {
            element.RecordUndo().amount = newAmount;
            return page.RunSolveJob();
        }
    }

    private static readonly float Epsilon = 1e-5f;
    private static readonly float ElementWidth = 3;
    private static readonly float ElementSpacing = 1;
    private static readonly float FirstColumnIconSize = 1.5f;

    private struct GoodDetails {
        public float totalProvided;
        public float totalNeeded;
        public float extraProduced;
        public float sum;
    }

    private Project project;
    private SearchQuery searchQuery;

    private readonly LinkedScrollArea leftScrollArea;
    private readonly LinkedScrollArea rightScrollArea;
    private readonly SummaryTabColumn tabColumn;
    private readonly SummaryDataColumn goodsColumn;
    private readonly DataGrid<ProjectPage> leftGrid;
    private readonly DataGrid<ProjectPage> rightGrid;

    private Dictionary<string, GoodDetails> allGoods = [];

    private IEnumerable<(string name, GoodDetails details, bool enoughProduced)> GoodsToDisplay() {
        foreach (KeyValuePair<string, GoodDetails> goodInfo in allGoods) {
            if (!searchQuery.Match(goodInfo.Key)) {
                continue;
            }

            float amountAvailable = YafcRounding((goodInfo.Value.totalProvided > 0 ? goodInfo.Value.totalProvided : 0) + goodInfo.Value.extraProduced);
            float amountNeeded = YafcRounding((goodInfo.Value.totalProvided < 0 ? -goodInfo.Value.totalProvided : 0) + goodInfo.Value.totalNeeded);

            if (model == null || (model.showOnlyIssues && (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0))) {
                continue;
            }

            bool enoughProduced = amountAvailable >= amountNeeded;

            yield return (goodInfo.Key, goodInfo.Value, enoughProduced);
        }
    }

    private IEnumerable<(string name, GoodDetails details, float excess)> GoodsToBalance() {
        foreach (KeyValuePair<string, GoodDetails> goodInfo in allGoods) {
            float amountAvailable = YafcRounding((goodInfo.Value.totalProvided > 0 ? goodInfo.Value.totalProvided : 0) + goodInfo.Value.extraProduced);
            float amountNeeded = YafcRounding((goodInfo.Value.totalProvided < 0 ? -goodInfo.Value.totalProvided : 0) + goodInfo.Value.totalNeeded);

            if (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0) {
                continue;
            }

            yield return (goodInfo.Key, goodInfo.Value, amountAvailable - amountNeeded);
        }
    }

    public SummaryView(Project project) {
        tabColumn = new SummaryTabColumn();
        goodsColumn = new SummaryDataColumn(this);
        leftGrid = new DataGrid<ProjectPage>(tabColumn);
        rightGrid = new DataGrid<ProjectPage>(goodsColumn);
        // Only the right side draws the visible vertical scrollbar; the left's vertical scrolling is mirrored from
        // the right via Link() so both sides stay aligned without a second visible scrollbar.
        leftScrollArea = new LinkedScrollArea(DefaultScrollHeight, BuildLeftScrollArea, horizontal: false, drawVerticalScrollbar: false);
        rightScrollArea = new LinkedScrollArea(DefaultScrollHeight, BuildRightScrollArea, horizontal: true, drawVerticalScrollbar: true);
        leftScrollArea.Link(rightScrollArea);

        SetProject(project);
    }

    [MemberNotNull(nameof(project))]
    public void SetProject(Project project) {
        if (this.project != null) {
            this.project.metaInfoChanged -= Recalculate;

            foreach (ProjectPage page in this.project.pages) {
                page.contentChanged -= Recalculate;
            }
        }

        this.project = project;
        project.metaInfoChanged += Recalculate;

        foreach (ProjectPage page in project.pages) {
            page.contentChanged += Recalculate;
        }
    }

    protected override void BuildPageTooltip(ImGui gui, Summary contents) {
    }

    protected override void BuildHeader(ImGui gui) {
        base.BuildHeader(gui);

        gui.allocator = RectAllocator.Center;
        gui.BuildText(LSs.SummaryHeader, new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        gui.allocator = RectAllocator.LeftAlign;
    }

    private float CalculateFirstColumWidth(ImGui gui) {
        // At the least the icons should fit
        float width = FirstColumnIconSize;

        foreach (Guid displayPage in project.displayPages) {
            ProjectPage? page = project.FindPage(displayPage);

            if (page?.contentType != typeof(ProductionTable)) {
                continue;
            }

            Vector2 textSize = gui.GetTextDimensions(out _, page.name);
            width = MathF.Max(width, textSize.X);
        }

        // Include padding to perfectly match
        return width + FirstColumnPadding.left + FirstColumnPadding.right;
    }

    protected override async void BuildContent(ImGui gui) {
        using (gui.EnterRow()) {
            _ = gui.AllocateRect(0, 2); // Increase row height to 2, for vertical centering.

            if (gui.BuildCheckBox(LSs.SummaryOnlyShowIssues, model?.showOnlyIssues ?? false, out bool newValue)) {
                model!.showOnlyIssues = newValue; // null-forgiving: when model is null, the page is no longer being displayed, so no clicks can happen.
                Recalculate();
            }

            using (gui.EnterRowWithHelpIcon(LSs.SummaryAutoBalanceHint, false)) {
                if (gui.BuildButton(LSs.SummaryAutoBalance)) {
                    await AutoBalance();
                }
            }
        }
        gui.AllocateSpacing();

        if (gui.isBuilding) {
            firstColumnWidth = CalculateFirstColumWidth(gui);
            rowHeight = ButtonDisplayStyle.ProductionTableUnscaled.Size + gui.PixelsToUnits(gui.GetFontSize().lineSize);
        }

        // Lay out left (sticky) and right (horizontally scrollable) side-by-side. The left side's scrollbar is
        // hidden, so it doesn't reserve scrollbar width. DataGrid.BuildRow shifts the column by `innerPadding.left`
        // twice (once via the row's starting x, once via the manual-rect padding) before reserving `innerPadding.right`
        // at the right edge — so account for both inner-padding halves here.
        using (gui.EnterRow(spacing: 0, allocator: RectAllocator.LeftRow)) {
            // Compute availableHeight *inside* the row so EnterRow's implicit spacing is already accounted for in
            // gui.statePosition.Y. visibleSize.Y - headerHeight is the body's viewport; subtracting the current top
            // gives the exact remaining height. Fall back to DefaultScrollHeight on the first build, before
            // ProjectPageView.Build has populated visibleSize.
            float availableHeight = MathF.Max(DefaultScrollHeight, visibleSize.Y - headerHeight - gui.statePosition.Y);
            float leftWidth = firstColumnWidth + (2 * DataGrid<ProjectPage>.innerPadding.left) + DataGrid<ProjectPage>.innerPadding.right;
            using (gui.EnterFixedPositioning(leftWidth, 0, default)) {
                leftScrollArea.Build(gui, availableHeight);
            }
            using (gui.EnterFixedPositioning(gui.width, 0, default)) {
                rightScrollArea.Build(gui, availableHeight);
            }
        }
    }

    private async Task AutoBalance() {
        try {
            // The things we've already updated, and the error each had before their previous update.
            Queue<ProductionLink> updateOrder = [];
            Dictionary<ProductionLink, float> previousUpdates = [];
            int negativeFeedbackCount = 0;

            for (int i = 0; i < 1000; i++) { // No matter what else happens, give up after 1000 clicks.
                float? oldExcess = null;
                GoodDetails details;
                float excess;
                ProductionLink? link;

                while (true) {
                    // Look for a product that (a) has a mismatch, (b) has exactly one link in the displayed pages, and (c) hasn't been updated yet.
                    (details, excess, link) = GoodsToBalance()
                        // Find the link
                        .Select(x => (x.details, x.excess, link: DisplayTables.Select(t => t.links.FirstOrDefault(l => l.goods.QualityName() == x.name && l.amount != 0)).WhereNotNull().SingleOrDefault(false)))
                        // Find an item with exactly one link that hasn't been updated yet. (Or has been removed to allow it to be updated again.)
                        .FirstOrDefault(x => x.link != null && !previousUpdates.ContainsKey(x.link));

                    if (link != null) { break; }

                    if (updateOrder.Count == 0) {
                        return;
                    }
                    // If nothing was found, allow the least-recently updated product to be updated again, but remember its previous state so we can tell if we're making progress.
                    ProductionLink removedLink = updateOrder.Dequeue();
                    oldExcess = previousUpdates[removedLink];
                    _ = previousUpdates.Remove(removedLink);
                }

                if (oldExcess - Math.Abs(excess) <= Epsilon) {
                    // TODO: The negative feedback detection sometimes gets triggered, but sometimes the `updateOrder.Count == 0` check gets triggered instead.
                    // This probably needs to be revisited when fixing https://github.com/Yafc-CE/yafc-ce/issues/169
                    // The previous positive difference for this product is less than the current (supposedly better) positive difference.
                    if (++negativeFeedbackCount > 3) {
                        return; // Too many negative feedbacks detected; give up.
                    }
                    else { // Skip this for now, in hopes of getting a better result later.
                        updateOrder.Enqueue(link);
                        previousUpdates[link] = Math.Abs(excess);
                        continue;
                    }
                }

                updateOrder.Enqueue(link);
                previousUpdates[link] = Math.Abs(excess);

                await SummaryDataColumn.SetProviderAmount(link, (ProjectPage)link.owner.owner, details.sum);
            }
            _ = model.showOnlyIssues;
        }
        finally {
            // HACK: Nothing left to change, but force a recalculate anyway, so the user doesn't have to view all the tabs before clicking Auto balance again.
            // Sometimes this will cause updates that require an additional button click.
            // This is probably related to https://github.com/Yafc-CE/yafc-ce/issues/169
            foreach (ProjectPage page in DisplayPages) {
                await page.RunSolveJob();
            }
        }
    }

    private IEnumerable<ProductionTable> DisplayTables => project.displayPages.Select(p => project.FindPage(p)?.content as ProductionTable).WhereNotNull();
    private IEnumerable<ProjectPage> DisplayPages => DisplayTables.Select(t => (ProjectPage)t.owner);

    private void BuildLeftScrollArea(ImGui gui) {
        foreach (ProjectPage page in DisplayPages) {
            _ = leftGrid.BuildRow(gui, page, minRowHeight: rowHeight);
        }
        // Match the right side's reserved-for-H-scrollbar space so the synced vertical scroll positions align
        // perfectly between the two sides.
        _ = gui.AllocateRect(0, ScrollArea.ScrollbarSize);
    }

    private void BuildRightScrollArea(ImGui gui) {
        foreach (ProjectPage page in DisplayPages) {
            _ = rightGrid.BuildRow(gui, page, minRowHeight: rowHeight);
        }
        // Reserve space below the last row so it can be scrolled clear of the horizontal scrollbar that sits at
        // the bottom of this scroll area.
        _ = gui.AllocateRect(0, ScrollArea.ScrollbarSize);
    }

    private void Recalculate() => Recalculate(false);

    private void Recalculate(bool visualOnly) {
        Dictionary<string, GoodDetails> newGoods = [];

        foreach (Guid displayPage in project.displayPages) {
            ProjectPage? page = project.FindPage(displayPage);
            ProductionTable? content = page?.content as ProductionTable;

            if (content == null) {
                continue;
            }

            foreach (IProductionLink link in content.allLinks) {
                if (link.amount != 0f) {
                    GoodDetails value = newGoods.GetValueOrDefault(link.goods.QualityName());
                    value.totalProvided += YafcRounding(link.amount); ;
                    newGoods[link.goods.QualityName()] = value;
                }
            }

            foreach (ProductionTableFlow flow in content.flow) {
                if (flow.amount < -Epsilon) {
                    GoodDetails value = newGoods.GetValueOrDefault(flow.goods.QualityName());
                    value.totalNeeded -= YafcRounding(flow.amount); ;
                    value.sum -= YafcRounding(flow.amount); ;
                    newGoods[flow.goods.QualityName()] = value;
                }
                else if (flow.amount > Epsilon) {
                    if (!content.allLinks.Any(x => x.goods == flow.goods)) {
                        // Only count extras if not linked
                        GoodDetails value = newGoods.GetValueOrDefault(flow.goods.QualityName());
                        value.extraProduced += YafcRounding(flow.amount);
                        value.sum -= YafcRounding(flow.amount);
                        newGoods[flow.goods.QualityName()] = value;
                    }
                }
            }
        }

        int count = 0;
        foreach (KeyValuePair<string, GoodDetails> entry in newGoods) {
            float amountAvailable = YafcRounding((entry.Value.totalProvided > 0 ? entry.Value.totalProvided : 0) + entry.Value.extraProduced);
            float amountNeeded = YafcRounding((entry.Value.totalProvided < 0 ? -entry.Value.totalProvided : 0) + entry.Value.totalNeeded);

            if (model != null && model.showOnlyIssues && (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0)) {
                continue;
            }
            count++;
        }

        // Width of the goods column: N cells of (ElementWidth + ElementSpacing), minus the trailing inter-cell
        // spacing after the last cell, which doesn't visually exist.
        goodsColumn.width = (count * (ElementWidth + ElementSpacing)) - ElementSpacing;

        allGoods = newGoods;

        Rebuild(visualOnly);
        leftScrollArea.RebuildContents();
        rightScrollArea.RebuildContents();
    }

    // Convert/truncate value as shown in UI to prevent slight mismatches
    private static float YafcRounding(float value) {
        _ = DataUtils.TryParseAmount(DataUtils.FormatAmount(value, UnitOfMeasure.PerSecond), out float result, UnitOfMeasure.PerSecond);
        return result;
    }

    public override void SetSearchQuery(SearchQuery query) {
        searchQuery = query;
        bodyContent.Rebuild();
        leftScrollArea.Rebuild();
        rightScrollArea.Rebuild();
    }
}
