﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;

namespace Terminal.Gui; 

class FileDialogState {
	protected readonly FileDialog Parent;

	public FileDialogState (IDirectoryInfo dir, FileDialog parent)
	{
		Directory = dir;
		Parent = parent;
		Path = parent.Path;

		RefreshChildren ();
	}

	public FileSystemInfoStats Selected { get; set; }

	/// <summary>
	/// Gets what was entered in the path text box of the dialog
	/// when the state was active.
	/// </summary>
	public string Path { get; }

	public IDirectoryInfo Directory { get; }

	public FileSystemInfoStats [] Children { get; internal set; }

	internal virtual void RefreshChildren ()
	{
		var dir = Directory;
		Children = GetChildren (dir).ToArray ();
	}

	protected virtual IEnumerable<FileSystemInfoStats> GetChildren (IDirectoryInfo dir)
	{
		try {

			List<FileSystemInfoStats> children;

			// if directories only
			if (Parent.OpenMode == OpenMode.Directory) {
				children = dir.GetDirectories ().Select (e => new FileSystemInfoStats (e, Parent.Style.Culture)).ToList ();
			} else {
				children = dir.GetFileSystemInfos ().Select (e => new FileSystemInfoStats (e, Parent.Style.Culture)).ToList ();
			}

			// if only allowing specific file types
			if (Parent.AllowedTypes.Any () && Parent.OpenMode == OpenMode.File) {

				children = children.Where (
						c => c.IsDir ||
						     c.FileSystemInfo is IFileInfo f && Parent.IsCompatibleWithAllowedExtensions (f))
					.ToList ();
			}

			// if theres a UI filter in place too
			if (Parent.CurrentFilter != null) {
				children = children.Where (MatchesApiFilter).ToList ();
			}

			// allow navigating up as '..'
			if (dir.Parent != null) {
				children.Add (new FileSystemInfoStats (dir.Parent, Parent.Style.Culture) { IsParent = true });
			}

			return children;
		} catch (Exception) {
			// Access permissions Exceptions, Dir not exists etc
			return Enumerable.Empty<FileSystemInfoStats> ();
		}
	}

	protected bool MatchesApiFilter (FileSystemInfoStats arg) => arg.IsDir ||
	                                                             arg.FileSystemInfo is IFileInfo f && Parent.CurrentFilter.IsAllowed (f.FullName);
}