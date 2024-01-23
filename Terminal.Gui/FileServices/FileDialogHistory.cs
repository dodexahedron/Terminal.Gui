using System.Collections.Generic;
using System.IO.Abstractions;

namespace Terminal.Gui; 

class FileDialogHistory {
	readonly Stack<FileDialogState> back = new ();
	readonly FileDialog dlg;
	readonly Stack<FileDialogState> forward = new ();

	public FileDialogHistory (FileDialog dlg) => this.dlg = dlg;

	public bool Back ()
	{

		IDirectoryInfo goTo = null;
		FileSystemInfoStats restoreSelection = null;
		string restorePath = null;

		if (CanBack ()) {

			var backTo = back.Pop ();
			goTo = backTo.Directory;
			restoreSelection = backTo.Selected;
			restorePath = backTo.Path;

		} else if (CanUp ()) {
			goTo = dlg.State?.Directory.Parent;
		}

		// nowhere to go
		if (goTo == null) {
			return false;
		}

		forward.Push (dlg.State);
		dlg.PushState (goTo, false, true, false, restorePath);


		if (restoreSelection != null) {
			dlg.RestoreSelection (restoreSelection.FileSystemInfo);
		}

		return true;
	}

	internal bool CanBack () => back.Count > 0;

	internal bool Forward ()
	{
		if (forward.Count > 0) {

			dlg.PushState (forward.Pop ().Directory, true, true, false);
			return true;
		}

		return false;
	}

	internal bool Up ()
	{
		var parent = dlg.State?.Directory.Parent;
		if (parent != null) {

			back.Push (new FileDialogState (parent, dlg));
			dlg.PushState (parent, false);
			return true;
		}

		return false;
	}

	internal bool CanUp () => dlg.State?.Directory.Parent != null;

	internal void Push (FileDialogState state, bool clearForward)
	{
		if (state == null) {
			return;
		}

		// if changing to a new directory push onto the Back history
		if (back.Count == 0 || back.Peek ().Directory.FullName != state.Directory.FullName) {

			back.Push (state);
			if (clearForward) {
				ClearForward ();
			}
		}
	}

	internal bool CanForward () => forward.Count > 0;

	internal void ClearForward () => forward.Clear ();
}