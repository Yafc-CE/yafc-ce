using System.Collections.Generic;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class SearchableList<TData>(float height, Vector2 elementSize, VirtualScrollList<TData>.Drawer drawer, SearchableList<TData>.Filter filter, IComparer<TData>? comparer = null)
    : VirtualScrollList<TData>(height, elementSize, drawer) {

    public delegate bool Filter(TData data, SearchQuery searchTokens);

    private readonly Filter filterFunc = filter;
    // The contents of data at the last refresh, to notice when the source list is modified in place.
    private TData[] dataSnapshot = [];

    public new IReadOnlyList<TData> data {
        get;
        set {
            field = value ?? [];
            RefreshData();
        }
    } = [];

    public SearchQuery filter {
        get;
        set {
            field = value;
            RefreshData();
        }
    } = default;

    public override Vector2 MeasureContent(float width) {
        // Called when the parent is rebuilt, which does not rebuild the contents unless they are marked for it.
        RefreshDataIfSourceChanged();
        return base.MeasureContent(width);
    }

    protected override void BuildContents(ImGui gui) {
        // Also runs for every input event; only check while building, for example after a drag in the list.
        if (gui.isBuilding) {
            RefreshDataIfSourceChanged();
        }
        base.BuildContents(gui);
    }

    private void RefreshDataIfSourceChanged() {
        if (data.Count != dataSnapshot.Length) {
            RefreshData();
            return;
        }

        for (int i = 0; i < dataSnapshot.Length; i++) {
            if (!EqualityComparer<TData>.Default.Equals(data[i], dataSnapshot[i])) {
                RefreshData();
                return;
            }
        }
    }

    private void RefreshData() {
        dataSnapshot = [.. data];
        List<TData> list = [];
        if (!filter.empty) {
            foreach (var element in data) {
                if (filterFunc(element, filter)) {
                    list.Add(element);
                }
            }
        }
        else {
            list.AddRange(data);
        }

        if (comparer != null) {
            list.Sort(comparer);
        }

        base.data = list;
    }
}
