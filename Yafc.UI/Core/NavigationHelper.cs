using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI;


// INavigationTarget exists to support Mouse 4 and 5 (AKA X1 and X2) based navigation of cases where defaultKeyboardFocus is set to an object that implements this interface.
public interface INavigationTarget {

    NavigationHelper navigationHelper { get; }
}

/// <summary>
/// This class is meant to separate and handle most Keyboard and mouse button based Navigation.
/// Generally (Shift) Backspace and Mouse buttons 4 and 5 (X1 and X2 respectively) 
/// </summary>
public sealed class NavigationHelper() {
    private const int MaxHistory = 100;
    private const int ItemsToRemoveWhenMaxHit = 20;

    // Store the items that should be navigated to/from via the changeDelegate. 
    //  The type is using dynamic, as it's "unknown" for some users of this class.
    //  And can't be set via generic because we don't want InputSystem to care about the exact type when getting instances of this class via INavigationTarget.
    // TODO: This shold probably be a LinkedList instead, as it cleanly allows removing stuff from the start *or end*, as well as allowing a LinkedListNode type that has Next and Previous!
    //  It would remove the mess of offsets in the addHistory function! 
    private readonly List<dynamic> history = [];

    public required Action<dynamic> changeDelegate;

    private bool isNavigating = false;
    private int currentIndex = -1;

    public void addHistory(dynamic newItem) {
        // Early out to avoid infinite loops
        if (isNavigating) {
            return;
        }

        // If we add a new item to the history, remove any "forward looking" history.
        if (history.Count > currentIndex + 1) {
            history.RemoveRange(currentIndex + 1, history.Count - (currentIndex + 1));
        }

        history.Add(newItem);
        if (history.Count > MaxHistory) {
            history.RemoveRange(0, ItemsToRemoveWhenMaxHit);
        }
        currentIndex = history.Count - 1;
    }


    /// <summary>
    /// Check KeyDown events for navigation inputs. 
    /// Allow for using backspace to backtrack to whatever was viewed previously.
    /// And shift + backspace to go "forward" from previous backtracking.
    /// </summary>
    public bool HandkeKeyDownNavigation(SDL.SDL_Keysym key) {
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

    // TODO: Should this use InputSystem.Instance.currentKeyboardFocus instead? That would (slightly) change the behaviour however!
    public static bool HandleMouseButtonNavigation(int button, IKeyboardFocus? defaultKeyboardFocus) {
        // Mouse button 4 (X1) is commonly used for backwards navigation.
        // If this button is used, we do an early out to avoid treating it as a "normal" mouse click.
        if (button == SDL.SDL_BUTTON_X1) {
            (defaultKeyboardFocus as INavigationTarget)?.navigationHelper.NavigateBack();
            return true;
        }
        // Mouse button 5 (X2) is commonly used for forwards navigation.
        // If this button is used, we do an early out to avoid treating it as a "normal" mouse click.
        if (button == SDL.SDL_BUTTON_X2) {
            (defaultKeyboardFocus as INavigationTarget)?.navigationHelper.NavigateForward();
            return true;
        }
        return false;
    }

    public void NavigateBack() {
        isNavigating = true;
        // Check that currentIndex is not pointing to the first item in the list.
        if (currentIndex > 0) {
            changeDelegate(history[--currentIndex]);
        }
        isNavigating = false;
    }

    public void NavigateForward() {
        isNavigating = true;
        // Check that the item after currentIndex exists.
        if (currentIndex < history.Count - 1) {
            changeDelegate(history[++currentIndex]);
        }
        isNavigating = false;
    }
}
