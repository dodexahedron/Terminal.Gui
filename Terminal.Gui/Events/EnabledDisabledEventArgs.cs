#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   Event arguments intended for use with the events defined by <see cref="ISupportsEnableDisable" />.
/// </summary>
/// <remarks>
///   Intended for use with events defined by <see cref="ISupportsEnableDisable" />.
///   <para />
///   All parameters required at initialization.
/// </remarks>
/// <param name="target">
///   A reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
/// </param>
/// <param name="oldDesiredState">
///   The previous <see cref="EnableState" /> of the associated property on <see cref="Target" /> before the change was made.
/// </param>
/// <param name="newDesiredState">
///   The requested <see cref="EnableState" /> of the associated property on <see cref="Target" /> for this event.
/// </param>
/// <param name="oldEffectiveState">
///   The effective <see cref="EnableState" /> of the associated property on <see cref="Target" /> before the change was made.
/// </param>
/// <param name="newEffectiveState">
///   The new effective <see cref="EnableState" /> of the associated property on <see cref="Target" /> after the change was made.
/// </param>
[method: SetsRequiredMembers]
public class EnabledDisabledEventArgs (ISupportsEnableDisable target, EnableState oldDesiredState, EnableState newDesiredState, EnableState oldEffectiveState, EnableState newEffectiveState) : EventArgs {
	/// <summary>
	///   Gets the requested <see cref="EnableState" /> of the associated property on <see cref="Target" /> for this event.
	/// </summary>
	/// <remarks>See containing type and associated events for details.</remarks>
	public EnableState NewDesiredState { get; init; } = newDesiredState;

	/// <summary>
	///   Gets the new effective <see cref="EnableState" /> of the associated property on <see cref="Target" /> after the change was made.
	/// </summary>
	/// <remarks>See containing type and associated events for details.</remarks>
	public EnableState NewEffectiveState { get; init; } = newEffectiveState;

	/// <summary>
	///   Gets the previous <see cref="EnableState" /> of the associated property on <see cref="Target" /> before the change was made.
	/// </summary>
	/// <remarks>See containing type and associated events for details.</remarks>
	public EnableState OldDesiredState { get; init; } = oldDesiredState;

	/// <summary>
	///   Gets the effective <see cref="EnableState" /> of the associated property on <see cref="Target" /> before the change was made.
	/// </summary>
	/// <remarks>See containing type and associated events for details.</remarks>
	public EnableState OldEffectiveState { get; init; } = oldEffectiveState;

	/// <summary>
	///   Gets a reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
	/// </summary>
	public required ISupportsEnableDisable Target { get; init; } = target;
}
