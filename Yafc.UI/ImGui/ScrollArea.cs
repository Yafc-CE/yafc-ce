using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;
using Yafc.Core;

namespace Yafc.UI;

/// <summary> Provide scrolling support for any component.</summary>
/// <param name="vertical">Whether vertical scrolling is enabled (mouse wheel, keyboard, programmatic <see cref="scrollY"/>).</param>
/// <param name="horizontal">Whether horizontal scrolling is enabled (Ctrl+wheel, keyboard, programmatic <see cref="scrollX"/>).</param>
/// <param name="collapsible">If <see langword="true"/>, the rendered area shrinks to fit the content when the content is smaller
/// than the available height. If <see langword="false"/>, the area always uses the full available height.</param>
/// <param name="drawVerticalScrollbar">When <see langword="false"/> and <paramref name="vertical"/> is <see langword="true"/>,
/// vertical scrolling still works (mouse wheel, keyboard, programmatic <see cref="scrollY"/>) but no scrollbar is drawn and no
/// width is reserved for it. Useful for one half of a pair of side-by-side, vertically-synced scroll areas where only one
/// visible scrollbar is desired.</param>
/// <remarks> The component should use the <see cref="scroll"/> property to get the offset of rendering the contents. </remarks>
public abstract class Scrollable(bool vertical, bool horizontal, bool collapsible, bool drawVerticalScrollbar = true) : IKeyboardFocus {
    private readonly bool drawVerticalScrollbar = drawVerticalScrollbar;
    /// <summary>Required size to fit Scrollable (child) contents</summary>
    private Vector2 requiredContentSize;
    /// <summary>This rectangle contains the available size (and position) of the scrollable content</summary>
    private Rect contentRect;
    /// <summary>Maximum scroller offset, calculated with the <see cref="requiredContentSize"/> and the available size</summary>
    private Vector2 maxScroll;
    private Vector2 _scroll;
    private ImGui? gui;
    public const float ScrollbarSize = 1f;

    // Padding to add at the bottom of the scroll area to be able to scroll past the
    // last item; needs useBottomPadding to be set to true in method Build()
    private const float BottomPaddingInPixels = 100f;

    protected abstract void PositionContent(ImGui gui, Rect viewport);

    /// <param name="availableHeight">Available height in parent context for the Scrollable</param>
    public void Build(ImGui gui, float availableHeight, bool useBottomPadding = false) {
        this.gui = gui;
        if (!gui.enableDrawing) {
            return;
        }

        var rect = gui.statePosition;
        float width = rect.Width;

        if (vertical && drawVerticalScrollbar) {
            width -= ScrollbarSize;
        }

        if (gui.isBuilding) {
            // Calculate required size, including padding if needed
            requiredContentSize = MeasureContent(width, gui);

            if (requiredContentSize.Y > availableHeight && useBottomPadding) {
                requiredContentSize.Y += BottomPaddingInPixels / gui.pixelsPerUnit;
            }
        }

        float realHeight = collapsible ? MathF.Min(requiredContentSize.Y, availableHeight) : availableHeight;

        if (gui.isBuilding) {
            contentRect = rect;
            contentRect.Width = width;

            maxScroll = Vector2.Max(requiredContentSize - new Vector2(contentRect.Width, availableHeight), Vector2.Zero);
            scroll = Vector2.Clamp(scroll, Vector2.Zero, maxScroll);

            contentRect.Height = realHeight;

            if (horizontal && maxScroll.X > 0) {
                contentRect.Height -= ScrollbarSize;
            }

            PositionContent(gui, contentRect);
        }

        rect.Height = realHeight;
        _ = gui.EncapsulateRect(rect);

        // Calculate scroller dimensions.
        Vector2 size = new Vector2(width, availableHeight);
        var scrollerSize = size * size / (size + maxScroll);
        scrollerSize = Vector2.Max(scrollerSize, Vector2.One);
        var scrollerStart = _scroll / maxScroll * (size - scrollerSize);

        if ((gui.action == ImGuiAction.MouseDown || gui.action == ImGuiAction.MouseScroll) && rect.Contains(gui.mousePosition)) {
            InputSystem.Instance.SetKeyboardFocus(this);
        }

        if (gui.action == ImGuiAction.MouseScroll) {
            if (vertical && !InputSystem.Instance.control && gui.ConsumeEvent(rect)) {
                scrollY += gui.actionParameter * 3f;
            }
            else if (horizontal && InputSystem.Instance.control && gui.ConsumeEvent(rect)) {
                scrollX += gui.actionParameter * 3f;
            }
        }
        else {
            if (horizontal && maxScroll.X > 0f) {
                Rect scrollbarRect = new Rect(rect.X, rect.Bottom - ScrollbarSize, rect.Width, ScrollbarSize);
                Rect scrollerRect = new Rect(rect.X + scrollerStart.X, scrollbarRect.Y, scrollerSize.X, ScrollbarSize);
                BuildScrollBar(gui, 0, in scrollbarRect, in scrollerRect);
            }

            if (vertical && drawVerticalScrollbar && maxScroll.Y > 0f) {
                Rect scrollbarRect = new Rect(rect.Right - ScrollbarSize, rect.Y, ScrollbarSize, rect.Height);
                Rect scrollerRect = new Rect(scrollbarRect.X, rect.Y + scrollerStart.Y, ScrollbarSize, scrollerSize.Y);
                BuildScrollBar(gui, 1, in scrollbarRect, in scrollerRect);
            }
        }
    }

