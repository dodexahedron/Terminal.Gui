#nullable enable
using System.ComponentModel;

namespace Terminal.Gui.ConsoleDrivers.Windows;

internal class WindowsClipboard : ClipboardBase {
	public WindowsClipboard ()
	{
		IsSupported = IsClipboardFormatAvailable (_cfUnicodeText);
	}

	public override bool IsSupported { get; }

	protected override string GetClipboardDataImpl ()
	{
		try {
			if (!OpenClipboard (nint.Zero)) {
				return string.Empty;
			}

			var handle = GetClipboardData (_cfUnicodeText);
			if (handle == nint.Zero) {
				return string.Empty;
			}

			var pointer = nint.Zero;

			try {
				pointer = GlobalLock (handle);
				if (pointer == nint.Zero) {
					return string.Empty;
				}

				var size = GlobalSize (handle);
				var buff = new byte [size];

				Marshal.Copy (pointer, buff, 0, size);

				return Encoding.Unicode.GetString (buff).TrimEnd ('\0');
			} finally {
				if (pointer != nint.Zero) {
					GlobalUnlock (handle);
				}
			}
		} finally {
			CloseClipboard ();
		}
	}

	protected override void SetClipboardDataImpl (string text)
	{
		OpenClipboard ();

		EmptyClipboard ();
		nint hGlobal = default;
		try {
			var bytes = (text.Length + 1) * 2;
			hGlobal = Marshal.AllocHGlobal (bytes);

			if (hGlobal == default) {
				ThrowWin32 ();
			}

			var target = GlobalLock (hGlobal);

			if (target == default) {
				ThrowWin32 ();
			}

			try {
				Marshal.Copy (text.ToCharArray (), 0, target, text.Length);
			} finally {
				GlobalUnlock (target);
			}

			if (SetClipboardData (_cfUnicodeText, hGlobal) == default) {
				ThrowWin32 ();
			}

			hGlobal = default;
		} finally {
			if (hGlobal != default) {
				Marshal.FreeHGlobal (hGlobal);
			}

			CloseClipboard ();
		}
	}

	void OpenClipboard ()
	{
		var num = 10;
		while (true) {
			if (OpenClipboard (default)) {
				break;
			}

			if (--num == 0) {
				ThrowWin32 ();
			}

			Thread.Sleep (100);
		}
	}

	const uint _cfUnicodeText = 13;

	void ThrowWin32 ()
	{
		throw new Win32Exception (Marshal.GetLastWin32Error ());
	}

	[DllImport ("User32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool IsClipboardFormatAvailable (uint format);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern int GlobalSize (nint handle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern nint GlobalLock (nint hMem);

	[DllImport ("kernel32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool GlobalUnlock (nint hMem);

	[DllImport ("user32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool OpenClipboard (nint hWndNewOwner);

	[DllImport ("user32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool CloseClipboard ();

	[DllImport ("user32.dll", SetLastError = true)]
	static extern nint SetClipboardData (uint uFormat, nint data);

	[DllImport ("user32.dll")]
	static extern bool EmptyClipboard ();

	[DllImport ("user32.dll", SetLastError = true)]
	static extern nint GetClipboardData (uint uFormat);
}
