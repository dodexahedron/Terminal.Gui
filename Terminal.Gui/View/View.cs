using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Terminal.Gui;

#region API Docs
/// <summary>
/// View is the base class for all views on the screen and represents a visible element that can render itself and
/// contains zero or more nested views, called SubViews. View provides basic functionality for layout, positioning,
/// and drawing. In addition, View provides keyboard and mouse event handling.
/// </summary>
/// <remarks>
/// <list type="table">
///         <listheader>
///                 <term>Term</term><description>Definition</description>
///         </listheader>
///         <item>
///                 <term>SubView</term>
///                 <description>
///                 A View that is contained in another view and will be rendered as part of the containing view's
///                 ContentArea.
///                 SubViews are added to another view via the <see cref="View.Add(View)"/>` method. A View may only be a
///                 SubView of a single View.
///                 </description>
///         </item>
///         <item>
///                 <term>SuperView</term><description>The View that is a container for SubViews.</description>
///         </item>
/// </list>
/// <para>
/// Focus is a concept that is used to describe which View is currently receiving user input. Only Views that are
/// <see cref="Enabled"/>, <see cref="Visible"/>, and <see cref="CanFocus"/> will receive focus.
/// </para>
/// <para>
/// Views that are focusable should implement the <see cref="PositionCursor"/> to make sure that
/// the cursor is placed in a location that makes sense. Unix terminals do not have
/// a way of hiding the cursor, so it can be distracting to have the cursor left at
/// the last focused view. So views should make sure that they place the cursor
/// in a visually sensible place.
/// </para>
/// <para>
/// The View defines the base functionality for user interface elements in Terminal.Gui. Views
/// can contain one or more subviews, can respond to user input and render themselves on the screen.
/// </para>
/// <para>
/// View supports two layout styles: <see cref="LayoutStyle.Absolute"/> or <see cref="LayoutStyle.Computed"/>.
/// The style is determined by the values of <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and
/// <see cref="Height"/>.
/// If any of these is set to non-absolute <see cref="Pos"/> or <see cref="Dim"/> object,
/// then the layout style is <see cref="LayoutStyle.Computed"/>. Otherwise it is <see cref="LayoutStyle.Absolute"/>.
/// </para>
/// <para>
/// To create a View using Absolute layout, call a constructor that takes a
/// Rect parameter to specify the absolute position and size or simply set <see cref="View.Frame "/>). To create a View
/// using Computed layout use a constructor that does not take a Rect parameter and set the X, Y, Width and Height
/// properties on the view to non-absolute values. Both approaches use coordinates that are relative to the
/// <see cref="Bounds"/> of the <see cref="SuperView"/> the View is added to.
/// </para>
/// <para>
/// Computed layout is more flexible and supports dynamic console apps where controls adjust layout
/// as the terminal resizes or other Views change size or position. The
/// <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and
/// <see cref="Height"/> properties are <see cref="Dim"/> and <see cref="Pos"/> objects that dynamically update the
/// position of a view.
/// The X and Y properties are of type <see cref="Pos"/>
/// and you can use either absolute positions, percentages, or anchor
/// points. The Width and Height properties are of type
/// <see cref="Dim"/> and can use absolute position,
/// percentages, and anchors. These are useful as they will take
/// care of repositioning views when view's adornments are resized or
/// if the terminal size changes.
/// </para>
/// <para>
/// Absolute layout requires specifying coordinates and sizes of Views explicitly, and the
/// View will typically stay in a fixed position and size. To change the position and size use the
/// <see cref="Frame"/> property.
/// </para>
/// <para>
/// Subviews (child views) can be added to a View by calling the <see cref="Add(View)"/> method.
/// The container of a View can be accessed with the <see cref="SuperView"/> property.
/// </para>
/// <para>
/// To flag a region of the View's <see cref="Bounds"/> to be redrawn call <see cref="SetNeedsDisplay(Rect)"/>.
/// To flag the entire view for redraw call <see cref="SetNeedsDisplay()"/>.
/// </para>
/// <para>
/// The <see cref="LayoutSubviews"/> method is invoked when the size or layout of a view has
/// changed. The default processing system will keep the size and dimensions
/// for views that use the <see cref="LayoutStyle.Absolute"/>, and will recompute the
/// Adornments for the views that use <see cref="LayoutStyle.Computed"/>.
/// </para>
/// <para>
/// Views have a <see cref="ColorScheme"/> property that defines the default colors that subviews
/// should use for rendering. This ensures that the views fit in the context where
/// they are being used, and allows for themes to be plugged in. For example, the
/// default colors for windows and Toplevels uses a blue background, while it uses
/// a white background for dialog boxes and a red background for errors.
/// </para>
/// <para>
/// Subclasses should not rely on <see cref="ColorScheme"/> being
/// set at construction time. If a <see cref="ColorScheme"/> is not set on a view, the view will inherit the
/// value from its <see cref="SuperView"/> and the value might only be valid once a view has been
/// added to a SuperView.
/// </para>
/// <para>
/// By using  <see cref="ColorScheme"/> applications will work both
/// in color as well as black and white displays.
/// </para>
/// <para>
/// Views can also opt-in to more sophisticated initialization
/// by implementing overrides to <see cref="ISupportInitialize.BeginInit"/> and
/// <see cref="ISupportInitialize.EndInit"/> which will be called
/// when the view is added to a <see cref="SuperView"/>.
/// </para>
/// <para>
/// If first-run-only initialization is preferred, overrides to <see cref="ISupportInitializeNotification"/>
/// can be implemented, in which case the <see cref="ISupportInitialize"/>
/// methods will only be called if <see cref="ISupportInitializeNotification.IsInitialized"/>
/// is <see langword="false"/>. This allows proper <see cref="View"/> inheritance hierarchies
/// to override base class layout code optimally by doing so only on first run,
/// instead of on every run.
/// </para>
/// <para>
/// See <see href="../docs/keyboard.md">for an overview of View keyboard handling.</see>
/// </para>
/// ///
/// </remarks>
#endregion API Docs

