#nullable enable
// //
// WindowsDriver.cs: Windows specific driver
//

// HACK:
// WindowsConsole/Terminal has two issues:
// 1) Tearing can occur when the console is resized.
// 2) The values provided during Init (and the first WindowsConsole.EventType.WindowBufferSize) are not correct.
//
// If HACK_CHECK_WINCHANGED is defined then we ignore WindowsConsole.EventType.WindowBufferSize events
// and instead check the console size every 500ms in a thread in WidowsMainLoop. 
// As of Windows 11 23H2 25947.1000 and/or WT 1.19.2682 tearing no longer occurs when using 
// the WindowsConsole.EventType.WindowBufferSize event. However, on Init the window size is
// still incorrect, so we still need this hack.
// HACK_CHECK_WINCHANGED is automatically defined for the project if the MSBuild OS property begins with "win".

using System.ComponentModel;
using System.Diagnostics;
using static Terminal.Gui.ConsoleDrivers.ConsoleKeyMapping;

namespace Terminal.Gui.ConsoleDrivers;

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
					//    - If user presses a key that can be combined with the dead key ('a'), the right thing happens (app will see 'ã').
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

				// Strip off Shift - We got here because they KeyChar from Windows is the shifted char (e.g. "Ç")
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