#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   Interface declaring events relevant to and implemented by all types descending from <see cref="View" />
/// </summary>
public interface ICommonViewEventsPublisher {
	/// <summary>
	///   Event raised immediately before the <see cref="View.Visible" /> property is about to be changed.
	/// </summary>
	/// <remarks>
	///   Typically only raised if there will actually be a change. If no change will be made because the value requested is equal to the current
	///   value, this event will not be raised.
	/// </remarks>
	event EventHandler<VisibleChangedEventArgs>? VisibleChanged;

    /// <summary>
    ///   Event raised immediately after the <see cref="View.Visible" /> property is has changed.
    /// </summary>
    event EventHandler<VisibleChangingEventArgs>? VisibleChanging;
}