public partial class View : Responder, ISupportInitializeNotification {
	/// <summary>
	/// Previous value of Enabled
	/// </summary>
	private bool _oldEnabled;

	private string _title = string.Empty;

	/// <summary>
	/// Points to the current driver in use by the view, it is a convenience property
	/// for simplifying the development of new views.
	/// </summary>
	public static ConsoleDriver Driver => Application.Driver;

	/// <summary>
	/// Gets or sets arbitrary data for the view.
	/// </summary>
	/// <remarks>This property is not used internally.</remarks>
	public object Data { get; set; }

	/// <summary>
	/// Gets or sets an identifier for the view;
	/// </summary>
	/// <value>The identifier.</value>
	/// <remarks>The id should be unique across all Views that share a SuperView.</remarks>
	public string Id { get; set; } = "";

	/// <summary>
	/// The title to be displayed for this <see cref="View"/>. The title will be displayed if <see cref="Border"/>.
	/// <see cref="Thickness.Top"/>
	/// is greater than 0.
	/// </summary>
	/// <value>The title.</value>
	public string Title {
		get => _title;
		set {
			if (!OnTitleChanging (_title, value)) {
				var old = _title;
				_title = value;
				SetNeedsDisplay ();
#if DEBUG
				if (_title != null && string.IsNullOrEmpty (Id)) {
					Id = _title;
				}
#endif // DEBUG
				OnTitleChanged (old, _title);
			}
		}
	}

	private bool _enabled = true;

	/// <summary>
	/// Gets or sets a value indicating whether this <see cref="View"/> can respond to user interaction.
	/// </summary>
	public virtual bool Enabled {
		get => _enabled;
		set {
			if ( _enabled == value )
				return;

			if (value) {
				if (SuperView is null or { Enabled: true }) {
					_enabled = true;
				}
			} else {
				_enabled = false;
			}
			if (!value && HasFocus) {
				SetHasFocus (false, this);
			}
			OnEnabledChanged ();
			SetNeedsDisplay ();

			if (_subviews != null) {
				foreach (var view in _subviews) {
					if (!value) {
						view._oldEnabled = view.Enabled;
						view.Enabled = false;
					} else {
						view.Enabled = view._oldEnabled;
						view._addingView = false;
					}
				}
			}
		}
	}

	/// <summary>
	/// Gets or sets whether a view is cleared if the <see cref="Visible"/> property is <see langword="false"/>.
	/// </summary>
	public bool ClearOnVisibleFalse { get; set; } = true;

	private bool _visible = true;

