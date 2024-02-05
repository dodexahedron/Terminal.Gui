#nullable enable
namespace Terminal.Gui.Events;

/// <summary>
///   An interface for types implementing support for enabling and disabling processing of events implemented by the declaring type.
/// </summary>
[UsedImplicitly (ImplicitUseTargetFlags.WithMembers)]
public interface ISupportsEnableDisable {
	/// <summary>
	///   Gets a value indicating if this <see cref="ISupportsEnableDisable" /> should respond to user interaction or other events.
	/// </summary>
	/// <remarks>
	///   <para>
	///     It is suggested that this property be backed explicitly by a private or protected field on types implementing this interface and that
	///     no set accessor is provided for this property.
	///     <para />
	///     <see cref="View" /> types provided by Terminal.Gui follow this pattern.
	///   </para>
	/// </remarks>
	bool IsEnabled { get; }

	/// <summary>
	///   Performs implementation-specific operations necessary to disable this instance of an <see cref="ISupportsEnableDisable" /> and returns a
	///   <see langword="bool" /> value indicating success or failure, with an optional <paramref name="sourceView" /> as the sender parameter of
	///   the <see cref="Disabling" /> and <see cref="Disabled" /> events.
	/// </summary>
	/// <param name="sourceView">
	///   An optional reference to an instance of a <see cref="View" /> that should be used as the sender parameter of the <see cref="Disabling" />
	///   and <see cref="Disabled" /> events.
	/// </param>
	/// <returns>
	///   A <see langword="bool" /> value indicating success or failure of the attempt to disable this <see cref="ISupportsEnableDisable" /> and if
	///   any state changes were actually made.
	/// </returns>
	/// <remarks>
	///   <para>Notes to implementers:</para>
	///   <para>
	///     It is expected that this method will perform the necessary actions to disable event processing as described by the
	///     <see cref="IsEnabled" /> property, either in its own body or by delegating to <see langword="base" />.<see cref="Disable{T}" />
	///     <i>before</i> any change is made that would result in the value returned by <see cref="IsEnabled" /> changing.
	///   </para>
	///   <para>
	///     The method is also expected to raise the <see cref="Disabling" /> and <see cref="Disabled" /> events at their respective appropriate
	///     times by the same method as above. The base implementation provided by <see cref="View" /> should typically be sufficient, as it
	///     already handles this.
	///   </para>
	///   <para>
	///     When overriding this method, it is expected that derived types will call this method on their base type and that only any specific
	///     additional operations necessary to support those types will be carried out. If any such operations would dictate that events should be
	///     raised at different points, a full implementation of all related members of this interface as overrides from the base type is
	///     suggested.
	///   </para>
	///   <para>
	///     Types implementing this interface must be prepared to receive calls from other types implementing this interface, especially if any
	///     instances of those types are instantiated and assigned as ancestors or descendants of the type implementing this interface.
	///   </para>
	///   <para>
	///     If a type explicitly overrides or hides this method or provides an explicit interface implementation of it, the above expectations
	///     should be followed, to ensure consistent behavior.
	///   </para>
	///   <para>
	///     If invoked without the <paramref name="sourceView" /> parameter or if the <paramref name="sourceView" /> parameter is explicitly
	///     <see langword="null" />, the expectation is that types implementing this interface will substitute a self-reference. The base
	///     implementation in <see cref="View" /> follows this behavior.
	///   </para>
	/// </remarks>
	bool Disable<T> (T? sourceView = null) where T : View;

    /// <summary>
    ///   Event raised after the <see cref="IsEnabled" /> property is changed from <see langword="true" /> to <see langword="false" />.
    /// </summary>
    event EventHandler<EnabledDisabledEventArgs>? Disabled;

    /// <summary>
    ///   Event raised before the <see cref="IsEnabled" /> property is changed from <see langword="true" /> to <see langword="false" />.
    /// </summary>
    event EventHandler<EnablingDisablingEventArgs>? Disabling;

    /// <summary>
    ///   Performs implementation-specific operations necessary to enable this instance of an <see cref="ISupportsEnableDisable" /> and returns a
    ///   <see langword="bool" /> value indicating success or failure, with an optional <paramref name="sourceView" /> as the sender parameter of
    ///   the <see cref="Enabling" /> and <see cref="Enabled" /> events.
    /// </summary>
    /// <param name="sourceView">
    ///   An optional reference to an instance of a <see cref="View" /> that should be used as the sender parameter of the <see cref="Enabling" />
    ///   and <see cref="Enabled" /> events.
    /// </param>
    /// <returns>
    ///   A <see langword="bool" /> value indicating success or failure of the attempt to enable this <see cref="ISupportsEnableDisable" /> and if
    ///   any state changes were actually made.
    /// </returns>
    /// <remarks>
    ///   <para>Notes to implementers:</para>
    ///   <para>
    ///     It is expected that this method will perform the necessary actions to enable event processing as described by the
    ///     <see cref="IsEnabled" /> property, either in its own body or by delegating to <see langword="base" />.<see cref="Enable{T}" />
    ///     <i>before</i> any change is made that would result in the value returned by <see cref="IsEnabled" /> changing.
    ///   </para>
    ///   <para>
    ///     The method is also expected to raise the <see cref="Enabling" /> and <see cref="Enabled" /> events at their respective appropriate
    ///     times by the same method as above. The base implementation provided by <see cref="View" /> should typically be sufficient, as it
    ///     already handles this.
    ///   </para>
    ///   <para>
    ///     When overriding this method, it is expected that derived types will call this method on their base type and that only any specific
    ///     additional operations necessary to support those types will be carried out. If any such operations would dictate that events should be
    ///     raised at different points, a full implementation of all related members of this interface as overrides from the base type is
    ///     suggested.
    ///   </para>
    ///   <para>
    ///     Types implementing this interface must be prepared to receive calls from other types implementing this interface, especially if any
    ///     instances of those types are instantiated and assigned as ancestors or descendants of the type implementing this interface.
    ///   </para>
    ///   <para>
    ///     If a type explicitly overrides or hides this method or provides an explicit interface implementation of it, the above expectations
    ///     should be followed, to ensure consistent behavior.
    ///   </para>
    ///   <para>
    ///     If invoked without the <paramref name="sourceView" /> parameter or if the <paramref name="sourceView" /> parameter is explicitly
    ///     <see langword="null" />, the expectation is that types implementing this interface will substitute a self-reference. The base
    ///     implementation in <see cref="View" /> follows this behavior.
    ///   </para>
    /// </remarks>
    bool Enable<T> (T? sourceView = null) where T : View;

    /// <summary>
    ///   Event raised after the <see cref="IsEnabled" /> property is changed from <see langword="false" /> to <see langword="true" />.
    /// </summary>
    event EventHandler<EnabledDisabledEventArgs>? Enabled;

    /// <summary>
    ///   Event raised before the <see cref="IsEnabled" /> property is changed from <see langword="false" /> to <see langword="true" />.
    /// </summary>
    event EventHandler<EnablingDisablingEventArgs>? Enabling;
}
