using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI;

// INavigationTarget exists to support Mouse 4 and 5 (AKA X1 and X2) based navigation of cases where defaultKeyboardFocus is set to an object that implements this interface.
public interface INavigationTarget {
    INavigationHelper navigationHelper { get; }
}

// Extra interface layer to allow access to the Navigation functions without needing to care about the Generic type.
public interface INavigationHelper {
    void NavigateBack();
}

/// <summary>
/// This class is meant to separate and handle most Keyboard and mouse button based Navigation.
/// Generally (Shift) Backspace and Mouse buttons 4 and 5 (X1 and X2 respectively)
/// </summary>
public sealed class NavigationHelper<T> : INavigationHelper where T : class {
    private const int MaxHistory = 100;
    private const int ItemsToRemoveWhenMaxHit = 20;

    // Store the items that should be navigated to/from via the changeDelegate. 
    private readonly List<T> history = [];

    private readonly Action<T> changeDelegate;
    private int currentIndex = -1;

    public NavigationHelper(Action<T> changeDelegate, T? firstItem = null) {
        this.changeDelegate = changeDelegate;
        if (firstItem != null) {
            history.Add(firstItem);
            currentIndex = 0;
        }
    }

    public void NavigateTo(T newItem) {
        if (newItem == null) {
            return;
        }

        // If we add a new item to the history, remove any "forward looking" history.
        if (history.Count > currentIndex + 1) {
            history.RemoveRange(currentIndex + 1, history.Count - (currentIndex + 1));
        }

        history.Add(newItem);

        // If we add a new item to the history, remove any "forward looking" history.
        if (history.Count > currentIndex + 1) {
            history.RemoveRange(currentIndex + 1, history.Count - (currentIndex + 1));
        }

        if (history.Count > MaxHistory) {
            history.RemoveRange(0, ItemsToRemoveWhenMaxHit);
        }
        currentIndex = history.Count - 1;
        changeDelegate(newItem);
    }

    /// <summary>
    /// Check KeyDown events for navigation inputs.
    /// Allow for using backspace to backtrack to whatever was viewed previously.
    /// And shift + backspace to go "forward" from previous backtracking.
    /// </summary>
    public bool HandleKeyDownNavigation(SDL.SDL_Keysym key) {
        if (key.scancode is SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE or SDL.SDL_Scancode.SDL_SCANCODE_KP_BACKSPACE) {
            if ((key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0) {
                NavigateForward();
            }
            else {
                NavigateBack();
            }
            return true;
        }
        return false;
    }

    public void NavigateBack() {
        // Check that currentIndex is not pointing to the first item in the list.
        if (currentIndex > 0) {
            changeDelegate(history[--currentIndex]);
        }
    }

    public void NavigateForward() {
        // Check that the item after currentIndex exists.
        if (currentIndex < history.Count - 1) {
            changeDelegate(history[++currentIndex]);
        }
    }
}