    private void BuildScrollBar(ImGui gui, int axis, in Rect scrollbarRect, in Rect scrollerRect) {
        switch (gui.action) {
            case ImGuiAction.MouseDown:
                if (scrollerRect.Contains(gui.mousePosition)) {
                    _ = gui.ConsumeMouseDown(scrollbarRect);
                }

                break;
            case ImGuiAction.MouseMove:
                if (gui.IsMouseDown(scrollbarRect, SDL.SDL_BUTTON_LEFT)) {
                    if (axis == 0) {
                        scrollX += InputSystem.Instance.mouseDelta.X * requiredContentSize.X / scrollbarRect.Width;
                    }
                    else {
                        scrollY += InputSystem.Instance.mouseDelta.Y * requiredContentSize.Y / scrollbarRect.Height;
                    }
                }
                break;
            case ImGuiAction.Build:
                gui.DrawRectangle(scrollerRect, SchemeColor.Grey);
                break;
        }
    }

    /// <summary>X and Y positions of the scrollers</summary>
    public virtual Vector2 scroll {
        get => _scroll;
        set {
            value = Vector2.Clamp(value, Vector2.Zero, maxScroll);

            if (value != _scroll) {
                _scroll = value;
                gui?.Rebuild();
            }
        }
    }

    /// <summary>Position of the Y scroller</summary>
    public float scrollY {
        get => _scroll.Y;
        set => scroll = new Vector2(_scroll.X, value);
    }

    /// <summary>Position of the X scroller</summary>
    public float scrollX {
        get => _scroll.X;
        set => scroll = new Vector2(value, _scroll.Y);
    }

    ///<summary>This method is called when the required area of the <see cref="Scrollable"/> for the provided <paramref name="width"/> is needed.</summary>
    /// <returns>The required area of the contents of the <see cref="Scrollable"/>.</returns>
    public abstract Vector2 MeasureContent(float width, ImGui gui);

    public bool KeyDown(SDL.SDL_Keysym key) {
        if (gui?.enableDrawing != true) {
            return false;
        }

        bool ctrl = InputSystem.Instance.control;
        bool shift = InputSystem.Instance.shift;

        switch ((ctrl, shift, key.scancode)) {
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_UP):
                if (!vertical) {
                    return false;
                }
                scrollY -= 3;
                return true;
            case (true, false, SDL.SDL_Scancode.SDL_SCANCODE_UP): // ctrl+up = home
                if (!vertical) {
                    return false;
                }
                scrollY = 0;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_DOWN):
                if (!vertical) {
                    return false;
                }
                scrollY += 3;
                return true;
            case (true, false, SDL.SDL_Scancode.SDL_SCANCODE_DOWN): // ctrl+down = end
                if (!vertical) {
                    return false;
                }
                scrollY = maxScroll.Y;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_LEFT):
                if (!horizontal) {
                    return false;
                }
                scrollX -= 3;
                return true;
            case (true, false, SDL.SDL_Scancode.SDL_SCANCODE_LEFT): // ctrl+left = start of line
                if (!horizontal) {
                    return false;
                }
                scrollX = 0;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_RIGHT):
                if (!horizontal) {
                    return false;
                }
                scrollX += 3;
                return true;
            case (true, false, SDL.SDL_Scancode.SDL_SCANCODE_RIGHT):  // ctrl+right = end of line
                if (!horizontal) {
                    return false;
                }
                scrollX = maxScroll.X;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN):
                if (!vertical) {
                    return false;
                }
                scrollY += contentRect.Height;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP):
                if (!vertical) {
                    return false;
                }
                scrollY -= contentRect.Height;
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_HOME):
                scrollY = 0;
                if (!vertical) {
                    return false;
                }
                return true;
            case (false, false, SDL.SDL_Scancode.SDL_SCANCODE_END):
                if (!vertical) {
                    return false;
                }
                scrollY = maxScroll.Y;
                return true;
            default:
                return false;
        }
    }

    public bool TextInput(string input) => false;

    public bool KeyUp(SDL.SDL_Keysym key) => false;

    public void FocusChanged(bool focused) { }
}

