#nullable enable
namespace Terminal.Gui;

/// <summary>
///   An interface providing basic standardized support for cooperative cancellation.
/// </summary>
/// <remarks>
///   Notes to implementers:
///   <para>
///     Types implementing this interface should typically have a non-static private readonly field of type
///     <see cref="CancellationTokenSource" /> to manage cancellation.
///   </para>
///   <para>
///     This interface declares <seealso cref="IDisposable" />, which should be used to dispose of the <see cref="CancellationTokenSource" />.
///   </para>
/// </remarks>
[UsedImplicitly (ImplicitUseTargetFlags.WithMembers)]
public interface ISupportsCancellation : IDisposable {
    /// <summary>
    ///   Gets the <see cref="System.Threading.CancellationToken" /> associated with this instance.
    /// </summary>
    /// <remarks>
    ///   Should typically be provided by a <see cref="CancellationTokenSource" /> owned by the implementing type.
    /// </remarks>
    CancellationToken CancellationToken { get; }

    /// <inheritdoc cref="CancellationToken.IsCancellationRequested" />
    public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

    /// <summary>
    ///   Requests cancellation for this instance of <see cref="ISupportsCancellation" />.
    /// </summary>
    void RequestCancellation ();

    /// <summary>
    ///   Requests cancellation for this instance of <see cref="ISupportsCancellation" /> and provides the associated
    ///   <see cref="CancellationToken" /> as an output parameter.
    /// </summary>
    void RequestCancellation (out CancellationToken cancellationToken);
}
