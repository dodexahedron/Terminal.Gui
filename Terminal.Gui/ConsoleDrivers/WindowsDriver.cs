//
// WindowsDriver.cs: Windows specific driver
//

// HACK:
// WindowsConsole/Terminal has two issues:
// 1) Tearing can occur when the console is resized.
// 2) The values provided during Init (and the first WindowsConsole.EventType.WindowBufferSize) are not correct.
//
// If HACK_CHECK_WINCHANGED is defined then we ignore WindowsConsole.EventType.WindowBufferSize events
// and instead check the console size every every 500ms in a thread in WidowsMainLoop. 
// As of Windows 11 23H2 25947.1000 and/or WT 1.19.2682 tearing no longer occurs when using 
// the WindowsConsole.EventType.WindowBufferSize event. However, on Init the window size is
// still incorrect so we still need this hack.
#define HACK_CHECK_WINCHANGED

using System.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Terminal.Gui.ConsoleDrivers;
using static Unix.Terminal.Delegates;
using static Terminal.Gui.ConsoleDrivers.ConsoleKeyMapping;

namespace Terminal.Gui;


internal class WindowsConsole {
	public const int STD_OUTPUT_HANDLE = -11;
	public const int STD_INPUT_HANDLE = -10;

	IntPtr _inputHandle, _outputHandle;
	IntPtr _screenBuffer;
	readonly uint _originalConsoleMode;
	CursorVisibility? _initialCursorVisibility = null;
	CursorVisibility? _currentCursorVisibility = null;
	CursorVisibility? _pendingCursorVisibility = null;
	readonly StringBuilder _stringBuilder = new StringBuilder (256 * 1024);

	public WindowsConsole ()
	{
		_inputHandle = GetStdHandle (STD_INPUT_HANDLE);
		_outputHandle = GetStdHandle (STD_OUTPUT_HANDLE);
		_originalConsoleMode = ConsoleMode;
		var newConsoleMode = _originalConsoleMode;
		newConsoleMode |= (uint)(ConsoleModes.EnableMouseInput | ConsoleModes.EnableExtendedFlags);
		newConsoleMode &= ~(uint)ConsoleModes.EnableQuickEditMode;
		newConsoleMode &= ~(uint)ConsoleModes.EnableProcessedInput;
		ConsoleMode = newConsoleMode;
	}

	CharInfo [] _originalStdOutChars;

	public bool WriteToConsole (Size size, ExtendedCharInfo [] charInfoBuffer, Coord bufferSize, SmallRect window, bool force16Colors)
	{
		if (_screenBuffer == IntPtr.Zero) {
			ReadFromConsoleOutput (size, bufferSize, ref window);
		}

		bool result = false;
		if (force16Colors) {
			int i = 0;
			CharInfo [] ci = new CharInfo [charInfoBuffer.Length];
			foreach (ExtendedCharInfo info in charInfoBuffer) {
				ci [i++] = new CharInfo () {
					Char = new CharUnion () { UnicodeChar = info.Char },
					Attributes = (ushort)((int)info.Attribute.Foreground.GetClosestNamedColor () | (int)info.Attribute.Background.GetClosestNamedColor () << 4)
				};
			}

			result = WriteConsoleOutput (_screenBuffer, ci, bufferSize, new Coord () { X = window.Left, Y = window.Top }, ref window);
		} else {

			_stringBuilder.Clear ();

			_stringBuilder.Append (EscSeqUtils.CSI_SaveCursorPosition);
			_stringBuilder.Append (EscSeqUtils.CSI_SetCursorPosition (0, 0));

			Attribute? prev = null;
			foreach (var info in charInfoBuffer) {
				var attr = info.Attribute;

				if (attr != prev) {
					prev = attr;
					_stringBuilder.Append (EscSeqUtils.CSI_SetForegroundColorRGB (attr.Foreground.R, attr.Foreground.G, attr.Foreground.B));
					_stringBuilder.Append (EscSeqUtils.CSI_SetBackgroundColorRGB (attr.Background.R, attr.Background.G, attr.Background.B));
				}

				if (info.Char != '\x1b') {
					if (!info.Empty) {
						_stringBuilder.Append (info.Char);
					}

				} else {
					_stringBuilder.Append (' ');
				}
			}

			_stringBuilder.Append (EscSeqUtils.CSI_RestoreCursorPosition);

			string s = _stringBuilder.ToString ();

			result = WriteConsole (_screenBuffer, s, (uint)(s.Length), out uint _, null);
		}

		if (!result) {
			var err = Marshal.GetLastWin32Error ();
			if (err != 0) {
				throw new System.ComponentModel.Win32Exception (err);
			}
		}

		return result;
	}

