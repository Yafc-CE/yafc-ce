using System;
using System.Threading.Tasks;
using Yafc.UI;

namespace Yafc;

public class MessageBox : PseudoScreenWithResult<bool> {
    private readonly string title;
    private readonly string message;
    private readonly string yes;
    private readonly string? no;

    private MessageBox(string title, string message, string yes, string? no) : base(30f) {
        this.title = title;
        this.message = message;
        this.yes = yes;
        this.no = no;
    }

    public static void Show(Action<bool>? result, string title, string message, string yes, string? no) {
        MessageBox instance = new MessageBox(title, message, yes, no) { completionCallback = result };
        MainScreen.Instance.ShowPseudoScreen(instance);
    }

    public static void Show(string title, string message, string yes) => Show(null, title, message, yes, null);

    /// <summary>
    /// Display a dialog with yes/no/cancel behavior. <see langword="await">ing the <see cref="Task"> yields <see langword="true"/> if the user
    /// selected the '<paramref name="yes"/>' option, <see langword="false"/> if the user selected the '<paramref name="no"/>' option, or
    /// <see langword="null"/> if the user pressed Escape or otherwise cancelled the dialog.
    /// </summary>
    public static Task<bool?> Show(string title, string message, string yes, string? no) {
        TaskCompletionSource<bool?> tcs = new();
        MessageBox instance = new MessageBox(title, message, yes, no) {
            completionCallback = a => tcs.TrySetResult(a),
            cleanupCallback = () => tcs.TrySetResult(null)
        };
        MainScreen.Instance.ShowPseudoScreen(instance);
        return tcs.Task;
    }

    public override void Build(ImGui gui) {
        BuildHeader(gui, title);
        if (message != null) {
            gui.BuildText(message, TextBlockDisplayStyle.WrappedText);
        }

        gui.AllocateSpacing(2f);
        using (gui.EnterRow(allocator: RectAllocator.RightRow)) {
            if (gui.BuildButton(yes)) {
                CloseWithResult(true);
            }

            if (no != null && gui.BuildButton(no, SchemeColor.Grey)) {
                CloseWithResult(false);
            }
        }
    }

    protected override void ReturnPressed() => CloseWithResult(true);
}