	/// <summary>
	///   Gets a value indicating whether this <see cref="View" /> will be displayed.
	/// </summary>
	/// <remarks>
	///   Value returned by the getter is the binary AND of this <see cref="View" />'s last explicitly set visibility and the Visible property of
	///   <see cref="SuperView" />.
	/// </remarks>
	public virtual bool Visible => _visible && (SuperView?.Visible ?? true);

	public virtual void SetDesiredVisibility (bool value) {
		if (Visible == value)
			return;

		_visible = value;

		// TODO: Raise a new VisibleChanging event.
		// TODO: Consider subscribing newly-added subviews to one or both of these events.
		// TODO: Consider whether recursive getter is necessary or not after events are in place.
		if (!value) {
			if (HasFocus) {
				SetHasFocus (false, this);
			}

			if (ClearOnVisibleFalse) {
				Clear ();
			}
		}

		OnVisibleChanged (!value, value);
		SetNeedsDisplay ();
	}

	/// <summary>
	/// Called before the <see cref="View.Title"/> changes. Invokes the <see cref="TitleChanging"/> event, which can be
	/// cancelled.
	/// </summary>
	/// <param name="oldTitle">The <see cref="View.Title"/> that is/has been replaced.</param>
	/// <param name="newTitle">The new <see cref="View.Title"/> to be replaced.</param>
	/// <returns>`true` if an event handler canceled the Title change.</returns>
	public virtual bool OnTitleChanging (string oldTitle, string newTitle)
	{
		var args = new TitleEventArgs (oldTitle, newTitle);
		TitleChanging?.Invoke (this, args);
		return args.Cancel;
	}

	/// <summary>
	/// Event fired when the <see cref="View.Title"/> is changing. Set <see cref="TitleEventArgs.Cancel"/> to
	/// `true` to cancel the Title change.
	/// </summary>
	public event EventHandler<TitleEventArgs> TitleChanging;

	/// <summary>
	/// Called when the <see cref="View.Title"/> has been changed. Invokes the <see cref="TitleChanged"/> event.
	/// </summary>
	/// <param name="oldTitle">The <see cref="View.Title"/> that is/has been replaced.</param>
	/// <param name="newTitle">The new <see cref="View.Title"/> to be replaced.</param>
	public virtual void OnTitleChanged (string oldTitle, string newTitle)
	{
		var args = new TitleEventArgs (oldTitle, newTitle);
		TitleChanged?.Invoke (this, args);
	}

	/// <summary>
	/// Event fired after the <see cref="View.Title"/> has been changed.
	/// </summary>
	public event EventHandler<TitleEventArgs> TitleChanged;

	/// <summary>
	/// Event fired when the <see cref="Enabled"/> value is being changed.
	/// </summary>
	public event EventHandler EnabledChanged;

	/// <inheritdoc/>
	public override void OnEnabledChanged () => EnabledChanged?.Invoke (this, EventArgs.Empty);

	/// <summary>
	/// Event fired when the <see cref="Visible"/> value is being changed.
	/// </summary>
	public event EventHandler VisibleChanged;

	/// <inheritdoc/>
	public override void OnVisibleChanged () => VisibleChanged?.Invoke (this, EventArgs.Empty);