	public void ReadFromConsoleOutput (Size size, Coord coords, ref SmallRect window)
	{
		_screenBuffer = CreateConsoleScreenBuffer (
		    DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
		    ShareMode.FileShareRead | ShareMode.FileShareWrite,
		    IntPtr.Zero,
		    1,
		    IntPtr.Zero
		);
		if (_screenBuffer == INVALID_HANDLE_VALUE) {
			var err = Marshal.GetLastWin32Error ();

			if (err != 0) {
				throw new System.ComponentModel.Win32Exception (err);
			}
		}

		if (!_initialCursorVisibility.HasValue && GetCursorVisibility (out CursorVisibility visibility)) {
			_initialCursorVisibility = visibility;
		}

		if (!SetConsoleActiveScreenBuffer (_screenBuffer)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}

		_originalStdOutChars = new CharInfo [size.Height * size.Width];

		if (!ReadConsoleOutput (_screenBuffer, _originalStdOutChars, coords, new Coord () { X = 0, Y = 0 }, ref window)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
	}

	public bool SetCursorPosition (Coord position)
	{
		return SetConsoleCursorPosition (_screenBuffer, position);
	}

	public void SetInitialCursorVisibility ()
	{
		if (_initialCursorVisibility.HasValue == false && GetCursorVisibility (out CursorVisibility visibility)) {
			_initialCursorVisibility = visibility;
		}
	}

	public bool GetCursorVisibility (out CursorVisibility visibility)
	{
		if (_screenBuffer == IntPtr.Zero) {
			visibility = CursorVisibility.Invisible;
			return false;
		}
		if (!GetConsoleCursorInfo (_screenBuffer, out ConsoleCursorInfo info)) {
			var err = Marshal.GetLastWin32Error ();
			if (err != 0) {
				throw new System.ComponentModel.Win32Exception (err);
			}
			visibility = Gui.CursorVisibility.Default;

			return false;
		}

		if (!info.bVisible) {
			visibility = CursorVisibility.Invisible;
		} else if (info.dwSize > 50) {
			visibility = CursorVisibility.Box;
		} else {
			visibility = CursorVisibility.Underline;
		}

		return true;
	}

	public bool EnsureCursorVisibility ()
	{
		if (_initialCursorVisibility.HasValue && _pendingCursorVisibility.HasValue && SetCursorVisibility (_pendingCursorVisibility.Value)) {
			_pendingCursorVisibility = null;

			return true;
		}

		return false;
	}

	public void ForceRefreshCursorVisibility ()
	{
		if (_currentCursorVisibility.HasValue) {
			_pendingCursorVisibility = _currentCursorVisibility;
			_currentCursorVisibility = null;
		}
	}

	public bool SetCursorVisibility (CursorVisibility visibility)
	{
		if (_initialCursorVisibility.HasValue == false) {
			_pendingCursorVisibility = visibility;

			return false;
		}

		if (_currentCursorVisibility.HasValue == false || _currentCursorVisibility.Value != visibility) {
			ConsoleCursorInfo info = new ConsoleCursorInfo {
				dwSize = (uint)visibility & 0x00FF,
				bVisible = ((uint)visibility & 0xFF00) != 0
			};

			if (!SetConsoleCursorInfo (_screenBuffer, ref info)) {
				return false;
			}

			_currentCursorVisibility = visibility;
		}

		return true;
	}

	public void Cleanup ()
	{
		if (_initialCursorVisibility.HasValue) {
			SetCursorVisibility (_initialCursorVisibility.Value);
		}

		SetConsoleOutputWindow (out _);

		ConsoleMode = _originalConsoleMode;
		if (!SetConsoleActiveScreenBuffer (_outputHandle)) {
			var err = Marshal.GetLastWin32Error ();
			Console.WriteLine ("Error: {0}", err);
		}

		if (_screenBuffer != IntPtr.Zero) {
			CloseHandle (_screenBuffer);
		}

		_screenBuffer = IntPtr.Zero;
	}

	internal Size GetConsoleBufferWindow (out Point position)
	{
		if (_screenBuffer == IntPtr.Zero) {
			position = Point.Empty;
			return Size.Empty;
		}

		var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
		csbi.cbSize = (uint)Marshal.SizeOf (csbi);
		if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
			//throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
			position = Point.Empty;
			return Size.Empty;
		}
		var sz = new Size (csbi.srWindow.Right - csbi.srWindow.Left + 1,
		    csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
		position = new Point (csbi.srWindow.Left, csbi.srWindow.Top);

		return sz;
	}

	internal Size GetConsoleOutputWindow (out Point position)
	{
		var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
		csbi.cbSize = (uint)Marshal.SizeOf (csbi);
		if (!GetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		var sz = new Size (csbi.srWindow.Right - csbi.srWindow.Left + 1,
		    csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
		position = new Point (csbi.srWindow.Left, csbi.srWindow.Top);

		return sz;
	}

	internal Size SetConsoleWindow (short cols, short rows)
	{
		var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
		csbi.cbSize = (uint)Marshal.SizeOf (csbi);

		if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		var maxWinSize = GetLargestConsoleWindowSize (_screenBuffer);
		var newCols = Math.Min (cols, maxWinSize.X);
		var newRows = Math.Min (rows, maxWinSize.Y);
		csbi.dwSize = new Coord (newCols, Math.Max (newRows, (short)1));
		csbi.srWindow = new SmallRect (0, 0, newCols, newRows);
		csbi.dwMaximumWindowSize = new Coord (newCols, newRows);
		if (!SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		var winRect = new SmallRect (0, 0, (short)(newCols - 1), (short)Math.Max (newRows - 1, 0));
		if (!SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
			//throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
			return new Size (cols, rows);
		}
		SetConsoleOutputWindow (csbi);
		return new Size (winRect.Right + 1, newRows - 1 < 0 ? 0 : winRect.Bottom + 1);
	}

	void SetConsoleOutputWindow (CONSOLE_SCREEN_BUFFER_INFOEX csbi)
	{
		if (_screenBuffer != IntPtr.Zero && !SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
	}

	internal Size SetConsoleOutputWindow (out Point position)
	{
		if (_screenBuffer == IntPtr.Zero) {
			position = Point.Empty;
			return Size.Empty;
		}

		var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
		csbi.cbSize = (uint)Marshal.SizeOf (csbi);
		if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		var sz = new Size (csbi.srWindow.Right - csbi.srWindow.Left + 1,
		    Math.Max (csbi.srWindow.Bottom - csbi.srWindow.Top + 1, 0));
		position = new Point (csbi.srWindow.Left, csbi.srWindow.Top);
		SetConsoleOutputWindow (csbi);
		var winRect = new SmallRect (0, 0, (short)(sz.Width - 1), (short)Math.Max (sz.Height - 1, 0));
		if (!SetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}
		if (!SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
			throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
		}

		return sz;
	}

	uint ConsoleMode {
		get {
			GetConsoleMode (_inputHandle, out uint v);
			return v;
		}
		set {
			SetConsoleMode (_inputHandle, value);
		}
	}

	[Flags]
	public enum ConsoleModes : uint {
		EnableProcessedInput = 1,
		EnableMouseInput = 16,
		EnableQuickEditMode = 64,
		EnableExtendedFlags = 128,
	}

	[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	public struct KeyEventRecord {
		[FieldOffset (0), MarshalAs (UnmanagedType.Bool)]
		public bool bKeyDown;
		[FieldOffset (4), MarshalAs (UnmanagedType.U2)]
		public ushort wRepeatCount;
		[FieldOffset (6), MarshalAs (UnmanagedType.U2)]
		public VK wVirtualKeyCode;
		[FieldOffset (8), MarshalAs (UnmanagedType.U2)]
		public ushort wVirtualScanCode;
		[FieldOffset (10)]
		public char UnicodeChar;
		[FieldOffset (12), MarshalAs (UnmanagedType.U4)]
		public ControlKeyState dwControlKeyState;

		public override readonly string ToString () => $"[KeyEventRecord({(bKeyDown ? "down" : "up")},{wRepeatCount},{wVirtualKeyCode},{wVirtualScanCode},{new Rune (UnicodeChar).MakePrintable ()},{dwControlKeyState})]";
	}

	[Flags]
	public enum ButtonState {
		Button1Pressed = 1,
		Button2Pressed = 4,
		Button3Pressed = 8,
		Button4Pressed = 16,
		RightmostButtonPressed = 2
	}

	[Flags]
	public enum ControlKeyState {
		RightAltPressed = 1,
		LeftAltPressed = 2,
		RightControlPressed = 4,
		LeftControlPressed = 8,
		ShiftPressed = 16,
		NumlockOn = 32,
		ScrolllockOn = 64,
		CapslockOn = 128,
		EnhancedKey = 256
	}

	[Flags]
	public enum EventFlags {
		MouseMoved = 1,
		DoubleClick = 2,
		MouseWheeled = 4,
		MouseHorizontalWheeled = 8
	}

	[StructLayout (LayoutKind.Explicit)]
	public struct MouseEventRecord {
		[FieldOffset (0)]
		public Coord MousePosition;
		[FieldOffset (4)]
		public ButtonState ButtonState;
		[FieldOffset (8)]
		public ControlKeyState ControlKeyState;
		[FieldOffset (12)]
		public EventFlags EventFlags;

		public override readonly string ToString () => $"[Mouse({MousePosition},{ButtonState},{ControlKeyState},{EventFlags}";

	}

	public struct WindowBufferSizeRecord {
		public Coord _size;

		public WindowBufferSizeRecord (short x, short y)
		{
			_size = new Coord (x, y);
		}

		public override readonly string ToString () => $"[WindowBufferSize{_size}";
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct MenuEventRecord {
		public uint dwCommandId;
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct FocusEventRecord {
		public uint bSetFocus;
	}

	public enum EventType : ushort {
		Focus = 0x10,
		Key = 0x1,
		Menu = 0x8,
		Mouse = 2,
		WindowBufferSize = 4
	}

	[StructLayout (LayoutKind.Explicit)]
	public struct InputRecord {
		[FieldOffset (0)]
		public EventType EventType;
		[FieldOffset (4)]
		public KeyEventRecord KeyEvent;
		[FieldOffset (4)]
		public MouseEventRecord MouseEvent;
		[FieldOffset (4)]
		public WindowBufferSizeRecord WindowBufferSizeEvent;
		[FieldOffset (4)]
		public MenuEventRecord MenuEvent;
		[FieldOffset (4)]
		public FocusEventRecord FocusEvent;

		public override readonly string ToString ()
		{
			return EventType switch {
				EventType.Focus => FocusEvent.ToString (),
				EventType.Key => KeyEvent.ToString (),
				EventType.Menu => MenuEvent.ToString (),
				EventType.Mouse => MouseEvent.ToString (),
				EventType.WindowBufferSize => WindowBufferSizeEvent.ToString (),
				_ => "Unknown event type: " + EventType
			};
		}
	};

	[Flags]
	enum ShareMode : uint {
		FileShareRead = 1,
		FileShareWrite = 2,
	}

	[Flags]
	enum DesiredAccess : uint {
		GenericRead = 2147483648,
		GenericWrite = 1073741824,
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ConsoleScreenBufferInfo {
		public Coord dwSize;
		public Coord dwCursorPosition;
		public ushort wAttributes;
		public SmallRect srWindow;
		public Coord dwMaximumWindowSize;
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct Coord {
		public short X;
		public short Y;

		public Coord (short x, short y)
		{
			X = x;
			Y = y;
		}
		public override readonly string ToString () => $"({X},{Y})";
	};

	[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	public struct CharUnion {
		[FieldOffset (0)] public char UnicodeChar;
		[FieldOffset (0)] public byte AsciiChar;
	}

	[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	public struct CharInfo {
		[FieldOffset (0)] public CharUnion Char;
		[FieldOffset (2)] public ushort Attributes;
	}

	public struct ExtendedCharInfo {
		public char Char { get; set; }
		public Attribute Attribute { get; set; }
		public bool Empty { get; set; } // TODO: Temp hack until virutal terminal sequences

		public ExtendedCharInfo (char character, Attribute attribute)
		{
			Char = character;
			Attribute = attribute;
			Empty = false;
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct SmallRect {
		public short Left;
		public short Top;
		public short Right;
		public short Bottom;

		public SmallRect (short left, short top, short right, short bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public static void MakeEmpty (ref SmallRect rect)
		{
			rect.Left = -1;
		}

		public static void Update (ref SmallRect rect, short col, short row)
		{
			if (rect.Left == -1) {
				rect.Left = rect.Right = col;
				rect.Bottom = rect.Top = row;
				return;
			}
			if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom)
				return;
			if (col < rect.Left)
				rect.Left = col;
			if (col > rect.Right)
				rect.Right = col;
			if (row < rect.Top)
				rect.Top = row;
			if (row > rect.Bottom)
				rect.Bottom = row;
		}

		public override readonly string ToString () => $"Left={Left},Top={Top},Right={Right},Bottom={Bottom}";
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ConsoleKeyInfoEx {
		public ConsoleKeyInfo ConsoleKeyInfo;
		public bool CapsLock;
		public bool NumLock;
		public bool ScrollLock;

		public ConsoleKeyInfoEx (ConsoleKeyInfo consoleKeyInfo, bool capslock, bool numlock, bool scrolllock)
		{
			ConsoleKeyInfo = consoleKeyInfo;
			CapsLock = capslock;
			NumLock = numlock;
			ScrollLock = scrolllock;
		}

		/// <summary>
		/// Prints a ConsoleKeyInfoEx structure
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		public readonly string ToString (ConsoleKeyInfoEx ex)
		{
			var ke = new Key ((KeyCode)ex.ConsoleKeyInfo.KeyChar);
			var sb = new StringBuilder ();
			sb.Append ($"Key: {(KeyCode)ex.ConsoleKeyInfo.Key} ({ex.ConsoleKeyInfo.Key})");
			sb.Append ((ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Shift) != 0 ? " | Shift" : string.Empty);
			sb.Append ((ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Control) != 0 ? " | Control" : string.Empty);
			sb.Append ((ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Alt) != 0 ? " | Alt" : string.Empty);
			sb.Append ($", KeyChar: {ke.AsRune.MakePrintable ()} ({(uint)ex.ConsoleKeyInfo.KeyChar}) ");
			sb.Append ((ex.CapsLock ? "caps," : string.Empty));
			sb.Append ((ex.NumLock ? "num," : string.Empty));
			sb.Append ((ex.ScrollLock ? "scroll," : string.Empty));
			var s = sb.ToString ().TrimEnd (',').TrimEnd (' ');
			return $"[ConsoleKeyInfoEx({s})]";
		}
	}


	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern IntPtr GetStdHandle (int nStdHandle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool CloseHandle (IntPtr handle);

	[DllImport ("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
	public static extern bool ReadConsoleInput (
	    IntPtr hConsoleInput,
	    IntPtr lpBuffer,
	    uint nLength,
	    out uint lpNumberOfEventsRead);

	[DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern bool ReadConsoleOutput (
	    IntPtr hConsoleOutput,
	    [Out] CharInfo [] lpBuffer,
	    Coord dwBufferSize,
	    Coord dwBufferCoord,
	    ref SmallRect lpReadRegion
	);

	// TODO: This API is obsolete. See https://learn.microsoft.com/en-us/windows/console/writeconsoleoutput
	[DllImport ("kernel32.dll", EntryPoint = "WriteConsoleOutputW", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern bool WriteConsoleOutput (
	    IntPtr hConsoleOutput,
	    CharInfo [] lpBuffer,
	    Coord dwBufferSize,
	    Coord dwBufferCoord,
	    ref SmallRect lpWriteRegion
	);

	[DllImport ("kernel32.dll", EntryPoint = "WriteConsole", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern bool WriteConsole (
		IntPtr hConsoleOutput,
		String lpbufer,
		UInt32 NumberOfCharsToWriten,
		out UInt32 lpNumberOfCharsWritten,
		object lpReserved
	    );

	[DllImport ("kernel32.dll")]
	static extern bool SetConsoleCursorPosition (IntPtr hConsoleOutput, Coord dwCursorPosition);

	[StructLayout (LayoutKind.Sequential)]
	public struct ConsoleCursorInfo {
		public uint dwSize;
		public bool bVisible;
	}

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleCursorInfo (IntPtr hConsoleOutput, [In] ref ConsoleCursorInfo lpConsoleCursorInfo);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool GetConsoleCursorInfo (IntPtr hConsoleOutput, out ConsoleCursorInfo lpConsoleCursorInfo);

	[DllImport ("kernel32.dll")]
	static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

	[DllImport ("kernel32.dll")]
	static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern IntPtr CreateConsoleScreenBuffer (
	    DesiredAccess dwDesiredAccess,
	    ShareMode dwShareMode,
	    IntPtr secutiryAttributes,
	    uint flags,
	    IntPtr screenBufferData
	);

	internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr (-1);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleActiveScreenBuffer (IntPtr Handle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool GetNumberOfConsoleInputEvents (IntPtr handle, out uint lpcNumberOfEvents);

	public InputRecord [] ReadConsoleInput ()
	{
		const int bufferSize = 1;
		var pRecord = Marshal.AllocHGlobal (Marshal.SizeOf<InputRecord> () * bufferSize);
		try {
			ReadConsoleInput (_inputHandle, pRecord, bufferSize,
			    out var numberEventsRead);

			return numberEventsRead == 0
			    ? null
			    : new [] { Marshal.PtrToStructure<InputRecord> (pRecord) };
		} catch (Exception) {
			return null;
		} finally {
			Marshal.FreeHGlobal (pRecord);
		}
	}

#if false      // Not needed on the constructor. Perhaps could be used on resizing. To study.                                                                                     
		[DllImport ("kernel32.dll", ExactSpelling = true)]
		static extern IntPtr GetConsoleWindow ();

		[DllImport ("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern bool ShowWindow (IntPtr hWnd, int nCmdShow);

		public const int HIDE = 0;
		public const int MAXIMIZE = 3;
		public const int MINIMIZE = 6;
		public const int RESTORE = 9;

		internal void ShowWindow (int state)
		{
			IntPtr thisConsole = GetConsoleWindow ();
			ShowWindow (thisConsole, state);
		}
#endif
	// See: https://github.com/gui-cs/Terminal.Gui/issues/357

	[StructLayout (LayoutKind.Sequential)]
	public struct CONSOLE_SCREEN_BUFFER_INFOEX {
		public uint cbSize;
		public Coord dwSize;
		public Coord dwCursorPosition;
		public ushort wAttributes;
		public SmallRect srWindow;
		public Coord dwMaximumWindowSize;
		public ushort wPopupAttributes;
		public bool bFullscreenSupported;

		[MarshalAs (UnmanagedType.ByValArray, SizeConst = 16)]
		public COLORREF [] ColorTable;
	}

	[StructLayout (LayoutKind.Explicit, Size = 4)]
	public struct COLORREF {
		public COLORREF (byte r, byte g, byte b)
		{
			Value = 0;
			R = r;
			G = g;
			B = b;
		}

		public COLORREF (uint value)
		{
			R = 0;
			G = 0;
			B = 0;
			Value = value & 0x00FFFFFF;
		}

		[FieldOffset (0)]
		public byte R;
		[FieldOffset (1)]
		public byte G;
		[FieldOffset (2)]
		public byte B;

		[FieldOffset (0)]
		public uint Value;
	}

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool GetConsoleScreenBufferInfoEx (IntPtr hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX csbi);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleScreenBufferInfoEx (IntPtr hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX ConsoleScreenBufferInfo);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleWindowInfo (
	    IntPtr hConsoleOutput,
	    bool bAbsolute,
	    [In] ref SmallRect lpConsoleWindow);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern Coord GetLargestConsoleWindowSize (
	    IntPtr hConsoleOutput);
}

internal class WindowsDriver : ConsoleDriver {
	WindowsConsole.ExtendedCharInfo [] _outputBuffer;
	WindowsConsole.SmallRect _damageRegion;

	public WindowsConsole WinConsole { get; private set; }

	public override bool SupportsTrueColor => RunningUnitTests || (Environment.OSVersion.Version.Build >= 14931 && _isWindowsTerminal);

	readonly bool _isWindowsTerminal = false;
	WindowsMainLoop _mainLoopDriver = null;

	public WindowsDriver ()
	{
		if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
			WinConsole = new WindowsConsole ();
			// otherwise we're probably running in unit tests
			Clipboard = new WindowsClipboard ();
		} else {
			Clipboard = new FakeDriver.FakeClipboard ();
		}

		// TODO: if some other Windows-based terminal supports true color, update this logic to not
		// force 16color mode (.e.g ConEmu which really doesn't work well at all).
		_isWindowsTerminal = _isWindowsTerminal = Environment.GetEnvironmentVariable ("WT_SESSION") != null ||
		                                          Environment.GetEnvironmentVariable ("VSAPPIDNAME") != null;
		if (!_isWindowsTerminal) {
			Force16Colors = true;
		}
	}

	internal override MainLoop Init ()
	{
		_mainLoopDriver = new WindowsMainLoop (this);
		if (!RunningUnitTests) {
			try {
				if (WinConsole != null) {
					// BUGBUG: The results from GetConsoleOutputWindow are incorrect when called from Init. 
					// Our thread in WindowsMainLoop.CheckWin will get the correct results. See #if HACK_CHECK_WINCHANGED
					var winSize = WinConsole.GetConsoleOutputWindow (out Point pos);
					Cols = winSize.Width;
					Rows = winSize.Height;
				}
				WindowsConsole.SmallRect.MakeEmpty (ref _damageRegion);

				if (_isWindowsTerminal) {
					Console.Out.Write (EscSeqUtils.CSI_SaveCursorAndActivateAltBufferNoBackscroll);
				}
			} catch (Win32Exception e) {
				// We are being run in an environment that does not support a console
				// such as a unit test, or a pipe.
				Debug.WriteLine ($"Likely running unit tests. Setting WinConsole to null so we can test it elsewhere. Exception: {e}");
				WinConsole = null;
			}
		}

		CurrentAttribute = new Attribute (Color.White, Color.Black);

		_outputBuffer = new WindowsConsole.ExtendedCharInfo [Rows * Cols];
		Clip = new Rect (0, 0, Cols, Rows);
		_damageRegion = new WindowsConsole.SmallRect () {
			Top = 0,
			Left = 0,
			Bottom = (short)Rows,
			Right = (short)Cols
		};

		ClearContents ();

#if HACK_CHECK_WINCHANGED
		_mainLoopDriver.WinChanged = ChangeWin;
#endif
		return new MainLoop (_mainLoopDriver);

	}

#if HACK_CHECK_WINCHANGED
	private void ChangeWin (Object s, SizeChangedEventArgs e)
	{
		var w = e.Size.Width;
		if (w == Cols - 3 && e.Size.Height < Rows) {
			w += 3;
		}
		Left = 0;
		Top = 0;
		Cols = e.Size.Width;
		Rows = e.Size.Height;

		if (!RunningUnitTests) {
			var newSize = WinConsole.SetConsoleWindow (
				(short)Math.Max (w, 16), (short)Math.Max (e.Size.Height, 0));

			Cols = newSize.Width;
			Rows = newSize.Height;
		}

		ResizeScreen ();
		ClearContents ();
		OnSizeChanged (new SizeChangedEventArgs (new Size (Cols, Rows)));
	}
#endif


	KeyCode MapKey (WindowsConsole.ConsoleKeyInfoEx keyInfoEx)
	{
		var keyInfo = keyInfoEx.ConsoleKeyInfo;
		switch (keyInfo.Key) {
		case ConsoleKey.D0:
		case ConsoleKey.D1:
		case ConsoleKey.D2:
		case ConsoleKey.D3:
		case ConsoleKey.D4:
		case ConsoleKey.D5:
		case ConsoleKey.D6:
		case ConsoleKey.D7:
		case ConsoleKey.D8:
		case ConsoleKey.D9:
		case ConsoleKey.NumPad0:
		case ConsoleKey.NumPad1:
		case ConsoleKey.NumPad2:
		case ConsoleKey.NumPad3:
		case ConsoleKey.NumPad4:
		case ConsoleKey.NumPad5:
		case ConsoleKey.NumPad6:
		case ConsoleKey.NumPad7:
		case ConsoleKey.NumPad8:
		case ConsoleKey.NumPad9:
		case ConsoleKey.Oem1:
		case ConsoleKey.Oem2:
		case ConsoleKey.Oem3:
		case ConsoleKey.Oem4:
		case ConsoleKey.Oem5:
		case ConsoleKey.Oem6:
		case ConsoleKey.Oem7:
		case ConsoleKey.Oem8:
		case ConsoleKey.Oem102:
		case ConsoleKey.Multiply:
		case ConsoleKey.Add:
		case ConsoleKey.Separator:
		case ConsoleKey.Subtract:
		case ConsoleKey.Decimal:
		case ConsoleKey.Divide:
		case ConsoleKey.OemPeriod:
		case ConsoleKey.OemComma:
		case ConsoleKey.OemPlus:
		case ConsoleKey.OemMinus:
			// These virtual key codes are mapped differently depending on the keyboard layout in use.
			// We use the Win32 API to map them to the correct character.
			var mapResult = MapVKtoChar ((VK)keyInfo.Key);
			if (mapResult == 0) {
				// There is no mapping - this should not happen
				Debug.Assert (mapResult != 0, $@"Unable to map the virtual key code {keyInfo.Key}.");
				return KeyCode.Null;
			}

			// An un-shifted character value is in the low order word of the return value.
			var mappedChar = (char)(mapResult & 0x0000FFFF);

			if (keyInfo.KeyChar == 0) {
				// If the keyChar is 0, keyInfo.Key value is not a printable character. 

				// Dead keys (diacritics) are indicated by setting the top bit of the return value. 
				if ((mapResult & 0x80000000) != 0) {
					// Dead key (e.g. Oem2 '~'/'^' on POR keyboard)
					// Option 1: Throw it out. 
					//    - Apps will never see the dead keys
					//    - If user presses a key that can be combined with the dead key ('a'), the right thing happens (app will see '�').
					//      - NOTE: With Dead Keys, KeyDown != KeyUp. The KeyUp event will have just the base char ('a').
					//    - If user presses dead key again, the right thing happens (app will see `~~`)
					//    - This is what Notepad etc... appear to do
					// Option 2: Expand the API to indicate the KeyCode is a dead key
					//    - Enables apps to do their own dead key processing
					//    - Adds complexity; no dev has asked for this (yet).
					// We choose Option 1 for now.
					return KeyCode.Null;

					// Note: Ctrl-Deadkey (like Oem3 '`'/'~` on ENG) can't be supported.
					// Sadly, the charVal is just the deadkey and subsequent key events do not contain
					// any info that the previous event was a deadkey.
					// Note WT does not support Ctrl-Deadkey either.
				}

				if (keyInfo.Modifiers != 0) {
					// These Oem keys have well defined chars. We ensure the representative char is used.
					// If we don't do this, then on some keyboard layouts the wrong char is 
					// returned (e.g. on ENG OemPlus un-shifted is =, not +). This is important
					// for key persistence ("Ctrl++" vs. "Ctrl+=").
					mappedChar = keyInfo.Key switch {
						ConsoleKey.OemPeriod => '.',
						ConsoleKey.OemComma => ',',
						ConsoleKey.OemPlus => '+',
						ConsoleKey.OemMinus => '-',
						_ => mappedChar
					};
				}

				// Return the mappedChar with they modifiers. Because mappedChar is un-shifted, if Shift was down
				// we should keep it
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)mappedChar);
			} else {
				// KeyChar is printable
				if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt) && keyInfo.Modifiers.HasFlag (ConsoleModifiers.Control)) {
					// AltGr support - AltGr is equivalent to Ctrl+Alt - the correct char is in KeyChar
					return (KeyCode)keyInfo.KeyChar;
				}

				if (keyInfo.Modifiers != ConsoleModifiers.Shift) {
					// If Shift wasn't down we don't need to do anything but return the mappedChar
					return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(mappedChar));
				}

				// Strip off Shift - We got here because they KeyChar from Windows is the shifted char (e.g. "�")
				// and passing on Shift would be redundant.
				return MapToKeyCodeModifiers (keyInfo.Modifiers & ~ConsoleModifiers.Shift, (KeyCode)keyInfo.KeyChar);
			}
		}

		// A..Z are special cased:
		// - Alone, they represent lowercase a...z
		// - With ShiftMask they are A..Z
		// - If CapsLock is on the above is reversed.
		// - If Alt and/or Ctrl are present, treat as upper case
		if (keyInfo.Key is >= ConsoleKey.A and <= ConsoleKey.Z) {
			if (keyInfo.KeyChar == 0) {
				// KeyChar is not printable - possibly an AltGr key?
				// AltGr support - AltGr is equivalent to Ctrl+Alt
				if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt) && keyInfo.Modifiers.HasFlag (ConsoleModifiers.Control)) {
					return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(uint)keyInfo.Key);
				}
			}

			if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt) || keyInfo.Modifiers.HasFlag (ConsoleModifiers.Control)) {
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(uint)keyInfo.Key);
			}

			if (((keyInfo.Modifiers == ConsoleModifiers.Shift) ^ (keyInfoEx.CapsLock))) {
				// If (ShiftMask is on and CapsLock is off) or (ShiftMask is off and CapsLock is on) add the ShiftMask
				if (char.IsUpper (keyInfo.KeyChar)) {
					return (KeyCode)((uint)keyInfo.Key) | KeyCode.ShiftMask;
				}
			}
			return (KeyCode)(uint)keyInfo.KeyChar;
		}

		// Handle control keys whose VK codes match the related ASCII value (those below ASCII 33) like ESC
		if (Enum.IsDefined (typeof (KeyCode), (uint)keyInfo.Key)) {
			// If the key is JUST a modifier, return it as just that key
			if (keyInfo.Key == (ConsoleKey)VK.SHIFT) { // Shift 16
				return KeyCode.ShiftMask;
			}

			if (keyInfo.Key == (ConsoleKey)VK.CONTROL) { // Ctrl 17
				return KeyCode.CtrlMask;
			}

			if (keyInfo.Key == (ConsoleKey)VK.MENU) { // Alt 18
				return KeyCode.AltMask;
			}

			if (keyInfo.KeyChar == 0) {
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(keyInfo.KeyChar));
			} else if (keyInfo.Key != ConsoleKey.None) {
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(keyInfo.KeyChar));
			} else {
				return MapToKeyCodeModifiers (keyInfo.Modifiers & ~ConsoleModifiers.Shift, (KeyCode)(keyInfo.KeyChar));
			}
		}

		// Handle control keys (e.g. CursorUp)
		if (Enum.IsDefined (typeof (KeyCode), ((uint)keyInfo.Key + (uint)KeyCode.MaxCodePoint))) {
			return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)((uint)keyInfo.Key + (uint)KeyCode.MaxCodePoint));
		}

		return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(keyInfo.KeyChar));
	}

	internal void ProcessInput (WindowsConsole.InputRecord inputEvent)
	{
		switch (inputEvent.EventType) {
		case WindowsConsole.EventType.Key:
			if (inputEvent.KeyEvent.wVirtualKeyCode == (VK)ConsoleKey.Packet) {
				// Used to pass Unicode characters as if they were keystrokes.
				// The VK_PACKET key is the low word of a 32-bit
				// Virtual Key value used for non-keyboard input methods.
				inputEvent.KeyEvent = FromVKPacketToKeyEventRecord (inputEvent.KeyEvent);
			}
			var keyInfo = ToConsoleKeyInfoEx (inputEvent.KeyEvent);
			//Debug.WriteLine ($"event: KBD: {GetKeyboardLayoutName()} {inputEvent.ToString ()} {keyInfo.ToString (keyInfo)}");

			var map = MapKey (keyInfo);

			if (map == KeyCode.Null) {
				break;
			}

			if (inputEvent.KeyEvent.bKeyDown) {
				// Avoid sending repeat key down events
				OnKeyDown (new Key (map));
			} else {
				OnKeyUp (new Key (map));
			}

			break;

		case WindowsConsole.EventType.Mouse:
			var me = ToDriverMouse (inputEvent.MouseEvent);
			OnMouseEvent (new MouseEventEventArgs (me));
			if (_processButtonClick) {
				OnMouseEvent (new MouseEventEventArgs (new MouseEvent () {
					X = me.X,
					Y = me.Y,
					Flags = ProcessButtonClick (inputEvent.MouseEvent)
				}));
			}
			break;

		case WindowsConsole.EventType.Focus:
			break;

#if !HACK_CHECK_WINCHANGED
		case WindowsConsole.EventType.WindowBufferSize:
			
			Cols = inputEvent.WindowBufferSizeEvent._size.X;
			Rows = inputEvent.WindowBufferSizeEvent._size.Y;

			ResizeScreen ();
			ClearContents ();
			TerminalResized.Invoke ();
			break;
#endif
		}
	}

	WindowsConsole.ButtonState? _lastMouseButtonPressed = null;
	bool _isButtonPressed = false;
	bool _isButtonReleased = false;
	bool _isButtonDoubleClicked = false;
	Point? _point;
	Point _pointMove;
	bool _isOneFingerDoubleClicked = false;
	bool _processButtonClick;

	MouseEvent ToDriverMouse (WindowsConsole.MouseEventRecord mouseEvent)
	{
		MouseFlags mouseFlag = MouseFlags.AllEvents;

		//System.Diagnostics.Debug.WriteLine (
		//	$"X:{mouseEvent.MousePosition.X};Y:{mouseEvent.MousePosition.Y};ButtonState:{mouseEvent.ButtonState};EventFlags:{mouseEvent.EventFlags}");

		if (_isButtonDoubleClicked || _isOneFingerDoubleClicked) {
			Application.MainLoop.AddIdle (() => {
				Task.Run (async () => await ProcessButtonDoubleClickedAsync ());
				return false;
			});
		}

		// The ButtonState member of the MouseEvent structure has bit corresponding to each mouse button.
		// This will tell when a mouse button is pressed. When the button is released this event will
		// be fired with it's bit set to 0. So when the button is up ButtonState will be 0.
		// To map to the correct driver events we save the last pressed mouse button so we can
		// map to the correct clicked event.
		if ((_lastMouseButtonPressed != null || _isButtonReleased) && mouseEvent.ButtonState != 0) {
			_lastMouseButtonPressed = null;
			//isButtonPressed = false;
			_isButtonReleased = false;
		}

		var p = new Point () {
			X = mouseEvent.MousePosition.X,
			Y = mouseEvent.MousePosition.Y
		};

		if ((mouseEvent.ButtonState != 0 && mouseEvent.EventFlags == 0 && _lastMouseButtonPressed == null && !_isButtonDoubleClicked) ||
		     (_lastMouseButtonPressed == null && mouseEvent.EventFlags.HasFlag (WindowsConsole.EventFlags.MouseMoved) &&
		     mouseEvent.ButtonState != 0 && !_isButtonReleased && !_isButtonDoubleClicked)) {
			switch (mouseEvent.ButtonState) {
			case WindowsConsole.ButtonState.Button1Pressed:
				mouseFlag = MouseFlags.Button1Pressed;
				break;

			case WindowsConsole.ButtonState.Button2Pressed:
				mouseFlag = MouseFlags.Button2Pressed;
				break;

			case WindowsConsole.ButtonState.RightmostButtonPressed:
				mouseFlag = MouseFlags.Button3Pressed;
				break;
			}

			if (_point == null) {
				_point = p;
			}

			if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) {
				mouseFlag |= MouseFlags.ReportMousePosition;
				_isButtonReleased = false;
				_processButtonClick = false;
			}
			_lastMouseButtonPressed = mouseEvent.ButtonState;
			_isButtonPressed = true;

			if ((mouseFlag & MouseFlags.ReportMousePosition) == 0) {
				Application.MainLoop.AddIdle (() => {
					Task.Run (async () => await ProcessContinuousButtonPressedAsync (mouseFlag));
					return false;
				});
			}

		} else if (_lastMouseButtonPressed != null && mouseEvent.EventFlags == 0
		      && !_isButtonReleased && !_isButtonDoubleClicked && !_isOneFingerDoubleClicked) {
			switch (_lastMouseButtonPressed) {
			case WindowsConsole.ButtonState.Button1Pressed:
				mouseFlag = MouseFlags.Button1Released;
				break;

			case WindowsConsole.ButtonState.Button2Pressed:
				mouseFlag = MouseFlags.Button2Released;
				break;

			case WindowsConsole.ButtonState.RightmostButtonPressed:
				mouseFlag = MouseFlags.Button3Released;
				break;
			}
			_isButtonPressed = false;
			_isButtonReleased = true;
			if (_point != null && (((Point)_point).X == mouseEvent.MousePosition.X && ((Point)_point).Y == mouseEvent.MousePosition.Y)) {
				_processButtonClick = true;
			} else {
				_point = null;
			}
		} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved
		      && !_isOneFingerDoubleClicked && _isButtonReleased && p == _point) {

			mouseFlag = ProcessButtonClick (mouseEvent);

		} else if (mouseEvent.EventFlags.HasFlag (WindowsConsole.EventFlags.DoubleClick)) {
			switch (mouseEvent.ButtonState) {
			case WindowsConsole.ButtonState.Button1Pressed:
				mouseFlag = MouseFlags.Button1DoubleClicked;
				break;

			case WindowsConsole.ButtonState.Button2Pressed:
				mouseFlag = MouseFlags.Button2DoubleClicked;
				break;

			case WindowsConsole.ButtonState.RightmostButtonPressed:
				mouseFlag = MouseFlags.Button3DoubleClicked;
				break;
			}
			_isButtonDoubleClicked = true;
		} else if (mouseEvent.EventFlags == 0 && mouseEvent.ButtonState != 0 && _isButtonDoubleClicked) {
			switch (mouseEvent.ButtonState) {
			case WindowsConsole.ButtonState.Button1Pressed:
				mouseFlag = MouseFlags.Button1TripleClicked;
				break;

			case WindowsConsole.ButtonState.Button2Pressed:
				mouseFlag = MouseFlags.Button2TripleClicked;
				break;

			case WindowsConsole.ButtonState.RightmostButtonPressed:
				mouseFlag = MouseFlags.Button3TripleClicked;
				break;
			}
			_isButtonDoubleClicked = false;
		} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseWheeled) {
			switch ((int)mouseEvent.ButtonState) {
			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledUp;
				break;

			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledDown;
				break;
			}

		} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseWheeled &&
		      mouseEvent.ControlKeyState == WindowsConsole.ControlKeyState.ShiftPressed) {
			switch ((int)mouseEvent.ButtonState) {
			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledLeft;
				break;

			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledRight;
				break;
			}

		} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseHorizontalWheeled) {
			switch ((int)mouseEvent.ButtonState) {
			case int v when v < 0:
				mouseFlag = MouseFlags.WheeledLeft;
				break;

			case int v when v > 0:
				mouseFlag = MouseFlags.WheeledRight;
				break;
			}

		} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) {
			mouseFlag = MouseFlags.ReportMousePosition;
			if (mouseEvent.MousePosition.X != _pointMove.X || mouseEvent.MousePosition.Y != _pointMove.Y) {
				_pointMove = new Point (mouseEvent.MousePosition.X, mouseEvent.MousePosition.Y);
			}
		} else if (mouseEvent.ButtonState == 0 && mouseEvent.EventFlags == 0) {
			mouseFlag = 0;
		}

		mouseFlag = SetControlKeyStates (mouseEvent, mouseFlag);

		//System.Diagnostics.Debug.WriteLine (
		//	$"point.X:{(point != null ? ((Point)point).X : -1)};point.Y:{(point != null ? ((Point)point).Y : -1)}");

		return new MouseEvent () {
			X = mouseEvent.MousePosition.X,
			Y = mouseEvent.MousePosition.Y,
			Flags = mouseFlag
		};
	}

	MouseFlags ProcessButtonClick (WindowsConsole.MouseEventRecord mouseEvent)
	{
		MouseFlags mouseFlag = 0;
		switch (_lastMouseButtonPressed) {
		case WindowsConsole.ButtonState.Button1Pressed:
			mouseFlag = MouseFlags.Button1Clicked;
			break;

		case WindowsConsole.ButtonState.Button2Pressed:
			mouseFlag = MouseFlags.Button2Clicked;
			break;

		case WindowsConsole.ButtonState.RightmostButtonPressed:
			mouseFlag = MouseFlags.Button3Clicked;
			break;
		}
		_point = new Point () {
			X = mouseEvent.MousePosition.X,
			Y = mouseEvent.MousePosition.Y
		};
		_lastMouseButtonPressed = null;
		_isButtonReleased = false;
		_processButtonClick = false;
		_point = null;
		return mouseFlag;
	}

	async Task ProcessButtonDoubleClickedAsync ()
	{
		await Task.Delay (300);
		_isButtonDoubleClicked = false;
		_isOneFingerDoubleClicked = false;
		//buttonPressedCount = 0;
	}

	async Task ProcessContinuousButtonPressedAsync (MouseFlags mouseFlag)
	{
		while (_isButtonPressed) {
			await Task.Delay (100);
			var me = new MouseEvent () {
				X = _pointMove.X,
				Y = _pointMove.Y,
				Flags = mouseFlag
			};

			var view = Application.WantContinuousButtonPressedView;
			if (view == null) {
				break;
			}
			if (_isButtonPressed && (mouseFlag & MouseFlags.ReportMousePosition) == 0) {
				Application.Invoke (() => OnMouseEvent (new MouseEventEventArgs (me)));
			}
		}
	}

	static MouseFlags SetControlKeyStates (WindowsConsole.MouseEventRecord mouseEvent, MouseFlags mouseFlag)
	{
		if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightControlPressed) ||
		    mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftControlPressed)) {
			mouseFlag |= MouseFlags.ButtonCtrl;
		}

		if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.ShiftPressed)) {
			mouseFlag |= MouseFlags.ButtonShift;
		}

		if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightAltPressed) ||
		     mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftAltPressed)) {
			mouseFlag |= MouseFlags.ButtonAlt;
		}
		return mouseFlag;
	}

	public WindowsConsole.ConsoleKeyInfoEx ToConsoleKeyInfoEx (WindowsConsole.KeyEventRecord keyEvent)
	{
		var state = keyEvent.dwControlKeyState;

		var shift = (state & WindowsConsole.ControlKeyState.ShiftPressed) != 0;
		var alt = (state & (WindowsConsole.ControlKeyState.LeftAltPressed | WindowsConsole.ControlKeyState.RightAltPressed)) != 0;
		var control = (state & (WindowsConsole.ControlKeyState.LeftControlPressed | WindowsConsole.ControlKeyState.RightControlPressed)) != 0;
		var capslock = (state & WindowsConsole.ControlKeyState.CapslockOn) != 0;
		var numlock = (state & WindowsConsole.ControlKeyState.NumlockOn) != 0;
		var scrolllock = (state & WindowsConsole.ControlKeyState.ScrolllockOn) != 0;

		var cki = new ConsoleKeyInfo (keyEvent.UnicodeChar, (ConsoleKey)keyEvent.wVirtualKeyCode, shift, alt, control);
		return new WindowsConsole.ConsoleKeyInfoEx (cki, capslock, numlock, scrolllock);
	}

	public WindowsConsole.KeyEventRecord FromVKPacketToKeyEventRecord (WindowsConsole.KeyEventRecord keyEvent)
	{
		if (keyEvent.wVirtualKeyCode != (VK)ConsoleKey.Packet) {
			return keyEvent;
		}

		var mod = new ConsoleModifiers ();
		if (keyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.ShiftPressed)) {
			mod |= ConsoleModifiers.Shift;
		}
		if (keyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightAltPressed) ||
		    keyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftAltPressed)) {
			mod |= ConsoleModifiers.Alt;
		}
		if (keyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftControlPressed) ||
		    keyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightControlPressed)) {
			mod |= ConsoleModifiers.Control;
		}
		var cKeyInfo = new ConsoleKeyInfo (keyEvent.UnicodeChar, (ConsoleKey)keyEvent.wVirtualKeyCode,
			mod.HasFlag (ConsoleModifiers.Shift), mod.HasFlag (ConsoleModifiers.Alt), mod.HasFlag (ConsoleModifiers.Control));
		cKeyInfo = DecodeVKPacketToKConsoleKeyInfo (cKeyInfo);
		var scanCode = GetScanCodeFromConsoleKeyInfo (cKeyInfo);

		return new WindowsConsole.KeyEventRecord {
			UnicodeChar = cKeyInfo.KeyChar,
			bKeyDown = keyEvent.bKeyDown,
			dwControlKeyState = keyEvent.dwControlKeyState,
			wRepeatCount = keyEvent.wRepeatCount,
			wVirtualKeyCode = (VK)cKeyInfo.Key,
			wVirtualScanCode = (ushort)scanCode
		};
	}

	public override bool IsRuneSupported (Rune rune)
	{
		return base.IsRuneSupported (rune) && rune.IsBmp;
	}

	void ResizeScreen ()
	{
		_outputBuffer = new WindowsConsole.ExtendedCharInfo [Rows * Cols];
		Clip = new Rect (0, 0, Cols, Rows);
		_damageRegion = new WindowsConsole.SmallRect () {
			Top = 0,
			Left = 0,
			Bottom = (short)Rows,
			Right = (short)Cols
		};
		_dirtyLines = new bool [Rows];

		WinConsole?.ForceRefreshCursorVisibility ();
	}

	public override void UpdateScreen ()
	{
		var windowSize = WinConsole?.GetConsoleBufferWindow (out var _) ?? new Size (Cols, Rows);
		if (!windowSize.IsEmpty && (windowSize.Width != Cols || windowSize.Height != Rows)) {
			return;
		}

		var bufferCoords = new WindowsConsole.Coord () {
			X = (short)Clip.Width,
			Y = (short)Clip.Height
		};

		for (int row = 0; row < Rows; row++) {
			if (!_dirtyLines [row]) {
				continue;
			}
			_dirtyLines [row] = false;

			for (int col = 0; col < Cols; col++) {
				int position = row * Cols + col;
				_outputBuffer [position].Attribute = Contents [row, col].Attribute.GetValueOrDefault ();
				if (Contents [row, col].IsDirty == false) {
					_outputBuffer [position].Empty = true;
					_outputBuffer [position].Char = (char)Rune.ReplacementChar.Value;
					continue;
				}
				_outputBuffer [position].Empty = false;
				if (Contents [row, col].Rune.IsBmp) {
					_outputBuffer [position].Char = (char)Contents [row, col].Rune.Value;
				} else {
					//_outputBuffer [position].Empty = true;
					_outputBuffer [position].Char = (char)Rune.ReplacementChar.Value;
					if (Contents [row, col].Rune.GetColumns () > 1 && col + 1 < Cols) {
						// TODO: This is a hack to deal with non-BMP and wide characters.
						col++;
						position = row * Cols + col;
						_outputBuffer [position].Empty = false;
						_outputBuffer [position].Char = ' ';
					}
				}
			}
		}

		_damageRegion = new WindowsConsole.SmallRect () {
			Top = 0,
			Left = 0,
			Bottom = (short)Rows,
			Right = (short)Cols
		};

		if (!RunningUnitTests && WinConsole != null && !WinConsole.WriteToConsole (new Size (Cols, Rows), _outputBuffer, bufferCoords, _damageRegion, Force16Colors)) {
			var err = Marshal.GetLastWin32Error ();
			if (err != 0) {
				throw new System.ComponentModel.Win32Exception (err);
			}
		}
		WindowsConsole.SmallRect.MakeEmpty (ref _damageRegion);
	}

	public override void Refresh ()
	{
		UpdateScreen ();
		WinConsole?.SetInitialCursorVisibility ();
		UpdateCursor ();
	}

	CursorVisibility _cachedCursorVisibility;

	public override void UpdateCursor ()
	{
		if (Col < 0 || Row < 0 || Col > Cols || Row > Rows) {
			GetCursorVisibility (out CursorVisibility cursorVisibility);
			_cachedCursorVisibility = cursorVisibility;
			SetCursorVisibility (CursorVisibility.Invisible);
			return;
		}

		SetCursorVisibility (_cachedCursorVisibility);
		var position = new WindowsConsole.Coord () {
			X = (short)Col,
			Y = (short)Row
		};
		WinConsole?.SetCursorPosition (position);
	}

	/// <inheritdoc/>
	public override bool GetCursorVisibility (out CursorVisibility visibility)
	{
		if (WinConsole != null) {
			return WinConsole.GetCursorVisibility (out visibility);
		}
		visibility = _cachedCursorVisibility;
		return true;
	}

	/// <inheritdoc/>
	public override bool SetCursorVisibility (CursorVisibility visibility)
	{
		_cachedCursorVisibility = visibility;
		return WinConsole == null || WinConsole.SetCursorVisibility (visibility);
	}

	/// <inheritdoc/>
	public override bool EnsureCursorVisibility ()
	{
		return WinConsole == null || WinConsole.EnsureCursorVisibility ();
	}

	public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
	{
		WindowsConsole.InputRecord input = new WindowsConsole.InputRecord {
			EventType = WindowsConsole.EventType.Key
		};

		WindowsConsole.KeyEventRecord keyEvent = new WindowsConsole.KeyEventRecord {
			bKeyDown = true
		};
		WindowsConsole.ControlKeyState controlKey = new WindowsConsole.ControlKeyState ();
		if (shift) {
			controlKey |= WindowsConsole.ControlKeyState.ShiftPressed;
			keyEvent.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = VK.SHIFT;
		}
		if (alt) {
			controlKey |= WindowsConsole.ControlKeyState.LeftAltPressed;
			controlKey |= WindowsConsole.ControlKeyState.RightAltPressed;
			keyEvent.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = VK.MENU;
		}
		if (control) {
			controlKey |= WindowsConsole.ControlKeyState.LeftControlPressed;
			controlKey |= WindowsConsole.ControlKeyState.RightControlPressed;
			keyEvent.UnicodeChar = '\0';
			keyEvent.wVirtualKeyCode = VK.CONTROL;
		}
		keyEvent.dwControlKeyState = controlKey;

		input.KeyEvent = keyEvent;

		if (shift || alt || control) {
			ProcessInput (input);
		}

		keyEvent.UnicodeChar = keyChar;
		//if ((uint)key < 255) {
		//	keyEvent.wVirtualKeyCode = (ushort)key;
		//} else {
		//	keyEvent.wVirtualKeyCode = '\0';
		//}
		keyEvent.wVirtualKeyCode = (VK)key;

		input.KeyEvent = keyEvent;

		try {
			ProcessInput (input);
		} catch (OverflowException) { } finally {
			keyEvent.bKeyDown = false;
			input.KeyEvent = keyEvent;
			ProcessInput (input);
		}
	}

	internal override void End ()
	{
		if (_mainLoopDriver != null) {
#if HACK_CHECK_WINCHANGED
			//_mainLoop.WinChanged -= ChangeWin;
#endif
		}
		_mainLoopDriver = null;

		WinConsole?.Cleanup ();
		WinConsole = null;

		if (!RunningUnitTests && _isWindowsTerminal) {
			// Disable alternative screen buffer.
			Console.Out.Write (EscSeqUtils.CSI_RestoreCursorAndRestoreAltBufferWithBackscroll);
		}
	}

	#region Not Implemented
	public override void Suspend ()
	{
		throw new NotImplementedException ();
	}
	#endregion
}

