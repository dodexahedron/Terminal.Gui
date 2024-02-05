#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   Event arguments intended for use with the <see cref="ICommonViewEventsPublisher.VisibleChanging" /> event.
/// </summary>
/// <remarks>
///   Creates a new instance of a <see cref="VisibleChangingEventArgs{T}" /> object, intended for use with the
///   <see cref="ICommonViewEventsPublisher.VisibleChanging" /> event.
/// </remarks>
/// <param name="viewToChange">
///   The instance of an object of a type assignable to <see cref="View" /> on which the change will be made.
/// </param>
/// <param name="desiredChangingFrom">The value of the desired visibility before the change was committed.</param>
/// <param name="desiredChangingTo">The value of the desired visibility after the change was committed.</param>
/// <param name="effectiveChangingFrom">The value of the effective visibility before the change was committed.</param>
/// <param name="predictedEffectiveChangingTo">The value of the effective visibility after the change was committed.</param>
[method: SetsRequiredMembers]
public class VisibleChangingEventArgs (View viewToChange, bool desiredChangingFrom, bool desiredChangingTo, bool effectiveChangingFrom, bool predictedEffectiveChangingTo) : EventArgs {
    /// <summary>
    ///   Gets or sets a value indicating if cancellation has been requested for this event.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>Gets the value of the desired visibility before the change is committed.</summary>
    public required bool DesiredChangingFrom { get; init; } = desiredChangingFrom;

    /// <summary>Gets the value of the desired visibility after the change is committed.</summary>
    public required bool DesiredChangingTo { get; init; } = desiredChangingTo;

    /// <summary>Gets the value of the effective visibility before the change is committed.</summary>
    public required bool EffectiveChangingFrom { get; init; } = effectiveChangingFrom;

    /// <summary>
    ///   Gets the value of the predicted effective visibility after the change is committed.
    /// </summary>
    public required bool PredictedEffectiveChangingTo { get; init; } = predictedEffectiveChangingTo;

    /// <summary>
    ///   Gets a reference to the instance of a <see cref="View" /> on which the change is being made.
    /// </summary>
    public View ViewToChange { get; init; } = viewToChange;
}