	private bool CanBeVisible (View view)
	{
		if (!view.Visible) {
			return false;
		}
		for (var c = view.SuperView; c != null; c = c.SuperView) {
			if (!c.Visible) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Pretty prints the View
	/// </summary>
	/// <returns></returns>
	public override string ToString () => $"{GetType ().Name}({Id}){Frame}";

	/// <inheritdoc/>
	protected override void Dispose (bool disposing)
	{
		LineCanvas.Dispose ();

		Margin?.Dispose ();
		Margin = null;
		Border?.Dispose ();
		Border = null;
		Padding?.Dispose ();
		Padding = null;

		for (var i = InternalSubviews.Count - 1; i >= 0; i--) {
			var subview = InternalSubviews [i];
			Remove (subview);
			subview.Dispose ();
		}

		base.Dispose (disposing);
		Debug.Assert (InternalSubviews.Count == 0);
	}

	#region Constructors and Initialization
	/// <summary>
	/// Initializes a new instance of a <see cref="View"/> class with the absolute
	/// dimensions specified in the <paramref name="frame"/> parameter.
	/// </summary>
	/// <param name="frame">The region covered by this view.</param>
	/// <remarks>
	///         <para>
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view.
	///         The <see cref="View"/> will be created using <see cref="LayoutStyle.Absolute"/>
	///         coordinates. The initial size (<see cref="View.Frame"/>) will be
	///         adjusted to fit the contents of <see cref="Text"/>, including newlines ('\n') for multiple lines.
	///         </para>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         This constructor initialize a View with a <see cref="LayoutStyle"/> of
	///         <see cref="LayoutStyle.Absolute"/>.
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view, changing it to  <see cref="LayoutStyle.Computed"/>.
	///         </para>
	/// </remarks>
	public View (Rect frame) : this (frame, null) { }

	/// <summary>
	/// Initializes a new instance of <see cref="View"/>.
	/// </summary>
	/// <remarks>
	///         <para>
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view.
	///         The <see cref="View"/> will be created using <see cref="LayoutStyle.Absolute"/>
	///         coordinates. The initial size (<see cref="View.Frame"/>) will be
	///         adjusted to fit the contents of <see cref="Text"/>, including newlines ('\n') for multiple lines.
	///         </para>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         This constructor initialize a View with a <see cref="LayoutStyle"/> of
	///         <see cref="LayoutStyle.Absolute"/>.
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view, changing it to  <see cref="LayoutStyle.Computed"/>.
	///         </para>
	/// </remarks>
	public View () : this (string.Empty) { }

	/// <summary>
	/// Initializes a new instance of <see cref="View"/> in at the position specified with the
	/// dimensions specified in the <paramref name="x"/> and <paramref name="y"/> parameters.
	/// </summary>
	/// <remarks>
	///         <para>
	///         The <see cref="View"/> will be created at the given
	///         coordinates with the given string. The size (<see cref="View.Frame"/>) will be
	///         adjusted to fit the contents of <see cref="Text"/>, including newlines ('\n') for multiple lines.
	///         </para>
	///         <para>
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view.
	///         </para>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         This constructor initialize a View with a <see cref="LayoutStyle"/> of
	///         <see cref="LayoutStyle.Absolute"/>.
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view, changing it to  <see cref="LayoutStyle.Computed"/>.
	///         </para>
	/// </remarks>
	/// <param name="x">column to locate the View.</param>
	/// <param name="y">row to locate the View.</param>
	/// <param name="text">text to initialize the <see cref="Text"/> property with.</param>
	public View (int x, int y, string text) : this (TextFormatter.CalcRect (x, y, text), text) { }

	/// <summary>
	/// Initializes a new instance of a <see cref="View"/> class with the absolute
	/// dimensions specified in the <paramref name="frame"/> parameter.
	/// </summary>
	/// <remarks>
	///         <para>
	///         The <see cref="View"/> will be created at the given
	///         coordinates with the given string. The size (<see cref="View.Frame"/>) will be
	///         adjusted to fit the contents of <see cref="Text"/>, including newlines ('\n') for multiple lines.
	///         </para>
	///         <para>
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view.
	///         </para>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         This constructor initialize a View with a <see cref="LayoutStyle"/> of
	///         <see cref="LayoutStyle.Absolute"/>.
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view, changing it to  <see cref="LayoutStyle.Computed"/>.
	///         </para>
	/// </remarks>
	/// <param name="frame">Location.</param>
	/// <param name="text">text to initialize the <see cref="Text"/> property with.</param>
	public View (Rect frame, string text) => SetInitialProperties (text, frame, LayoutStyle.Absolute);


	/// <summary>
	/// Initializes a new instance of a <see cref="View"/> class with using the given text and text styling information.
	/// </summary>
	/// <remarks>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         The <see cref="View"/> will be created at the given
	///         coordinates with the given string. The size (<see cref="View.Frame"/>) will be
	///         adjusted to fit the contents of <see cref="Text"/>, including newlines ('\n') for multiple lines.
	///         </para>
	///         <para>
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view.
	///         </para>
	///         <para>
	///         If <see cref="Height"/> is greater than one, word wrapping is provided.
	///         </para>
	///         <para>
	///         This constructor initialize a View with a <see cref="LayoutStyle"/> of
	///         <see cref="LayoutStyle.Absolute"/>.
	///         Use <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, and <see cref="Height"/> properties to dynamically
	///         control the size and location of the view, changing it to  <see cref="LayoutStyle.Computed"/>.
	///         </para>
	/// </remarks>
	/// <param name="text">text to initialize the <see cref="Text"/> property with.</param>
	/// <param name="direction">The text direction.</param>
	public View (string text, TextDirection direction = TextDirection.LeftRight_TopBottom) => SetInitialProperties (text, Rect.Empty, LayoutStyle.Computed, direction);

	// TODO: v2 - Remove constructors with parameters


	/// <summary>
	/// Private helper to set the initial properties of the View that were provided via constructors.
	/// </summary>
	/// <param name="text"></param>
	/// <param name="rect"></param>
	/// <param name="layoutStyle"></param>
	/// <param name="direction"></param>
	private void SetInitialProperties (string text,
	                                   Rect rect,
	                                   LayoutStyle layoutStyle = LayoutStyle.Computed,
	                                   TextDirection direction = TextDirection.LeftRight_TopBottom)
	{
		TextFormatter = new TextFormatter ();
		TextFormatter.HotKeyChanged += TextFormatter_HotKeyChanged;
		TextDirection = direction;

		CanFocus = false;
		TabIndex = -1;
		TabStop = false;

		Text = text == null ? string.Empty : text;
		Frame = rect.IsEmpty ? TextFormatter.CalcRect (0, 0, text, direction) : rect;

		AddCommands ();

		Margin = CreateAdornment (typeof (Margin)) as Margin;
		Border = CreateAdornment (typeof (Border)) as Border;
		Padding = CreateAdornment (typeof (Padding)) as Padding;
	}

	/// <summary>
	/// Get or sets if  the <see cref="View"/> has been initialized (via <see cref="ISupportInitialize.BeginInit"/>
	/// and <see cref="ISupportInitialize.EndInit"/>).
	/// </summary>
	/// <para>
	/// If first-run-only initialization is preferred, overrides to <see cref="ISupportInitializeNotification.IsInitialized"/>
	/// can be implemented, in which case the <see cref="ISupportInitialize"/>
	/// methods will only be called if <see cref="ISupportInitializeNotification.IsInitialized"/>
	/// is <see langword="false"/>. This allows proper <see cref="View"/> inheritance hierarchies
	/// to override base class layout code optimally by doing so only on first run,
	/// instead of on every run.
	/// </para>
	public virtual bool IsInitialized { get; set; }

	/// <summary>
	/// Signals the View that initialization is starting. See <see cref="ISupportInitialize"/>.
	/// </summary>
	/// <remarks>
	///         <para>
	///         Views can opt-in to more sophisticated initialization
	///         by implementing overrides to <see cref="ISupportInitialize.BeginInit"/> and
	///         <see cref="ISupportInitialize.EndInit"/> which will be called
	///         when the view is added to a <see cref="SuperView"/>.
	///         </para>
	///         <para>
	///         If first-run-only initialization is preferred, overrides to <see cref="ISupportInitializeNotification"/>
	///         can be implemented too, in which case the <see cref="ISupportInitialize"/>
	///         methods will only be called if <see cref="ISupportInitializeNotification.IsInitialized"/>
	///         is <see langword="false"/>. This allows proper <see cref="View"/> inheritance hierarchies
	///         to override base class layout code optimally by doing so only on first run,
	///         instead of on every run.
	///         </para>
	/// </remarks>
	public virtual void BeginInit ()
	{
		if (!IsInitialized) {
			_oldCanFocus = CanFocus;
			_oldTabIndex = _tabIndex;
		}

		//throw new InvalidOperationException ("The view is already initialized.");
		if (_subviews?.Count > 0) {
			foreach (var view in _subviews) {
				if (!view.IsInitialized) {
					view.BeginInit ();
				}
			}
		}
	}

	/// <summary>
	/// Signals the View that initialization is ending. See <see cref="ISupportInitialize"/>.
	/// </summary>
	public void EndInit ()
	{
		IsInitialized = true;
		// These calls were moved from BeginInit as they access Bounds which is indeterminate until EndInit is called.
		UpdateTextDirection (TextDirection);
		UpdateTextFormatterText ();
		SetHotKey ();

		OnResizeNeeded ();
		if (_subviews != null) {
			foreach (var view in _subviews) {
				if (!view.IsInitialized) {
					view.EndInit ();
				}
			}
		}
		Initialized?.Invoke (this, EventArgs.Empty);
	}
	#endregion Constructors and Initialization
}