/// <summary>
/// Mainloop intended to be used with the <see cref="WindowsDriver"/>, and can
/// only be used on Windows.
/// </summary>
/// <remarks>
/// This implementation is used for WindowsDriver.
/// </remarks>
internal class WindowsMainLoop : IMainLoopDriver {
	readonly ManualResetEventSlim _eventReady = new ManualResetEventSlim (false);
	readonly ManualResetEventSlim _waitForProbe = new ManualResetEventSlim (false);
	MainLoop _mainLoop;
	readonly ConsoleDriver _consoleDriver;
	readonly WindowsConsole _winConsole;
	CancellationTokenSource _eventReadyTokenSource = new CancellationTokenSource ();
	CancellationTokenSource _inputHandlerTokenSource = new CancellationTokenSource ();

	// The records that we keep fetching
	readonly Queue<WindowsConsole.InputRecord []> _resultQueue = new Queue<WindowsConsole.InputRecord []> ();

	/// <summary>
	/// Invoked when the window is changed.
	/// </summary>
	public EventHandler<SizeChangedEventArgs> WinChanged;

	public WindowsMainLoop (ConsoleDriver consoleDriver = null)
	{
		_consoleDriver = consoleDriver ?? throw new ArgumentNullException (nameof (consoleDriver));
		_winConsole = ((WindowsDriver)consoleDriver).WinConsole;
	}

