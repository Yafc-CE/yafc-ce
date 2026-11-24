using SDL2;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

/// <summary>
/// Copies recipe rows, including their nested tables, to and from the OS clipboard.
/// </summary>
internal static class RecipeRowClipboard {
    /// <summary>
    /// Put a copy of <paramref name="row"/> on the clipboard, without its row markers and built building counts, or show the user why this isn't possible.
    /// </summary>
    public static void Copy(RecipeRow row) {
        ErrorCollector collector = new();
        RecipeRow? copy = row.CopyAsUnbuilt(collector);

        if (copy == null || collector.severity > ErrorSeverity.None) {
            ErrorListPanel.Show(collector);
        }
        else {
            _ = SetClipboard(copy);
        }
    }

    /// <summary>
    /// Put <paramref name="row"/> on the clipboard, including its row markers and built building counts, so the caller can remove it from its table. If this
    /// isn't possible, show the user why.
    /// </summary>
    /// <returns><see langword="true"/> if the row is on the clipboard, or <see langword="false"/> if it isn't and must not be removed.</returns>
    public static bool Cut(RecipeRow row) => SetClipboard(row);

    /// <summary>
    /// Returns <see langword="true"/> if there is a row on the clipboard. This can be slow, because it may have to request the text from another
    /// application, so call it when opening a menu and not while drawing it.
    /// </summary>
    public static bool CanPaste() => ImGuiUtils.HasClipboardText() && ShareString.Contains<RecipeRow>(SDL.SDL_GetClipboardText());

    /// <summary>
    /// Add the row on the clipboard to the end of <paramref name="table"/>, or show the user why this isn't possible.
    /// </summary>
    public static void Paste(ProductionTable table) {
        ErrorCollector collector = new();
        table.PasteRecipeRow(SDL.SDL_GetClipboardText(), collector);

        if (collector.severity > ErrorSeverity.None) {
            ErrorListPanel.Show(collector);
        }
    }

    private static bool SetClipboard(RecipeRow row) {
        if (SDL.SDL_SetClipboardText(ShareString.Encode(row)) == 0) {
            return true;
        }

        MessageBox.Show(LSs.ProductionTableCopyRowFailed, SDL.SDL_GetError(), LSs.Ok);
        return false;
    }
}
