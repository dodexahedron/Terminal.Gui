#nullable enable
namespace Terminal.Gui.Input;

/// <summary>
/// Standard event arguments for keyboard input events.
/// </summary>
/// <param name="Key">A <see cref="Key" /> instance associated with this event.</param>
/// <param name="Handled">A mutable boolean which subscribed event handlers can set to indicate that the event has been handled and that further processing may no longer be needed.</param>
public record KeyEventArgs (Key Key, bool Handled = false) : IEqualityOperators<KeyEventArgs, KeyEventArgs, bool>, IEqualityOperators<KeyEventArgs, Key, bool>
{
    /// <summary>
    /// Gets or sets a value indicating that the event has been handled and that further processing may no longer be needed.
    /// </summary>
    public bool Handled { get; set; } = Handled;

    /// <inheritdoc />
    public static bool operator == (KeyEventArgs? left, Key? right) => left?.Key == right;

    /// <inheritdoc />
    public static bool operator != (KeyEventArgs? left, Key? right) => left?.Key != right;
}
