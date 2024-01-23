using System.IO;
using System.Linq;
using System.Text;

namespace Terminal.Gui; 

/// <summary>
/// Autocomplete for a <see cref="TextField"/> which shows suggestions within the box.
/// Displayed suggestions can be completed using the tab key.
/// </summary>
public class AppendAutocomplete : AutocompleteBase {
	bool _suspendSuggestions;

	TextField textField;

	/// <summary>
	/// Creates a new instance of the <see cref="AppendAutocomplete"/> class.
	/// </summary>
	public AppendAutocomplete (TextField textField)
	{
		this.textField = textField;
		SelectionKey = KeyCode.Tab;

		ColorScheme = new ColorScheme {
			Normal = new Attribute (Color.DarkGray, Color.Black),
			Focus = new Attribute (Color.DarkGray, Color.Black),
			HotNormal = new Attribute (Color.DarkGray, Color.Black),
			HotFocus = new Attribute (Color.DarkGray, Color.Black),
			Disabled = new Attribute (Color.DarkGray, Color.Black)
		};
	}

	/// <inheritdoc/>
	public override View HostControl { get => textField; set => textField = (TextField)value; }

	/// <summary>
	/// The color used for rendering the appended text. Note that only
	/// <see cref="ColorScheme.Normal"/> is used and then only <see cref="Attribute.Foreground"/>
	/// (Background comes from <see cref="HostControl"/>).
	/// </summary>
	public override ColorScheme ColorScheme { get; set; }

	/// <inheritdoc/>
	public override void ClearSuggestions ()
	{
		base.ClearSuggestions ();
		textField.SetNeedsDisplay ();
	}

	/// <inheritdoc/>
	public override bool MouseEvent (MouseEvent me, bool fromHost = false) => false;

	/// <inheritdoc/>
	public override bool ProcessKey (Key a)
	{
		var key = a.KeyCode;
		if (key == SelectionKey) {
			return AcceptSelectionIfAny ();
		}
		if (key == KeyCode.CursorUp) {
			return CycleSuggestion (1);
		}
		if (key == KeyCode.CursorDown) {
			return CycleSuggestion (-1);
		}
		if (key == CloseKey && Suggestions.Any ()) {
			ClearSuggestions ();
			_suspendSuggestions = true;
			return true;
		}

		if (char.IsLetterOrDigit ((char)a)) {
			_suspendSuggestions = false;
		}

		return false;
	}

	/// <inheritdoc/>
	public override void GenerateSuggestions (AutocompleteContext context)
	{
		if (_suspendSuggestions) {
			_suspendSuggestions = false;
			return;
		}
		base.GenerateSuggestions (context);
	}

	/// <summary>
	/// Renders the current suggestion into the <see cref="TextField"/>
	/// </summary>
	public override void RenderOverlay (Point renderAt)
	{
		if (!MakingSuggestion ()) {
			return;
		}

		// draw it like its selected even though its not
		Application.Driver.SetAttribute (new Attribute (ColorScheme.Normal.Foreground, textField.ColorScheme.Focus.Background));
		textField.Move (textField.Text.Length, 0);

		var suggestion = Suggestions.ElementAt (SelectedIdx);
		var fragment = suggestion.Replacement.Substring (suggestion.Remove);

		var spaceAvailable = textField.Bounds.Width - textField.Text.GetColumns ();
		var spaceRequired = fragment.EnumerateRunes ().Sum (c => c.GetColumns ());

		if (spaceAvailable < spaceRequired) {
			fragment = new string (
				fragment.TakeWhile (c => (spaceAvailable -= ((Rune)c).GetColumns ()) >= 0)
					.ToArray ()
			);
		}

		Application.Driver.AddStr (fragment);
	}

	/// <summary>
	/// Accepts the current autocomplete suggestion displaying in the text box.
	/// Returns true if a valid suggestion was being rendered and acceptable or
	/// false if no suggestion was showing.
	/// </summary>
	/// <returns></returns>
	internal bool AcceptSelectionIfAny ()
	{
		if (MakingSuggestion ()) {

			var insert = Suggestions.ElementAt (SelectedIdx);
			var newText = textField.Text;
			newText = newText.Substring (0, newText.Length - insert.Remove);
			newText += insert.Replacement;
			textField.Text = newText;

			textField.MoveEnd ();

			ClearSuggestions ();
			return true;
		}

		return false;
	}

	internal void SetTextTo (FileSystemInfo fileSystemInfo)
	{
		var newText = fileSystemInfo.FullName;
		if (fileSystemInfo is DirectoryInfo) {
			newText += Path.DirectorySeparatorChar;
		}
		textField.Text = newText;
		textField.MoveEnd ();
	}

	/// <summary>
	/// Returns true if there is a suggestion that can be made and the control
	/// is in a state where user would expect to see auto-complete (i.e. focused and
	/// cursor in right place).
	/// </summary>
	/// <returns></returns>
	bool MakingSuggestion () => Suggestions.Any () && SelectedIdx != -1 && textField.HasFocus && textField.CursorIsAtEnd ();

	bool CycleSuggestion (int direction)
	{
		if (Suggestions.Count <= 1) {
			return false;
		}

		SelectedIdx = (SelectedIdx + direction) % Suggestions.Count;

		if (SelectedIdx < 0) {
			SelectedIdx = Suggestions.Count () - 1;
		}
		textField.SetNeedsDisplay ();
		return true;
	}
}