/// <summary>Provides a builder to the Scrollable to render the contents.</summary>
public abstract class ScrollAreaBase : Scrollable {
    protected ImGui contents;
    private ImGui.OverlappingAllocations? controller;
    public float height { get; }

    /// <param name="height">Default available height passed to <see cref="Scrollable.Build(ImGui, float, bool)"/> when no explicit
    /// height is provided.</param>
    /// <param name="padding">Padding applied to the inner contents <see cref="ImGui"/>.</param>
    /// <param name="collapsible">See <see cref="Scrollable"/>.</param>
    /// <param name="vertical">See <see cref="Scrollable"/>.</param>
    /// <param name="horizontal">See <see cref="Scrollable"/>.</param>
    /// <param name="drawVerticalScrollbar">See <see cref="Scrollable"/>.</param>
    public ScrollAreaBase(float height, Padding padding, bool collapsible = false, bool vertical = true, bool horizontal = false, bool drawVerticalScrollbar = true) : base(vertical, horizontal, collapsible, drawVerticalScrollbar) {
        contents = new ImGui(BuildContents, padding, clip: true);
        this.height = height;
    }

    protected override void PositionContent(ImGui gui, Rect viewport) {
        gui.DrawPanel(viewport, contents);
        contents.offset = -scroll;
    }

    public void Build(ImGui gui) {
        controller?.Dispose();
        // Copy enableDrawing permission from gui to contents.
        controller = contents.StartOverlappingAllocations(gui.enableDrawing);
        Build(gui, height);
    }

    protected abstract void BuildContents(ImGui gui);

    public void RebuildContents() => contents.Rebuild();

    ///<summary>Calculates the content dimensions by (re)building the contents using <see cref="BuildContents"/></summary>
    public override Vector2 MeasureContent(float width, ImGui gui) => contents.CalculateState(width, gui.pixelsPerUnit);
}

///<summary>Area with scrollbars, which will be visible if it does not fit in the parent area in order to let the user fully view the content of the area.</summary>
/// <param name="height">See <see cref="ScrollAreaBase"/>.</param>
/// <param name="builder">Callback that draws the scroll area's contents into the inner <see cref="ImGui"/>.</param>
/// <param name="padding">See <see cref="ScrollAreaBase"/>.</param>
/// <param name="collapsible">See <see cref="Scrollable"/>.</param>
/// <param name="vertical">See <see cref="Scrollable"/>.</param>
/// <param name="horizontal">See <see cref="Scrollable"/>.</param>
/// <param name="drawVerticalScrollbar">See <see cref="Scrollable"/>.</param>
public class ScrollArea(float height, GuiBuilder builder, Padding padding = default, bool collapsible = false, bool vertical = true, bool horizontal = false, bool drawVerticalScrollbar = true) : ScrollAreaBase(height, padding, collapsible, vertical, horizontal, drawVerticalScrollbar) {
    protected override void BuildContents(ImGui gui) => builder(gui);

    public void Rebuild() => RebuildContents();
}

/// <summary>A vertical-scrolling area whose vertical scroll position is mirrored to a linked partner, so two
/// side-by-side scroll areas stay aligned. Call <see cref="Link"/> on either side to pair them.</summary>
/// <param name="height">See <see cref="ScrollAreaBase"/>.</param>
/// <param name="builder">See <see cref="ScrollArea"/>.</param>
/// <param name="horizontal">See <see cref="Scrollable"/>.</param>
/// <param name="drawVerticalScrollbar">See <see cref="Scrollable"/>.</param>
public class LinkedScrollArea(float height, GuiBuilder builder, bool horizontal, bool drawVerticalScrollbar)
    : ScrollArea(height, builder, default, collapsible: false, vertical: true, horizontal: horizontal, drawVerticalScrollbar: drawVerticalScrollbar) {

    private LinkedScrollArea? partner;
    private bool syncing;

    /// <summary>Pair this scroll area with <paramref name="other"/> so their vertical scroll positions stay in sync.
    /// Mouse-wheel, keyboard, or programmatic scrolling on either side updates both.</summary>
    public void Link(LinkedScrollArea other) {
        partner = other;
        other.partner = this;
    }

    public override Vector2 scroll {
        get => base.scroll;
        set {
            base.scroll = value;
            if (partner != null && !syncing) {
                partner.MirrorScrollY(base.scroll.Y);
            }
        }
    }

    private void MirrorScrollY(float y) {
        // The flag breaks the otherwise-infinite mirror cycle (A → B → A → …); try/finally guarantees it's cleared
        // even if the assignment ever throws, so a failure can't permanently desync the pair.
        syncing = true;
        try {
            scrollY = y;
        }
        finally {
            syncing = false;
        }
    }

    public void Build(ImGui gui, float availableHeight) => Build(gui, availableHeight, useBottomPadding: false);
}

