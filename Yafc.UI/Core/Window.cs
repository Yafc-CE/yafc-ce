using System;
using System.Numerics;
using SDL2;
using Serilog;
using Yafc.Core;

namespace Yafc.UI;

public abstract class Window : IDisposable {
    private static readonly ILogger logger = Logging.GetLogger<Window>();

    public readonly ImGui rootGui;
    internal IntPtr window;
    /// <summary>Window icon, singleton so it is reused for all windows</summary>
    internal static IntPtr icon;
    internal Vector2 contentSize;
    internal uint id;
    internal bool repaintRequired = true;
    internal bool visible;
    internal bool closed;
    internal long nextRepaintTime = long.MaxValue;
    public float DisplayScale { get; private set; } = 1f;
    internal float pixelsPerUnit { get => UnitsToDips(DisplayScale); }
    public virtual SchemeColor backgroundColor => SchemeColor.Background;

    private Tooltip? tooltip;
    private SimpleTooltip? simpleTooltip;
    protected DropDownPanel? dropDown;
    private SimpleDropDown? simpleDropDown;
    private ImGui.DragOverlay? draggingOverlay;
    private bool disposedValue;

    public DrawingSurface? surface { get; protected set; }

    public int displayIndex => SDL.SDL_GetWindowDisplayIndex(window);
    public int repaintCount { get; private set; }

    public Vector2 size => contentSize;

    public virtual bool preventQuit => false;
    internal Window(Padding padding) => rootGui = new ImGui(Build, padding);

    public event OnFocusLost? onFocusLost;

    public Window? ChildWindow {
        get;
        set {
            // Because there can only be a single child window, always close the previous one if it's still open first.
            if (value != null) {
                field?.Close();
            }
            field = value;
        }
    }

