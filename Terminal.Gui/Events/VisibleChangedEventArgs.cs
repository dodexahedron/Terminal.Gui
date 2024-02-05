#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   Event arguments intended for use with the <see cref="ICommonViewEventsPublisher.VisibleChanged" /> event.
/// </summary>
/// <remarks>
///   Creates a new instance of a <see cref="VisibleChangedEventArgs" /> object, intended for use with the
///   <see cref="ICommonViewEventsPublisher.VisibleChanged" /> event.
/// </remarks>
/// <param name="changedView">
///   The instance of an object of a type assignable to <see cref="View" /> on which the change was made.
/// </param>
/// <param name="desiredChangedFrom">The value of the desired visibility before the change was committed.</param>
/// <param name="desiredChangedTo">The value of the desired visibility after the change was committed.</param>
/// <param name="effectiveChangedFrom">The value of the effective visibility before the change was committed.</param>
/// <param name="effectiveChangedTo">The value of the effective visibility after the change was committed.</param>
[method: SetsRequiredMembers]
public class VisibleChangedEventArgs (View changedView, bool desiredChangedFrom, bool desiredChangedTo, bool effectiveChangedFrom, bool effectiveChangedTo) : EventArgs {
    /// <summary>
    ///   Gets a reference to the instance of a <see cref="View" /> on which the change was made.
    /// </summary>
    public required View ChangedView { get; init; } = changedView;

    /// <summary>Gets the value of the desired visibility before the change was committed.</summary>
    public required bool DesiredChangedFrom { get; init; } = desiredChangedFrom;

    /// <summary>Gets the value of the desired visibility after the change was committed.</summary>
    public required bool DesiredChangedTo { get; init; } = desiredChangedTo;

    /// <summary>Gets the value of the effective visibility before the change was committed.</summary>
    public required bool EffectiveChangedFrom { get; init; } = effectiveChangedFrom;

    /// <summary>Gets the value of the effective visibility after the change was committed.</summary>
    public required bool EffectiveChangedTo { get; init; } = effectiveChangedTo;
}