	void IMainLoopDriver.Setup (MainLoop mainLoop)
	{
		_mainLoop = mainLoop;
		Task.Run (WindowsInputHandler, _inputHandlerTokenSource.Token);
#if HACK_CHECK_WINCHANGED
		Task.Run (CheckWinChange);
#endif
	}

	void WindowsInputHandler ()
	{
		while (_mainLoop != null) {
			try {
				if (!_inputHandlerTokenSource.IsCancellationRequested) {
					_waitForProbe.Wait (_inputHandlerTokenSource.Token);
				}

			} catch (OperationCanceledException) {
				return;
			} finally {
				_waitForProbe.Reset ();
			}

			if (_resultQueue?.Count == 0) {
				_resultQueue.Enqueue (_winConsole.ReadConsoleInput ());
			}

			_eventReady.Set ();
		}
	}

#if HACK_CHECK_WINCHANGED
	readonly ManualResetEventSlim _winChange = new ManualResetEventSlim (false);
	bool _winChanged;
	Size _windowSize;
	void CheckWinChange ()
	{
		while (_mainLoop != null) {
			_winChange.Wait ();
			_winChange.Reset ();

			// Check if the window size changed every half second. 
			// We do this to minimize the weird tearing seen on Windows when resizing the console
			// TODO: Do this in a timer or some other alternative, rather than hot-looping with a sync pause
			while (_mainLoop != null) {
				// ReSharper disable once AsyncApostle.AsyncWait
				Task.Delay (500).Wait ();
				_windowSize = _winConsole.GetConsoleBufferWindow (out _);
				if (_windowSize != Size.Empty && (_windowSize.Width != _consoleDriver.Cols
								|| _windowSize.Height != _consoleDriver.Rows)) {
					break;
				}
			}

			_winChanged = true;
			_eventReady.Set ();
		}
	}
#endif

