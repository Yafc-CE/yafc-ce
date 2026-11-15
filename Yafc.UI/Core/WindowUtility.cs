using System;
using System.Numerics;
using SDL2;
using Yafc.Core;

namespace Yafc.UI;

// Utility window is not hardware-accelerated and auto-size (and not resizable)
public abstract class WindowUtility(Padding padding) : Window(padding) {
    private int windowWidth, windowHeight;
    protected Window? parent;

    protected void Create(string? title, float width, Window? parent) {
        if (visible) {
            return;
        }

        this.parent = parent;
        parent?.ChildWindow = this;

        contentSize = new Vector2(width, 0);
        // Perform initial layout to dynamically calculate necessary window height.
        contentSize = rootGui.CalculateState(width);

        windowWidth = MathUtils.Round(UnitsToDips(contentSize.X));
        windowHeight = MathUtils.Round(UnitsToDips(contentSize.Y));

        var flags = SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS | SDL.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI;
        if (parent != null) {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR | SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
        }

        int display = parent == null ? 0 : SDL.SDL_GetWindowDisplayIndex(parent.window);
        window = SDL.SDL_CreateWindow(title,
            SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
            SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
            windowWidth,
            windowHeight,
            flags
        );

        surface = new UtilityWindowDrawingSurface(this);
        base.Create();
    }

    protected internal override void SizeChanged() {
        (surface as UtilityWindowDrawingSurface)?.OnSizeChanged();
        base.SizeChanged();
    }

    protected internal override void Close() {
        if (parent != null && !parent.closed) {
            parent.ChildWindow = null;
        }
        base.Close();
        parent = null;
    }

}

internal class UtilityWindowDrawingSurface : SoftwareDrawingSurface {
    public override Window window { get; }

    public override float pixelsPerUnit => window.pixelsPerUnit;

    public UtilityWindowDrawingSurface(WindowUtility window) : base(IntPtr.Zero) {
        this.window = window;
        InvalidateRenderer();
    }

    private void InvalidateRenderer() {
        if (renderer != IntPtr.Zero) {
            SDL.SDL_DestroyRenderer(renderer);
        }
        surface = SDL.SDL_GetWindowSurface(window.window);
        renderer = SDL.SDL_CreateSoftwareRenderer(surface);
        _ = SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
    }

    public void OnSizeChanged() => InvalidateRenderer();

    public override void Dispose() {
        base.Dispose();
        surface = IntPtr.Zero;
    }

    public override void Present() {
        base.Present();

        if (surface != IntPtr.Zero) {
            _ = SDL.SDL_UpdateWindowSurface(window.window);
        }
    }
}
