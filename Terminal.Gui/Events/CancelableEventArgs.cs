#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   Provides a default implementation of the <see cref="ISupportsCancellation" /> interface.
/// </summary>
[UsedImplicitly (ImplicitUseTargetFlags.WithMembers)]
[MustDisposeResource (false)]
public abstract class CancelableEventArgs : EventArgs, ISupportsCancellation {
	/// <summary>
	///   The <see cref="CancellationTokenSource" /> that owns the <see cref="System.Threading.CancellationToken" /> for this instance and
	///   arbitrates cancellation
	/// </summary>
	protected readonly CancellationTokenSource Cts;

	private bool _isDisposed;

	/// <summary>
	///   Protected constructor for the abstract <see cref="CancelableEventArgs" /> class, which delegates to the
	///   <see cref="CancelableEventArgs(System.Threading.CancellationToken)" /> overload, passing
	///   <see cref="CancellationToken.None" />.
	/// </summary>
	[MustDisposeResource (false)]
	protected CancelableEventArgs () : this (CancellationToken.None) { }

	/// <summary>
	///   Protected constructor for the abstract <see cref="CancelableEventArgs" /> class, using <paramref name="cancellationToken" /> to create a
	///   linked token source.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <remarks>
	///   If <paramref name="cancellationToken" /> is <see cref="CancellationToken.None" />, creates a new, independent
	///   <see cref="CancellationTokenSource" />.
	///   <para />
	///   For all other values, creates a linked <see cref="CancellationTokenSource" /> based on that token
	/// </remarks>
	[MustDisposeResource (false)]
	protected CancelableEventArgs (CancellationToken cancellationToken) { Cts = cancellationToken == CancellationToken.None ? new () : CancellationTokenSource.CreateLinkedTokenSource (cancellationToken); }

	/// <inheritdoc />
	/// <remarks>
	///   The value returned for types derived from <see cref="CancelableEventArgs" /> is the token provided by the protected <see cref="Cts" />
	///   instance owned by this instance.
	/// </remarks>
	public CancellationToken CancellationToken => Cts.Token;

	/// <inheritdoc />
	public void RequestCancellation ()
	{
		ObjectDisposedException.ThrowIf (_isDisposed, this);

		Cts.Token.ThrowIfCancellationRequested ();

		Cts.Cancel ();
	}

	// Disable this warning because the analyzer doesn't understand that we are using the correct Token anyway.
#pragma warning disable PH_P007
	/// <inheritdoc />
	public void RequestCancellation (out CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf (_isDisposed, this);

		RequestCancellation ();

		cancellationToken = Cts.Token;
	}
#pragma warning restore PH_P007

	/// <inheritdoc />
	public virtual void Dispose ()
	{
		if (_isDisposed) {
			return;
		}

		Dispose (true);
		GC.SuppressFinalize (this);
	}

	/// <summary>
	///   Protected implementation for disposal, called by the public <see cref="Dispose" /> method and the type finalizer, if defined.
	/// </summary>
	/// <param name="disposing">
	///   Whether this method call is from a call of the public <see cref="Dispose()" /> method (<see langword="true" />) or by the GC, in the type
	///   finalizer (<see langword="false" />).
	/// </param>
	/// <remarks>
	///   When invoked with <paramref name="disposing" /> <see langword="true" />, will only execute once. Subsequent calls with
	///   <paramref name="disposing" /> <see langword="true" /> will return immediately.
	/// </remarks>
	protected virtual void Dispose (bool disposing)
	{
		if (!disposing || _isDisposed) {
			return;
		}

		Cts.Dispose ();
		_isDisposed = true;
	}

	/// <inheritdoc />
	~CancelableEventArgs () { Dispose (false); }
}
