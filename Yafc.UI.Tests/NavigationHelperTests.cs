using SDL2;

namespace Yafc.UI.Tests;

// <summary>
// Test class for the NavigationHelper, meant to verify basic functionality and edge cases.
// It uses strings because NavigationHelper requires the Generic to be a class, so the slightly easier to write / use int wouldn't work.
// </summary>
public class NavigationHelperTests {
    private string? currentItem = null;

    private int numCalls = 0;

    // Used to verify that calls that shouldn't trigger a callback don't, without having to clutter the code as much!
    private bool allowCallback = true;
    // The string parameter is used to see what the "currently displayed" item is in the tests.
    private void SetItem(string s) {
        currentItem = s;
        numCalls++;
        Assert.True(allowCallback);
    }

    private readonly NavigationHelper<string> nav;

    // Constructor, as this would otherwise need to be done in each test, and due to the Action, it needs to be in the constructor or later.
    public NavigationHelperTests() => nav = new NavigationHelper<string>(SetItem);

    // Utility functions to simplify the testing methods
    private void NavigateToWithVerification(string newValue) {
        allowCallback = true;
        int preCalls = numCalls;
        nav.NavigateTo(newValue);
        Assert.Equal(currentItem, newValue); // Check that the expected value got set
        Assert.Equal(preCalls + 1, numCalls); // Check that only one callback occurred.
    }

    // Utility functions to simplify the testing methods
    private void NavigateToWithoutChange(string? newValue) {
        allowCallback = false;
        nav.NavigateTo(newValue);
    }

    private void NavigateBackWithChange(string expectedNewValue) {
        allowCallback = true;
        int preCalls = numCalls;
        nav.NavigateBack();
        Assert.Equal(currentItem, expectedNewValue); // Check that the expected value got set
        Assert.Equal(preCalls + 1, numCalls); // Check that only one callback occurred.
    }

    private void NavigateForwardWithChange(string expectedNewValue) {
        allowCallback = true;
        int preCalls = numCalls;
        nav.NavigateForward();
        Assert.Equal(currentItem, expectedNewValue); // Check that the expected value got set
        Assert.Equal(preCalls + 1, numCalls); // Check that only one callback occurred.
    }

    private void NavigateBackWithoutChange() {
        allowCallback = false;
        nav.NavigateBack();
    }

    private void NavigateForwardWithoutChange() {
        allowCallback = false;
        nav.NavigateForward();
    }

    // Check that the test is catching cases where it shouldn't run the callback.
    [Fact]
    public void VerifyFailOnCallbackWhenNoChangeExpected() {
        void act() => NavigateToWithoutChange("1");
        Assert.Throws<Xunit.Sdk.TrueException>(act);
        NavigateToWithVerification("2");
        Assert.Throws<Xunit.Sdk.TrueException>(NavigateBackWithoutChange);
        Assert.Throws<Xunit.Sdk.TrueException>(NavigateForwardWithoutChange);
    }

    [Fact]
    // Check that NavigationHelper handles navigation attempts when starting empty.
    public void NavigateWhenEmpty() {
        NavigateForwardWithoutChange();
        NavigateForwardWithoutChange();
    }

    [Fact]
    // Check that NavigationHelper handles navigation attempts when starting with a single item from the constructor and that item doesn't change.
    public void NavigateWhenSingleItemFromConstructor() {
        NavigationHelper<string> nav = new NavigationHelper<string>(SetItem, "1");
        Assert.Null(currentItem);
        NavigateBackWithoutChange();
        NavigateForwardWithoutChange();
    }

    [Fact]
    // Check that NavigationHelper handles navigation attempts when starting with a single item and that item doesn't change.
    public void NavigateWhenSingleItem() {
        Assert.Null(currentItem);
        NavigateToWithVerification("1");
        NavigateForwardWithoutChange();
        NavigateForwardWithoutChange();
    }

    [Fact]
    public void HandleNullEntry() {
        NavigateToWithoutChange(null);
        NavigateBackWithoutChange();
        NavigateForwardWithoutChange();
    }

    [Fact]
    public void TryNavigationBasic() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");
        NavigateBackWithChange("1");
        // An extra forward call should not generate any callback
        NavigateBackWithoutChange();