	void IMainLoopDriver.Wakeup ()
	{
		_eventReady.Set ();
	}

	bool IMainLoopDriver.EventsPending ()
	{
		_waitForProbe.Set ();
#if HACK_CHECK_WINCHANGED
		_winChange.Set ();
#endif
		if (_mainLoop.CheckTimersAndIdleHandlers (out var waitTimeout)) {
			return true;
		}

		try {
			if (!_eventReadyTokenSource.IsCancellationRequested) {
				// Note: ManualResetEventSlim.Wait will wait indefinitely if the timeout is -1. The timeout is -1 when there
				// are no timers, but there IS an idle handler waiting.
				_eventReady.Wait (waitTimeout, _eventReadyTokenSource.Token);
			}
		} catch (OperationCanceledException) {
			return true;
		} finally {
			_eventReady.Reset ();
		}

		if (!_eventReadyTokenSource.IsCancellationRequested) {
#if HACK_CHECK_WINCHANGED
			return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _) || _winChanged;
#else
			return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _);
#endif
		}

		_eventReadyTokenSource.Dispose ();
		_eventReadyTokenSource = new CancellationTokenSource ();
		return true;
	}

	void IMainLoopDriver.Iteration ()
	{
		while (_resultQueue.Count > 0) {
			var inputRecords = _resultQueue.Dequeue ();
			if (inputRecords is { Length: > 0 }) {
				((WindowsDriver)_consoleDriver).ProcessInput (inputRecords [0]);
			}
		}
#if HACK_CHECK_WINCHANGED
		if (_winChanged) {
			_winChanged = false;
			WinChanged?.Invoke (this, new SizeChangedEventArgs (_windowSize));
		}
#endif
	}

	void IMainLoopDriver.TearDown ()
	{
		_inputHandlerTokenSource?.Cancel ();
		_inputHandlerTokenSource?.Dispose ();

		_eventReadyTokenSource?.Cancel ();
		_eventReadyTokenSource?.Dispose ();
		_eventReady?.Dispose ();

		_resultQueue?.Clear ();

#if HACK_CHECK_WINCHANGED
		_winChange?.Dispose ();
#endif
		//_waitForProbe?.Dispose ();

		_mainLoop = null;
	}
}

