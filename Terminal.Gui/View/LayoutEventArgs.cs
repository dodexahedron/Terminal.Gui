#nullable enable
namespace Terminal.Gui;

/// <summary>
/// Event arguments for the <see cref="View.LayoutComplete"/> event.
/// </summary>
public class LayoutEventArgs : EventArgs {
    /// <summary>
    /// The view-relative bounds of the <see cref="View"/> before it was laid out.
    /// </summary>
    public Rect OldBounds { get; set; }
}