        NavigateForwardWithChange("2");
        // An extra forward call should not generate any callback
        NavigateForwardWithoutChange();
    }

    [Fact]
    public void ResetForwardHistory() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");

        NavigateBackWithChange("1");

        NavigateToWithVerification("3");
        NavigateForwardWithoutChange();
        NavigateBackWithChange("1");
    }

    [Fact]
    public void NoIdenticalEntriesSequentially() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");
        NavigateToWithoutChange("2");
        // If a duplicated entry exists, then it would go to 2 instead of 1.
        NavigateBackWithChange("1");
    }

    [Fact]
    public void DontResetHistoryForSequentialDuplicates() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");

        NavigateBackWithChange("1");
        // Try adding the same thing again:
        NavigateToWithoutChange("1");
        // Verify forward history remains
        NavigateForwardWithChange("2");
    }

    [Fact]
    public void AllowNonSequentialDuplicateEntries() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");

        NavigateBackWithChange("1");
        NavigateBackWithChange("2");
        NavigateBackWithChange("1");
        NavigateBackWithoutChange();

        NavigateForwardWithChange("2");
        NavigateForwardWithChange("1");
        NavigateForwardWithChange("2");
        NavigateForwardWithoutChange();
    }

    private void AddLotsOfItemsToList(int additions) {
        int i;
        for (i = 1; i <= additions; i++) {
            NavigateToWithVerification(i.ToString());
        }
        Assert.Equal(additions, numCalls); // Check that only one callback occurred.
        Assert.Equal(additions.ToString(), currentItem); // Check that the last item is the expected one.
        int latestCallCount;
        do { // Go back to start of the history.
            latestCallCount = numCalls;
            // Ignore verification here, keeping the code simpler.
            nav.NavigateBack();
        } while (latestCallCount != numCalls);
    }

    [Fact]
    public void VerifyMaxItems() {
        const int additions = 101;
        AddLotsOfItemsToList(additions);
        // Check that we are not at the original first item
        Assert.NotEqual("0", currentItem);
        // Check that we didn't double the amount of changes done, as that would indicate we were able to step back further then expected.
        Assert.True(additions >= (numCalls + 1) / 2); // The +1 is due to the last item only being shown once!
    }

    [Fact]
    public void VerifyCloseToMaxItems() {
        const int additions = 99;
        AddLotsOfItemsToList(additions);
        // Check that we are at the original first item
        Assert.Equal("1", currentItem);
        // Check that we didn't double the amount of changes done, as that would indicate we were able to step back further then expected.
        Assert.Equal(additions, (numCalls + 1) / 2); // The +1 is due to the last item only being shown once!
    }

    [Fact]
    public void TestKeyDownNavigation() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");

        // Check normal backspace
        SDL.SDL_Keysym key;
        key.scancode = SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE;
        key.mod = SDL.SDL_Keymod.KMOD_NONE;
        key.sym = SDL.SDL_Keycode.SDLK_BACKSPACE;
        key.unicode = 0;
        Assert.True(nav.HandleKeyDownNavigation(key));
        Assert.Equal("1", currentItem);

        // Check shift +  backspace for forward navigation
        key.scancode = SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE;
        key.mod = SDL.SDL_Keymod.KMOD_SHIFT;
        key.sym = SDL.SDL_Keycode.SDLK_BACKSPACE;
        key.unicode = 0;
        Assert.True(nav.HandleKeyDownNavigation(key));
        Assert.Equal("2", currentItem);

        // Check keypad backspace
        key.scancode = SDL.SDL_Scancode.SDL_SCANCODE_KP_BACKSPACE;
        key.mod = SDL.SDL_Keymod.KMOD_NONE;
        key.sym = SDL.SDL_Keycode.SDLK_KP_BACKSPACE;
        key.unicode = 0;
        Assert.True(nav.HandleKeyDownNavigation(key));
        Assert.Equal("1", currentItem);

        // Check shift + keypad backspace
        key.scancode = SDL.SDL_Scancode.SDL_SCANCODE_KP_BACKSPACE;
        key.mod = SDL.SDL_Keymod.KMOD_SHIFT;
        key.sym = SDL.SDL_Keycode.SDLK_KP_BACKSPACE;
        key.unicode = 0;
        Assert.True(nav.HandleKeyDownNavigation(key));
        Assert.Equal("2", currentItem);
    }

    // Check that no other keycode causes navigation
    [Fact]
    public void TestWrongKeysKeyDownNavigation() {
        NavigateToWithVerification("1");
        NavigateToWithVerification("2");
        NavigateToWithVerification("3");
        NavigateBackWithChange("2"); // Use the "middle" value to detect both forward and backward changes!
        allowCallback = false;

        SDL.SDL_Keysym key;
        key.scancode = SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE; // SDL_Keycode is ignored anyways in YAFC!
        key.mod = SDL.SDL_Keymod.KMOD_NONE;
        key.sym = SDL.SDL_Keycode.SDLK_BACKSPACE; // SDL_Keycode is ignored anyways in YAFC!
        key.unicode = 0;

        tryAllScancodesExceptCorrect(key);
        key.mod = SDL.SDL_Keymod.KMOD_SHIFT;
        tryAllScancodesExceptCorrect(key);

        void tryAllScancodesExceptCorrect(SDL.SDL_Keysym key) {
            foreach (SDL.SDL_Scancode scancode in Enum.GetValues<SDL.SDL_Scancode>()) {
                // Ignore the keycodes we *do* want to work.
                if (scancode is SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE or SDL.SDL_Scancode.SDL_SCANCODE_KP_BACKSPACE) {
                    continue;
                }
                key.scancode = scancode;
                Assert.False(nav.HandleKeyDownNavigation(key));
            }
        }
    }
}