class WindowsClipboard : ClipboardBase {
	public WindowsClipboard ()
	{
		IsSupported = IsClipboardFormatAvailable (_cfUnicodeText);
	}

	public override bool IsSupported { get; }

	protected override string GetClipboardDataImpl ()
	{
		try {
			if (!OpenClipboard (IntPtr.Zero)) {
				return string.Empty;
			}

			IntPtr handle = GetClipboardData (_cfUnicodeText);
			if (handle == IntPtr.Zero) {
				return string.Empty;
			}

			IntPtr pointer = IntPtr.Zero;

			try {
				pointer = GlobalLock (handle);
				if (pointer == IntPtr.Zero) {
					return string.Empty;
				}

				int size = GlobalSize (handle);
				byte [] buff = new byte [size];

				Marshal.Copy (pointer, buff, 0, size);

				return System.Text.Encoding.Unicode.GetString (buff).TrimEnd ('\0');
			} finally {
				if (pointer != IntPtr.Zero) {
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
		IntPtr hGlobal = default;
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
	static extern int GlobalSize (IntPtr handle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern IntPtr GlobalLock (IntPtr hMem);

	[DllImport ("kernel32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool GlobalUnlock (IntPtr hMem);

	[DllImport ("user32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool OpenClipboard (IntPtr hWndNewOwner);

	[DllImport ("user32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	static extern bool CloseClipboard ();

	[DllImport ("user32.dll", SetLastError = true)]
	static extern IntPtr SetClipboardData (uint uFormat, IntPtr data);

	[DllImport ("user32.dll")]
	static extern bool EmptyClipboard ();

	[DllImport ("user32.dll", SetLastError = true)]
	static extern IntPtr GetClipboardData (uint uFormat);
}