    internal void Create() {
        if (surface is null) {
            throw new InvalidOperationException($"surface must be set by a derived class before calling {nameof(Create)}.");
        }

        UpdateDisplayScale();
        SDL.SDL_SetWindowIcon(window, GetIcon());

        _ = SDL.SDL_SetRenderDrawBlendMode(surface.renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        id = SDL.SDL_GetWindowID(window);
        Ui.CloseWindowOfType(GetType());
        Ui.RegisterWindow(id, this);
        visible = true;
    }

    /// <summary>Load, if needed, the window icon and return its SDL_Surface pointer, or IntPtr.Zero if not loaded due to errors</summary>
    internal static IntPtr GetIcon() {
        if (icon == IntPtr.Zero) {
            icon = SDL_image.IMG_Load("image.ico");

            if (icon == IntPtr.Zero) {
                string error = SDL.SDL_GetError();
                logger.Warning("Failed to load application icon: {error}", error);
            }
        }

        return icon;
    }
    private void UpdateDisplayScale() {
        if (surface is null) {
            throw new InvalidOperationException($"surface must be set by a derived class before calling {nameof(UpdateDisplayScale)}.");
        }

        SDL.SDL_GetWindowSize(window, out int windowWidth, out int _);
        SDL.SDL_GetRendererOutputSize(surface.renderer, out int renderWidth, out int _);
        var value = (float)renderWidth / windowWidth;

        if (value != DisplayScale) {
            DisplayScale = value;
            rootGui.MarkEverythingForRebuild();
            Repaint();
        }
    }

    public void UpdateSize() {
        SDL.SDL_GetWindowSize(window, out int windowWidth, out int windowHeight);
        contentSize = new Vector2(DipsToUnits(windowWidth), DipsToUnits(windowHeight));

        // Force any changes to sizes to be applied when calling CalculateState.
        Rebuild();
        // Perform initial layout to dynamically calculate necessary window height.
        contentSize = rootGui.CalculateState(DipsToUnits(windowWidth));

        windowHeight = MathUtils.Round(UnitsToDips(contentSize.Y));
        SDL.SDL_SetWindowSize(window, windowWidth, windowHeight);

        // Now that the window size may have changed, make sure everything is re-rendered with the new size and other changes.
        rootGui.MarkEverythingForRebuild();
    }

    internal static float UnitsToDips(float units) => units * MathUtils.Round(96f / 6.8f);

    internal static float DipsToUnits(float dips) => dips / MathUtils.Round(96f / 6.8f);

    protected internal void SetWindowTitle(string value) => SDL.SDL_SetWindowTitle(window, value);

    protected internal virtual void WindowResize() { }

    protected internal virtual void SizeChanged() {
        UpdateDisplayScale();
    }

    protected virtual void OnRepaint() { }

    internal void Render() {
        if (surface is null) {
            throw new InvalidOperationException($"surface must be set by a derived class before calling {nameof(Render)}.");
        }

        if (!repaintRequired && nextRepaintTime > Ui.time) {
            return;
        }

        if (nextRepaintTime <= Ui.time) {
            nextRepaintTime = long.MaxValue;
        }

        OnRepaint();
        repaintRequired = false;

        if (rootGui.IsRebuildRequired()) {
            _ = rootGui.CalculateState(surface, size.X);
        }

        MainRender();
        surface.Present();
    }

    protected virtual void MainRender() {
        if (surface is null) {
            throw new InvalidOperationException($"surface must be set by a derived class before calling {nameof(MainRender)}.");
        }

        var bgColor = backgroundColor.ToSdlColor();
        _ = SDL.SDL_SetRenderDrawColor(surface.renderer, bgColor.r, bgColor.g, bgColor.b, bgColor.a);
        Rect fullRect = new Rect(default, contentSize);
        repaintCount++;
        surface.Clear(new SDL.SDL_Rect {
            x = MathUtils.Round(fullRect.X * surface.pixelsPerUnit),
            y = MathUtils.Round(fullRect.Y * surface.pixelsPerUnit),
            w = MathUtils.Round(fullRect.Width * surface.pixelsPerUnit),
            h = MathUtils.Round(fullRect.Height * surface.pixelsPerUnit),
        });
        rootGui.InternalPresent(surface, fullRect, fullRect);
    }

    public IPanel HitTest(Vector2 position) => rootGui.HitTest(position);

    public void Rebuild() => rootGui.Rebuild();

    public void Repaint() {
        if (closed) {
            return;
        }

        if (!Ui.IsMainThread()) {
            throw new NotSupportedException("This should be called from the main thread");
        }

        repaintRequired = true;
    }

    protected internal virtual void Close() {
        visible = false;
        closed = true;
        ChildWindow?.Close();
        surface?.Dispose();
        SDL.SDL_DestroyWindow(window);
        Dispose();
        window = IntPtr.Zero;
        Ui.UnregisterWindow(this);
    }

    private void Focus() {
        if (window != IntPtr.Zero) {
            SDL.SDL_RaiseWindow(window);
            SDL.SDL_RestoreWindow(window);
            _ = SDL.SDL_SetWindowInputFocus(window);
        }
    }

    public virtual void FocusLost() => onFocusLost?.Invoke();

    internal void FocusGained() => ChildWindow?.Focus();

    public virtual void Minimized() { }

    public void SetNextRepaint(long nextRepaintTime) {
        if (this.nextRepaintTime > nextRepaintTime) {
            this.nextRepaintTime = nextRepaintTime;
        }
    }

    internal virtual void DarkModeChanged() { }

    public void ShowTooltip(Tooltip tooltip) {
        this.tooltip = tooltip;
        Rebuild();
    }

    public void HideTooltip() {
        tooltip = null;
        Rebuild();
    }

    public void ShowTooltip(ImGui targetGui, Rect target, GuiBuilder builder, float width = 20f) {
        simpleTooltip ??= new SimpleTooltip();
        simpleTooltip.Show(builder, targetGui, target, width);
        ShowTooltip(simpleTooltip);
    }

    public void ShowDropDown(DropDownPanel dropDown) {
        this.dropDown = dropDown;
        Rebuild();
    }

    public void ShowDropDown(ImGui targetGui, Rect target, GuiBuilder builder, Padding padding, float width = 20f) {
        simpleDropDown ??= new SimpleDropDown();
        simpleDropDown.SetPadding(padding);
        simpleDropDown.SetFocus(targetGui, target, builder, width);
        ShowDropDown(simpleDropDown);
    }

    private void Build(ImGui gui) {
        if (closed) {
            return;
        }

        BuildContents(gui);
        if (dropDown != null) {
            dropDown.Build(gui);
            if (!dropDown.active) {
                dropDown = null;
            }
        }

        draggingOverlay?.Build(gui);

        if (tooltip != null) {
            tooltip.Build(gui);
            if (!tooltip.active) {
                tooltip = null;
            }
        }
    }

    protected abstract void BuildContents(ImGui gui);

    internal ImGui.DragOverlay GetDragOverlay() => draggingOverlay ??= new ImGui.DragOverlay();
    protected internal virtual void WindowMaximized() { }
    protected internal virtual void WindowRestored() { }

    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            if (disposing) {
                rootGui.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public delegate void OnFocusLost();
