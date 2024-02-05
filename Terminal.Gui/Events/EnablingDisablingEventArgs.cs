#nullable enable
using Terminal;

namespace Terminal.Gui.Events;

/// <summary>
///   Event arguments intended for use with the <see cref="ISupportsEnableDisable.Enabling" /> and
///   <see cref="ISupportsEnableDisable.Disabling" /> events of the <see cref="ISupportsEnableDisable" /> <see langword="interface" />.
/// </summary>
/// <remarks>
///   <para>
///     Note that all state values set are snapshots of their associated values as of the time that the event was raised and this
///     <see cref="EnablingDisablingEventArgs" /> instance was initialized.
///     <para />
///     If actual current values on <see cref="Target" /> are required, that must be handled by the subscriber's implementation.
///   </para>
/// </remarks>
[UsedImplicitly (ImplicitUseTargetFlags.WithMembers)]
[MustDisposeResource (false)]
public class EnablingDisablingEventArgs : CancelableEventArgs {
	/// <summary>
	///   Creates a new instance of <see cref="EnablingDisablingEventArgs" /> intended for use with the
	///   <see cref="ISupportsEnableDisable.Enabling" /> and <see cref="ISupportsEnableDisable.Disabling" /> events of the
	///   <see cref="ISupportsEnableDisable" /> <see langword="interface" />, with the supplied values.
	/// </summary>
	/// <remarks>
	///   This constructor overload sets all required properties. It is not necessary to set them in an initializer, when using this overload.
	/// </remarks>
	/// <param name="target">
	///   A reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
	/// </param>
	/// <param name="oldDesiredState">
	///   The current desired <see cref="EnableState" /> value before the requested change would be executed.
	/// </param>
	/// <param name="newDesiredState">The <see cref="EnableState" /> requested by this event.</param>
	/// <param name="oldEffectiveState">
	///   The current effective <see cref="EnableState" /> value before the requested change would be executed.
	/// </param>
	/// <param name="predictedEffectiveState">
	///   The effective <see cref="EnableState" /> value that is predicted to result after this change would be executed.
	/// </param>
	/// <param name="cancellationToken"></param>
	[SetsRequiredMembers]
	[MustDisposeResource (false)]
	public EnablingDisablingEventArgs (ISupportsEnableDisable target, EnableState oldDesiredState, EnableState newDesiredState, EnableState oldEffectiveState, EnableState predictedEffectiveState, CancellationToken cancellationToken) : base (cancellationToken)
	{
		Target = target;
		OldDesiredState = oldDesiredState;
		NewDesiredState = newDesiredState;
		OldEffectiveState = oldEffectiveState;
		PredictedEffectiveState = predictedEffectiveState;
	}

	/// <summary>
	///   Creates a new instance of <see cref="EnablingDisablingEventArgs" /> intended for use with the
	///   <see cref="ISupportsEnableDisable.Enabling" /> and <see cref="ISupportsEnableDisable.Disabling" /> events of the
	///   <see cref="ISupportsEnableDisable" /> <see langword="interface" />.
	/// </summary>
	/// <remarks>
	///   All <see cref="EnableState" /> properties must be set in an initializer, when using this constructor overload.
	/// </remarks>
	/// <param name="target">
	///   A reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
	/// </param>
	[MustDisposeResource (false)]
	public EnablingDisablingEventArgs (ISupportsEnableDisable target) : this (target, CancellationToken.None) { }

	/// <summary>
	///   Creates a new instance of <see cref="EnablingDisablingEventArgs" /> intended for use with the
	///   <see cref="ISupportsEnableDisable.Enabling" /> and <see cref="ISupportsEnableDisable.Disabling" /> events of the
	///   <see cref="ISupportsEnableDisable" /> <see langword="interface" />.
	/// </summary>
	/// <remarks>
	///   All <see cref="EnableState" /> properties must be set in an initializer, when using this constructor overload.
	/// </remarks>
	/// <param name="target">
	///   A reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
	/// </param>
	/// <param name="cancellationToken">The <see cref="CancellationToken" /> to associate with this instance.</param>
	[MustDisposeResource (false)]
	public EnablingDisablingEventArgs (ISupportsEnableDisable target, CancellationToken cancellationToken) : base (cancellationToken) { Target = target; }

	/// <summary>Gets the <see cref="EnableState" /> requested by this event.</summary>
	public required EnableState NewDesiredState { get; init; }

	/// <summary>
	///   Gets the current desired <see cref="EnableState" /> value before the requested change would be executed.
	/// </summary>
	public required EnableState OldDesiredState { get; init; }

	/// <summary>
	///   Gets the current effective <see cref="EnableState" /> value before the requested change would be executed.
	/// </summary>
	public required EnableState OldEffectiveState { get; init; }

	/// <summary>
	///   Gets the effective <see cref="EnableState" /> value that is predicted to result after this change would be executed.
	/// </summary>
	/// <remarks>
	///   This value is only guaranteed to be accurate at the time the event is initially raised. Subscribers may alter state.
	/// </remarks>
	public required EnableState PredictedEffectiveState { get; init; }

	/// <summary>
	///   Gets a reference to the instance of the <see cref="ISupportsEnableDisable" /> that is the intended target of the change.
	/// </summary>
	public required ISupportsEnableDisable Target { get; init; }

	/// <inheritdoc />
	~EnablingDisablingEventArgs () { Dispose (false); }
}