public class VirtualScrollList<TData>(float height, Vector2 elementSize, VirtualScrollList<TData>.Drawer drawer, Padding padding = default,
    Action<int, int>? reorder = null, bool collapsible = false) : ScrollAreaBase(height, padding, collapsible) {

    // When rendering the scrollable content, render 'blocks' of 4 rows at a time. (As far as I can tell, any positive value works. Shadow picked 4, so I kept that.)
    private const int BufferRows = 4;
    // The first block of bufferRows that was rendered last time BuildContents was called. If it changes while scrolling, we need to re-render the scrollable content.
    private int firstVisibleBlock;
    private int elementsPerRow;
    private IReadOnlyList<TData> _data = [];
    private readonly int maxRowsVisible = MathUtils.Ceil(height / elementSize.Y) + BufferRows + 1;
    private readonly Vector2 elementSize = elementSize.X > 0 && elementSize.Y > 0 ? elementSize : throw new ArgumentException("Both element size dimensions must be positive", nameof(elementSize));
    private float _spacing;

    public float spacing {
        get => _spacing;
        set {
            _spacing = value;
            RebuildContents();
        }
    }

    public delegate void Drawer(ImGui gui, TData element, int index);

    public IReadOnlyList<TData> data {
        get => _data;
        set {
            _data = value ?? [];
            RebuildContents();
        }
    }

    private int CalculateFirstBlock() => Math.Max(0, MathUtils.Floor((scrollY - contents.initialPadding.top) / (elementSize.Y * BufferRows)));

    public override Vector2 scroll {
        get => base.scroll;
        set {
            base.scroll = value;
            int row = CalculateFirstBlock();

            if (row != firstVisibleBlock) {
                RebuildContents();
            }
        }
    }

    protected override void BuildContents(ImGui gui) {
        elementsPerRow = MathUtils.Floor((gui.width + _spacing) / (elementSize.X + _spacing));

        if (elementsPerRow < 1) {
            elementsPerRow = 1;
        }

        int rowCount = IntegerMath.CeilingDivide(_data.Count, elementsPerRow);
        firstVisibleBlock = CalculateFirstBlock();
        // Scroll up until there are maxRowsVisible, or to the top.
        int firstRow = Math.Max(0, Math.Min(firstVisibleBlock * BufferRows, rowCount - maxRowsVisible));
        int index = firstRow * elementsPerRow;

        if (index >= _data.Count) {
            // If _data is empty, there's nothing to draw. Make sure MeasureContent reports that, instead of the size of the most recent non-empty content.
            // This will remove the scroll bar when the search doesn't match anything.
            gui.lastContentRect = new Rect(gui.lastContentRect.X, gui.lastContentRect.Y, 0, 0);

            return;
        }

        int lastRow = firstRow + maxRowsVisible;
        using var manualPlacing = gui.EnterFixedPositioning(gui.width, rowCount * elementSize.Y, default);
        var offset = gui.statePosition.Position;
        float elementWidth = gui.width / elementsPerRow;
        Rect cell = new Rect(offset.X, offset.Y, elementWidth - _spacing, elementSize.Y);

        for (int row = firstRow; row < lastRow; row++) {
            cell.Y = row * (elementSize.Y + _spacing);

            for (int elem = 0; elem < elementsPerRow; elem++) {
                cell.X = elem * elementWidth;
                manualPlacing.SetManualRectRaw(cell);
                BuildElement(gui, _data[index], index);

                if (reorder != null) {
                    if (gui.DoListReordering(cell, cell, index, out int fromIndex)) {
                        reorder(fromIndex, index);
                    }
                }

                if (++index >= _data.Count) {
                    return;
                }
            }
        }
    }

    protected virtual void BuildElement(ImGui gui, TData element, int index) => drawer(gui, element, index);
}
