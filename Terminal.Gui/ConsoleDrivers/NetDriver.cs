﻿//
// NetDriver.cs: The System.Console-based .NET driver, works on Windows and Unix, but is not particularly efficient.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Terminal.Gui.ConsoleDrivers.ConsoleKeyMapping;
using static Terminal.Gui.NetEvents;

namespace Terminal.Gui;

class NetWinVTConsole {

	const int STD_INPUT_HANDLE = -10;
	const int STD_OUTPUT_HANDLE = -11;
	const int STD_ERROR_HANDLE = -12;

	// Input modes.
	const uint ENABLE_PROCESSED_INPUT = 1;
	const uint ENABLE_LINE_INPUT = 2;
	const uint ENABLE_ECHO_INPUT = 4;
	const uint ENABLE_WINDOW_INPUT = 8;
	const uint ENABLE_MOUSE_INPUT = 16;
	const uint ENABLE_INSERT_MODE = 32;
	const uint ENABLE_QUICK_EDIT_MODE = 64;
	const uint ENABLE_EXTENDED_FLAGS = 128;
	const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 512;

	// Output modes.
	const uint ENABLE_PROCESSED_OUTPUT = 1;
	const uint ENABLE_WRAP_AT_EOL_OUTPUT = 2;
	const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
	const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
	const uint ENABLE_LVB_GRID_WORLDWIDE = 10;
	readonly IntPtr _inputHandle;
	readonly IntPtr _outputHandle;
	readonly IntPtr _errorHandle;
	readonly uint _originalInputConsoleMode;
	readonly uint _originalOutputConsoleMode;
	readonly uint _originalErrorConsoleMode;

	public NetWinVTConsole ()
	{
		_inputHandle = GetStdHandle (STD_INPUT_HANDLE);
		if (!GetConsoleMode (_inputHandle, out var mode)) {
			throw new ApplicationException ($"Failed to get input console mode, error code: {GetLastError ()}.");
		}
		_originalInputConsoleMode = mode;
		if ((mode & ENABLE_VIRTUAL_TERMINAL_INPUT) < ENABLE_VIRTUAL_TERMINAL_INPUT) {
			mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
			if (!SetConsoleMode (_inputHandle, mode)) {
				throw new ApplicationException ($"Failed to set input console mode, error code: {GetLastError ()}.");
			}
		}

		_outputHandle = GetStdHandle (STD_OUTPUT_HANDLE);
		if (!GetConsoleMode (_outputHandle, out mode)) {
			throw new ApplicationException ($"Failed to get output console mode, error code: {GetLastError ()}.");
		}
		_originalOutputConsoleMode = mode;
		if ((mode & (ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN)) < DISABLE_NEWLINE_AUTO_RETURN) {
			mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
			if (!SetConsoleMode (_outputHandle, mode)) {
				throw new ApplicationException ($"Failed to set output console mode, error code: {GetLastError ()}.");
			}
		}

		_errorHandle = GetStdHandle (STD_ERROR_HANDLE);
		if (!GetConsoleMode (_errorHandle, out mode)) {
			throw new ApplicationException ($"Failed to get error console mode, error code: {GetLastError ()}.");
		}
		_originalErrorConsoleMode = mode;
		if ((mode & DISABLE_NEWLINE_AUTO_RETURN) < DISABLE_NEWLINE_AUTO_RETURN) {
			mode |= DISABLE_NEWLINE_AUTO_RETURN;
			if (!SetConsoleMode (_errorHandle, mode)) {
				throw new ApplicationException ($"Failed to set error console mode, error code: {GetLastError ()}.");
			}
		}
	}

	public void Cleanup ()
	{
		if (!SetConsoleMode (_inputHandle, _originalInputConsoleMode)) {
			throw new ApplicationException ($"Failed to restore input console mode, error code: {GetLastError ()}.");
		}
		if (!SetConsoleMode (_outputHandle, _originalOutputConsoleMode)) {
			throw new ApplicationException ($"Failed to restore output console mode, error code: {GetLastError ()}.");
		}
		if (!SetConsoleMode (_errorHandle, _originalErrorConsoleMode)) {
			throw new ApplicationException ($"Failed to restore error console mode, error code: {GetLastError ()}.");
		}
	}

	[DllImport ("kernel32.dll", SetLastError = true)]
	extern static IntPtr GetStdHandle (int nStdHandle);

	[DllImport ("kernel32.dll")]
	extern static bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

	[DllImport ("kernel32.dll")]
	extern static bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

	[DllImport ("kernel32.dll")]
	extern static uint GetLastError ();
}

class NetEvents : IDisposable {
	readonly ManualResetEventSlim _inputReady = new (false);
	CancellationTokenSource _inputReadyCancellationTokenSource;

	readonly ManualResetEventSlim _waitForStart = new (false);
	//CancellationTokenSource _waitForStartCancellationTokenSource;

	readonly ManualResetEventSlim _winChange = new (false);

	readonly Queue<InputResult?> _inputQueue = new ();

	readonly ConsoleDriver _consoleDriver;
	ConsoleKeyInfo [] _cki;
	bool _isEscSeq;

#if PROCESS_REQUEST
		bool _neededProcessRequest;
#endif
	public EscSeqRequests EscSeqRequests { get; } = new ();

	public NetEvents (ConsoleDriver consoleDriver)
	{
		_consoleDriver = consoleDriver ?? throw new ArgumentNullException (nameof (consoleDriver));
		_inputReadyCancellationTokenSource = new CancellationTokenSource ();

		Task.Run (ProcessInputQueue, _inputReadyCancellationTokenSource.Token);

		Task.Run (CheckWindowSizeChange, _inputReadyCancellationTokenSource.Token);
	}

	public InputResult? DequeueInput ()
	{
		while (_inputReadyCancellationTokenSource != null && !_inputReadyCancellationTokenSource.Token.IsCancellationRequested) {
			_waitForStart.Set ();
			_winChange.Set ();

			try {
				if (!_inputReadyCancellationTokenSource.Token.IsCancellationRequested) {
					if (_inputQueue.Count == 0) {
						_inputReady.Wait (_inputReadyCancellationTokenSource.Token);
					}
				}

			} catch (OperationCanceledException) {
				return null;
			} finally {
				_inputReady.Reset ();
			}

#if PROCESS_REQUEST
				_neededProcessRequest = false;
#endif
			if (_inputQueue.Count > 0) {
				return _inputQueue.Dequeue ();
			}
		}
		return null;
	}

	static ConsoleKeyInfo ReadConsoleKeyInfo (CancellationToken cancellationToken, bool intercept = true)
	{
		// if there is a key available, return it without waiting
		//  (or dispatching work to the thread queue)
		if (Console.KeyAvailable) {
			return Console.ReadKey (intercept);
		}

		while (!cancellationToken.IsCancellationRequested) {
			Task.Delay (100);
			if (Console.KeyAvailable) {
				return Console.ReadKey (intercept);
			}
		}
		cancellationToken.ThrowIfCancellationRequested ();
		return default;
	}

	void ProcessInputQueue ()
	{
		while (!_inputReadyCancellationTokenSource.Token.IsCancellationRequested) {

			try {
				_waitForStart.Wait (_inputReadyCancellationTokenSource.Token);
			} catch (OperationCanceledException) {

				return;
			}
			_waitForStart.Reset ();

			if (_inputQueue.Count == 0) {
				ConsoleKey key = 0;
				ConsoleModifiers mod = 0;
				ConsoleKeyInfo newConsoleKeyInfo = default;

				while (true) {
					if (_inputReadyCancellationTokenSource.Token.IsCancellationRequested) {
						return;
					}
					ConsoleKeyInfo consoleKeyInfo;
					try {
						consoleKeyInfo = ReadConsoleKeyInfo (_inputReadyCancellationTokenSource.Token);
					} catch (OperationCanceledException) {
						return;
					}
					if (consoleKeyInfo.KeyChar == (char)KeyCode.Esc && !_isEscSeq
					    || consoleKeyInfo.KeyChar != (char)KeyCode.Esc && _isEscSeq) {

						if (_cki == null && consoleKeyInfo.KeyChar != (char)KeyCode.Esc && _isEscSeq) {
							_cki = EscSeqUtils.ResizeArray (new ConsoleKeyInfo ((char)KeyCode.Esc, 0,
								false, false, false), _cki);
						}
						_isEscSeq = true;
						newConsoleKeyInfo = consoleKeyInfo;
						_cki = EscSeqUtils.ResizeArray (consoleKeyInfo, _cki);
						if (Console.KeyAvailable) {
							continue;
						}
						ProcessRequestResponse (ref newConsoleKeyInfo, ref key, _cki, ref mod);
						_cki = null;
						_isEscSeq = false;
						break;
					}
					if (consoleKeyInfo.KeyChar == (char)KeyCode.Esc && _isEscSeq && _cki != null) {
						ProcessRequestResponse (ref newConsoleKeyInfo, ref key, _cki, ref mod);
						_cki = null;
						if (Console.KeyAvailable) {
							_cki = EscSeqUtils.ResizeArray (consoleKeyInfo, _cki);
						} else {
							ProcessMapConsoleKeyInfo (consoleKeyInfo);
						}
						break;
					}
					ProcessMapConsoleKeyInfo (consoleKeyInfo);
					break;
				}
			}

			_inputReady.Set ();
		}

		void ProcessMapConsoleKeyInfo (ConsoleKeyInfo consoleKeyInfo)
		{
			_inputQueue.Enqueue (new InputResult {
				EventType = EventType.Key,
				ConsoleKeyInfo = EscSeqUtils.MapConsoleKeyInfo (consoleKeyInfo)
			});
			_isEscSeq = false;
		}
	}

	void CheckWindowSizeChange ()
	{
		void RequestWindowSize (CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested) {
				// Wait for a while then check if screen has changed sizes
				Task.Delay (500, cancellationToken);

				int buffHeight, buffWidth;
				if (((NetDriver)_consoleDriver).IsWinPlatform) {
					buffHeight = Math.Max (Console.BufferHeight, 0);
					buffWidth = Math.Max (Console.BufferWidth, 0);
				} else {
					buffHeight = _consoleDriver.Rows;
					buffWidth = _consoleDriver.Cols;
				}
				if (EnqueueWindowSizeEvent (
					Math.Max (Console.WindowHeight, 0),
					Math.Max (Console.WindowWidth, 0),
					buffHeight,
					buffWidth)) {

					return;
				}
			}
			cancellationToken.ThrowIfCancellationRequested ();
		}

		while (true) {
			if (_inputReadyCancellationTokenSource.IsCancellationRequested) {
				return;
			}
			_winChange.Wait (_inputReadyCancellationTokenSource.Token);
			_winChange.Reset ();
			try {
				RequestWindowSize (_inputReadyCancellationTokenSource.Token);
			} catch (OperationCanceledException) {
				return;
			}
			_inputReady.Set ();
		}
	}

	/// <summary>
	/// Enqueue a window size event if the window size has changed.
	/// </summary>
	/// <param name="winHeight"></param>
	/// <param name="winWidth"></param>
	/// <param name="buffHeight"></param>
	/// <param name="buffWidth"></param>
	/// <returns></returns>
	bool EnqueueWindowSizeEvent (int winHeight, int winWidth, int buffHeight, int buffWidth)
	{
		if (winWidth == _consoleDriver.Cols && winHeight == _consoleDriver.Rows) {
			return false;
		}
		var w = Math.Max (winWidth, 0);
		var h = Math.Max (winHeight, 0);
		_inputQueue.Enqueue (new InputResult {
			EventType = EventType.WindowSize,
			WindowSizeEvent = new WindowSizeEvent {
				Size = new Size (w, h)
			}
		});
		return true;
	}

	// Process a CSI sequence received by the driver (key pressed, mouse event, or request/response event)
	void ProcessRequestResponse (ref ConsoleKeyInfo newConsoleKeyInfo, ref ConsoleKey key, ConsoleKeyInfo [] cki, ref ConsoleModifiers mod)
	{
		// isMouse is true if it's CSI<, false otherwise
		EscSeqUtils.DecodeEscSeq (EscSeqRequests, ref newConsoleKeyInfo, ref key, cki, ref mod,
			out var c1Control, out var code, out var values, out var terminating,
			out var isMouse, out var mouseFlags,
			out var pos, out var isReq,
			(f, p) => HandleMouseEvent (MapMouseFlags (f), p));

		if (isMouse) {
			foreach (var mf in mouseFlags) {
				HandleMouseEvent (MapMouseFlags (mf), pos);
			}
			return;
		}
		if (isReq) {
			HandleRequestResponseEvent (c1Control, code, values, terminating);
			return;
		}
		HandleKeyboardEvent (newConsoleKeyInfo);
	}

	MouseButtonState MapMouseFlags (MouseFlags mouseFlags)
	{
		MouseButtonState mbs = default;
		foreach (var flag in Enum.GetValues (mouseFlags.GetType ())) {
			if (mouseFlags.HasFlag ((MouseFlags)flag)) {
				switch (flag) {
				case MouseFlags.Button1Pressed:
					mbs |= MouseButtonState.Button1Pressed;
					break;
				case MouseFlags.Button1Released:
					mbs |= MouseButtonState.Button1Released;
					break;
				case MouseFlags.Button1Clicked:
					mbs |= MouseButtonState.Button1Clicked;
					break;
				case MouseFlags.Button1DoubleClicked:
					mbs |= MouseButtonState.Button1DoubleClicked;
					break;
				case MouseFlags.Button1TripleClicked:
					mbs |= MouseButtonState.Button1TripleClicked;
					break;
				case MouseFlags.Button2Pressed:
					mbs |= MouseButtonState.Button2Pressed;
					break;
				case MouseFlags.Button2Released:
					mbs |= MouseButtonState.Button2Released;
					break;
				case MouseFlags.Button2Clicked:
					mbs |= MouseButtonState.Button2Clicked;
					break;
				case MouseFlags.Button2DoubleClicked:
					mbs |= MouseButtonState.Button2DoubleClicked;
					break;
				case MouseFlags.Button2TripleClicked:
					mbs |= MouseButtonState.Button2TripleClicked;
					break;
				case MouseFlags.Button3Pressed:
					mbs |= MouseButtonState.Button3Pressed;
					break;
				case MouseFlags.Button3Released:
					mbs |= MouseButtonState.Button3Released;
					break;
				case MouseFlags.Button3Clicked:
					mbs |= MouseButtonState.Button3Clicked;
					break;
				case MouseFlags.Button3DoubleClicked:
					mbs |= MouseButtonState.Button3DoubleClicked;
					break;
				case MouseFlags.Button3TripleClicked:
					mbs |= MouseButtonState.Button3TripleClicked;
					break;
				case MouseFlags.WheeledUp:
					mbs |= MouseButtonState.ButtonWheeledUp;
					break;
				case MouseFlags.WheeledDown:
					mbs |= MouseButtonState.ButtonWheeledDown;
					break;
				case MouseFlags.WheeledLeft:
					mbs |= MouseButtonState.ButtonWheeledLeft;
					break;
				case MouseFlags.WheeledRight:
					mbs |= MouseButtonState.ButtonWheeledRight;
					break;
				case MouseFlags.Button4Pressed:
					mbs |= MouseButtonState.Button4Pressed;
					break;
				case MouseFlags.Button4Released:
					mbs |= MouseButtonState.Button4Released;
					break;
				case MouseFlags.Button4Clicked:
					mbs |= MouseButtonState.Button4Clicked;
					break;
				case MouseFlags.Button4DoubleClicked:
					mbs |= MouseButtonState.Button4DoubleClicked;
					break;
				case MouseFlags.Button4TripleClicked:
					mbs |= MouseButtonState.Button4TripleClicked;
					break;
				case MouseFlags.ButtonShift:
					mbs |= MouseButtonState.ButtonShift;
					break;
				case MouseFlags.ButtonCtrl:
					mbs |= MouseButtonState.ButtonCtrl;
					break;
				case MouseFlags.ButtonAlt:
					mbs |= MouseButtonState.ButtonAlt;
					break;
				case MouseFlags.ReportMousePosition:
					mbs |= MouseButtonState.ReportMousePosition;
					break;
				case MouseFlags.AllEvents:
					mbs |= MouseButtonState.AllEvents;
					break;
				}
			}
		}
		return mbs;
	}

	Point _lastCursorPosition;

	void HandleRequestResponseEvent (string c1Control, string code, string [] values, string terminating)
	{
		switch (terminating) {
		// BUGBUG: I can't find where we send a request for cursor position (ESC[?6n), so I'm not sure if this is needed.
		case EscSeqUtils.CSI_RequestCursorPositionReport_Terminator:
			var point = new Point {
				X = int.Parse (values [1]) - 1,
				Y = int.Parse (values [0]) - 1
			};
			if (_lastCursorPosition.Y != point.Y) {
				_lastCursorPosition = point;
				var eventType = EventType.WindowPosition;
				var winPositionEv = new WindowPositionEvent {
					CursorPosition = point
				};
				_inputQueue.Enqueue (new InputResult {
					EventType = eventType,
					WindowPositionEvent = winPositionEv
				});
			} else {
				return;
			}
			break;

		case EscSeqUtils.CSI_ReportTerminalSizeInChars_Terminator:
			switch (values [0]) {
			case EscSeqUtils.CSI_ReportTerminalSizeInChars_ResponseValue:
				EnqueueWindowSizeEvent (
					Math.Max (int.Parse (values [1]), 0),
					Math.Max (int.Parse (values [2]), 0),
					Math.Max (int.Parse (values [1]), 0),
					Math.Max (int.Parse (values [2]), 0));
				break;
			default:
				EnqueueRequestResponseEvent (c1Control, code, values, terminating);
				break;
			}
			break;
		default:
			EnqueueRequestResponseEvent (c1Control, code, values, terminating);
			break;
		}

		_inputReady.Set ();
	}

	void EnqueueRequestResponseEvent (string c1Control, string code, string [] values, string terminating)
	{
		var eventType = EventType.RequestResponse;
		var requestRespEv = new RequestResponseEvent {
			ResultTuple = (c1Control, code, values, terminating)
		};
		_inputQueue.Enqueue (new InputResult {
			EventType = eventType,
			RequestResponseEvent = requestRespEv
		});
	}

	void HandleMouseEvent (MouseButtonState buttonState, Point pos)
	{
		var mouseEvent = new MouseEvent {
			Position = pos,
			ButtonState = buttonState
		};

		_inputQueue.Enqueue (new InputResult {
			EventType = EventType.Mouse,
			MouseEvent = mouseEvent
		});

		_inputReady.Set ();
	}

	public enum EventType {
		Key = 1,
		Mouse = 2,
		WindowSize = 3,
		WindowPosition = 4,
		RequestResponse = 5
	}

	[Flags]
	public enum MouseButtonState {
		Button1Pressed = 0x1,
		Button1Released = 0x2,
		Button1Clicked = 0x4,
		Button1DoubleClicked = 0x8,
		Button1TripleClicked = 0x10,
		Button2Pressed = 0x20,
		Button2Released = 0x40,
		Button2Clicked = 0x80,
		Button2DoubleClicked = 0x100,
		Button2TripleClicked = 0x200,
		Button3Pressed = 0x400,
		Button3Released = 0x800,
		Button3Clicked = 0x1000,
		Button3DoubleClicked = 0x2000,
		Button3TripleClicked = 0x4000,
		ButtonWheeledUp = 0x8000,
		ButtonWheeledDown = 0x10000,
		ButtonWheeledLeft = 0x20000,
		ButtonWheeledRight = 0x40000,
		Button4Pressed = 0x80000,
		Button4Released = 0x100000,
		Button4Clicked = 0x200000,
		Button4DoubleClicked = 0x400000,
		Button4TripleClicked = 0x800000,
		ButtonShift = 0x1000000,
		ButtonCtrl = 0x2000000,
		ButtonAlt = 0x4000000,
		ReportMousePosition = 0x8000000,
		AllEvents = -1
	}

	public struct MouseEvent {
		public Point Position;
		public MouseButtonState ButtonState;
	}

	public struct WindowSizeEvent {
		public Size Size;
	}

	public struct WindowPositionEvent {
		public int Top;
		public int Left;
		public Point CursorPosition;
	}

	public struct RequestResponseEvent {
		public (string c1Control, string code, string [] values, string terminating) ResultTuple;
	}

	public struct InputResult {
		public EventType EventType;
		public ConsoleKeyInfo ConsoleKeyInfo;
		public MouseEvent MouseEvent;
		public WindowSizeEvent WindowSizeEvent;
		public WindowPositionEvent WindowPositionEvent;
		public RequestResponseEvent RequestResponseEvent;

		public override readonly string ToString () => EventType switch {
			EventType.Key => ToString (ConsoleKeyInfo),
			EventType.Mouse => MouseEvent.ToString (),
			//EventType.WindowSize => WindowSize.ToString (),
			//EventType.RequestResponse => RequestResponse.ToString (),
			_ => "Unknown event type: " + EventType
		};

		/// <summary>
		/// Prints a ConsoleKeyInfoEx structure
		/// </summary>
		/// <param name="cki"></param>
		/// <returns></returns>
		public readonly string ToString (ConsoleKeyInfo cki)
		{
			var ke = new Key ((KeyCode)cki.KeyChar);
			var sb = new StringBuilder ();
			sb.Append ($"Key: {(KeyCode)cki.Key} ({cki.Key})");
			sb.Append ((cki.Modifiers & ConsoleModifiers.Shift) != 0 ? " | Shift" : string.Empty);
			sb.Append ((cki.Modifiers & ConsoleModifiers.Control) != 0 ? " | Control" : string.Empty);
			sb.Append ((cki.Modifiers & ConsoleModifiers.Alt) != 0 ? " | Alt" : string.Empty);
			sb.Append ($", KeyChar: {ke.AsRune.MakePrintable ()} ({(uint)cki.KeyChar}) ");
			var s = sb.ToString ().TrimEnd (',').TrimEnd (' ');
			return $"[ConsoleKeyInfo({s})]";
		}
	}

	void HandleKeyboardEvent (ConsoleKeyInfo cki)
	{
		var inputResult = new InputResult {
			EventType = EventType.Key,
			ConsoleKeyInfo = cki
		};

		_inputQueue.Enqueue (inputResult);
	}

	public void Dispose ()
	{
		_inputReadyCancellationTokenSource?.Cancel ();
		_inputReadyCancellationTokenSource?.Dispose ();
		_inputReadyCancellationTokenSource = null;

		try {
			// throws away any typeahead that has been typed by
			// the user and has not yet been read by the program.
			while (Console.KeyAvailable) {
				Console.ReadKey (true);
			}
		} catch (InvalidOperationException) {
			// Ignore - Console input has already been closed
		}
	}
}

class NetDriver : ConsoleDriver {
	const int COLOR_BLACK = 30;
	const int COLOR_RED = 31;
	const int COLOR_GREEN = 32;
	const int COLOR_YELLOW = 33;
	const int COLOR_BLUE = 34;
	const int COLOR_MAGENTA = 35;
	const int COLOR_CYAN = 36;
	const int COLOR_WHITE = 37;
	const int COLOR_BRIGHT_BLACK = 90;
	const int COLOR_BRIGHT_RED = 91;
	const int COLOR_BRIGHT_GREEN = 92;
	const int COLOR_BRIGHT_YELLOW = 93;
	const int COLOR_BRIGHT_BLUE = 94;
	const int COLOR_BRIGHT_MAGENTA = 95;
	const int COLOR_BRIGHT_CYAN = 96;
	const int COLOR_BRIGHT_WHITE = 97;

	NetMainLoop _mainLoopDriver;

	public override bool SupportsTrueColor => Environment.OSVersion.Platform == PlatformID.Unix || IsWinPlatform && Environment.OSVersion.Version.Build >= 14931;

	public NetWinVTConsole NetWinConsole { get; private set; }

	public bool IsWinPlatform { get; private set; }

	internal override MainLoop Init ()
	{
		var p = Environment.OSVersion.Platform;
		if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows) {
			IsWinPlatform = true;
			try {
				NetWinConsole = new NetWinVTConsole ();
			} catch (ApplicationException) {
				// Likely running as a unit test, or in a non-interactive session.
			}
		}
		if (IsWinPlatform) {
			Clipboard = new WindowsClipboard ();
		} else if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX)) {
			Clipboard = new MacOSXClipboard ();
		} else {
			if (CursesDriver.Is_WSL_Platform ()) {
				Clipboard = new WSLClipboard ();
			} else {
				Clipboard = new CursesClipboard ();
			}
		}

		if (!RunningUnitTests) {
			Console.TreatControlCAsInput = true;

			Cols = Console.WindowWidth;
			Rows = Console.WindowHeight;

			//Enable alternative screen buffer.
			Console.Out.Write (EscSeqUtils.CSI_SaveCursorAndActivateAltBufferNoBackscroll);

			//Set cursor key to application.
			Console.Out.Write (EscSeqUtils.CSI_HideCursor);

		} else {
			// We are being run in an environment that does not support a console
			// such as a unit test, or a pipe.
			Cols = 80;
			Rows = 24;
		}

		ResizeScreen ();
		ClearContents ();
		CurrentAttribute = new Attribute (Color.White, Color.Black);

		StartReportingMouseMoves ();

		_mainLoopDriver = new NetMainLoop (this);
		_mainLoopDriver.ProcessInput = ProcessInput;
		return new MainLoop (_mainLoopDriver);
	}

	internal override void End ()
	{
		if (IsWinPlatform) {
			NetWinConsole?.Cleanup ();
		}

		StopReportingMouseMoves ();

		if (!RunningUnitTests) {
			Console.ResetColor ();

			//Disable alternative screen buffer.
			Console.Out.Write (EscSeqUtils.CSI_RestoreCursorAndRestoreAltBufferWithBackscroll);

			//Set cursor key to cursor.
			Console.Out.Write (EscSeqUtils.CSI_ShowCursor);
			Console.Out.Close ();
		}
	}

	public override void Refresh ()
	{
		UpdateScreen ();
		UpdateCursor ();
	}

	public override void UpdateScreen ()
	{
		if (RunningUnitTests || _winSizeChanging || Console.WindowHeight < 1 || Contents.Length != Rows * Cols || Rows != Console.WindowHeight) {
			return;
		}

		var top = 0;
		var left = 0;
		var rows = Rows;
		var cols = Cols;
		var output = new StringBuilder ();
		var redrawAttr = new Attribute ();
		var lastCol = -1;

		var savedVisibitity = _cachedCursorVisibility;
		SetCursorVisibility (CursorVisibility.Invisible);

		for (var row = top; row < rows; row++) {
			if (Console.WindowHeight < 1) {
				return;
			}
			if (!_dirtyLines [row]) {
				continue;
			}
			if (!SetCursorPosition (0, row)) {
				return;
			}
			_dirtyLines [row] = false;
			output.Clear ();
			for (var col = left; col < cols; col++) {
				lastCol = -1;
				var outputWidth = 0;
				for (; col < cols; col++) {
					if (!Contents [row, col].IsDirty) {
						if (output.Length > 0) {
							WriteToConsole (output, ref lastCol, row, ref outputWidth);
						} else if (lastCol == -1) {
							lastCol = col;
						}
						if (lastCol + 1 < cols) {
							lastCol++;
						}
						continue;
					}

					if (lastCol == -1) {
						lastCol = col;
					}

					var attr = Contents [row, col].Attribute.Value;
					// Performance: Only send the escape sequence if the attribute has changed.
					if (attr != redrawAttr) {
						redrawAttr = attr;

						if (Force16Colors) {
							output.Append (EscSeqUtils.CSI_SetGraphicsRendition (
								MapColors ((ConsoleColor)attr.Background.ColorName, false), MapColors ((ConsoleColor)attr.Foreground.ColorName)));
						} else {
							output.Append (EscSeqUtils.CSI_SetForegroundColorRGB (attr.Foreground.R, attr.Foreground.G, attr.Foreground.B));
							output.Append (EscSeqUtils.CSI_SetBackgroundColorRGB (attr.Background.R, attr.Background.G, attr.Background.B));
						}

					}
					outputWidth++;
					var rune = Contents [row, col].Rune;
					output.Append (rune);
					if (Contents [row, col].CombiningMarks.Count > 0) {
						// AtlasEngine does not support NON-NORMALIZED combining marks in a way
						// compatible with the driver architecture. Any CMs (except in the first col)
						// are correctly combined with the base char, but are ALSO treated as 1 column
						// width codepoints E.g. `echo "[e`u{0301}`u{0301}]"` will output `[é  ]`.
						// 
						// For now, we just ignore the list of CMs.
						//foreach (var combMark in Contents [row, col].CombiningMarks) {
						//	output.Append (combMark);
						//}
						// WriteToConsole (output, ref lastCol, row, ref outputWidth);
					} else if (rune.IsSurrogatePair () && rune.GetColumns () < 2) {
						WriteToConsole (output, ref lastCol, row, ref outputWidth);
						SetCursorPosition (col - 1, row);
					}
					Contents [row, col].IsDirty = false;
				}
			}
			if (output.Length > 0) {
				SetCursorPosition (lastCol, row);
				Console.Write (output);
			}
		}
		SetCursorPosition (0, 0);

		_cachedCursorVisibility = savedVisibitity;

		void WriteToConsole (StringBuilder output, ref int lastCol, int row, ref int outputWidth)
		{
			SetCursorPosition (lastCol, row);
			Console.Write (output);
			output.Clear ();
			lastCol += outputWidth;
			outputWidth = 0;
		}
	}

	void ProcessInput (InputResult inputEvent)
	{
		switch (inputEvent.EventType) {
		case EventType.Key:
			var consoleKeyInfo = inputEvent.ConsoleKeyInfo;
			//if (consoleKeyInfo.Key == ConsoleKey.Packet) {
			//	consoleKeyInfo = FromVKPacketToKConsoleKeyInfo (consoleKeyInfo);
			//}

			//Debug.WriteLine ($"event: {inputEvent}");

			var map = MapKey (consoleKeyInfo);

			if (map == KeyCode.Null) {
				break;
			}

			OnKeyDown (new Key (map));
			OnKeyUp (new Key (map));
			break;
		case EventType.Mouse:
			OnMouseEvent (new MouseEventEventArgs (ToDriverMouse (inputEvent.MouseEvent)));
			break;
		case EventType.WindowSize:
			_winSizeChanging = true;
			Top = 0;
			Left = 0;
			Cols = inputEvent.WindowSizeEvent.Size.Width;
			Rows = Math.Max (inputEvent.WindowSizeEvent.Size.Height, 0);
			;
			ResizeScreen ();
			ClearContents ();
			_winSizeChanging = false;
			OnSizeChanged (new SizeChangedEventArgs (new Size (Cols, Rows)));
			break;
		case EventType.RequestResponse:
			break;
		case EventType.WindowPosition:
			break;
		default:
			throw new ArgumentOutOfRangeException ();
		}
	}

	public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
	{
		var input = new InputResult {
			EventType = EventType.Key,
			ConsoleKeyInfo = new ConsoleKeyInfo (keyChar, key, shift, alt, control)
		};

		try {
			ProcessInput (input);
		} catch (OverflowException) { }
	}


	#region Not Implemented
	public override void Suspend () => throw new NotImplementedException ();
	#endregion


	#region Size and Position Handling
	volatile bool _winSizeChanging;

	void SetWindowPosition (int col, int row)
	{
		if (!RunningUnitTests) {
			Top = Console.WindowTop;
			Left = Console.WindowLeft;
		} else {
			Top = row;
			Left = col;
		}
	}

	public virtual void ResizeScreen ()
	{
		// Not supported on Unix.
		if (IsWinPlatform) {
			// Can raise an exception while is still resizing.
			try {
#pragma warning disable CA1416
				if (Console.WindowHeight > 0) {
					Console.CursorTop = 0;
					Console.CursorLeft = 0;
					Console.WindowTop = 0;
					Console.WindowLeft = 0;
					if (Console.WindowHeight > Rows) {
						Console.SetWindowSize (Cols, Rows);
					}
					Console.SetBufferSize (Cols, Rows);
				}
#pragma warning restore CA1416
			} catch (IOException) {
				Clip = new Rect (0, 0, Cols, Rows);
			} catch (ArgumentOutOfRangeException) {
				Clip = new Rect (0, 0, Cols, Rows);
			}
		} else {
			Console.Out.Write (EscSeqUtils.CSI_SetTerminalWindowSize (Rows, Cols));
		}


		Clip = new Rect (0, 0, Cols, Rows);
	}
	#endregion

	#region Color Handling
	// Cache the list of ConsoleColor values.
	static readonly HashSet<int> ConsoleColorValues = new (
		Enum.GetValues (typeof (ConsoleColor)).OfType<ConsoleColor> ().Select (c => (int)c)
	);

	// Dictionary for mapping ConsoleColor values to the values used by System.Net.Console.
	static readonly Dictionary<ConsoleColor, int> colorMap = new () {
		{ ConsoleColor.Black, COLOR_BLACK },
		{ ConsoleColor.DarkBlue, COLOR_BLUE },
		{ ConsoleColor.DarkGreen, COLOR_GREEN },
		{ ConsoleColor.DarkCyan, COLOR_CYAN },
		{ ConsoleColor.DarkRed, COLOR_RED },
		{ ConsoleColor.DarkMagenta, COLOR_MAGENTA },
		{ ConsoleColor.DarkYellow, COLOR_YELLOW },
		{ ConsoleColor.Gray, COLOR_WHITE },
		{ ConsoleColor.DarkGray, COLOR_BRIGHT_BLACK },
		{ ConsoleColor.Blue, COLOR_BRIGHT_BLUE },
		{ ConsoleColor.Green, COLOR_BRIGHT_GREEN },
		{ ConsoleColor.Cyan, COLOR_BRIGHT_CYAN },
		{ ConsoleColor.Red, COLOR_BRIGHT_RED },
		{ ConsoleColor.Magenta, COLOR_BRIGHT_MAGENTA },
		{ ConsoleColor.Yellow, COLOR_BRIGHT_YELLOW },
		{ ConsoleColor.White, COLOR_BRIGHT_WHITE }
	};

	// Map a ConsoleColor to a platform dependent value.
	int MapColors (ConsoleColor color, bool isForeground = true) => colorMap.TryGetValue (color, out var colorValue) ? colorValue + (isForeground ? 0 : 10) : 0;

	///// <remarks>
	///// In the NetDriver, colors are encoded as an int. 
	///// However, the foreground color is stored in the most significant 16 bits, 
	///// and the background color is stored in the least significant 16 bits.
	///// </remarks>
	//public override Attribute MakeColor (Color foreground, Color background)
	//{
	//	// Encode the colors into the int value.
	//	return new Attribute (
	//		platformColor: ((((int)foreground.ColorName) & 0xffff) << 16) | (((int)background.ColorName) & 0xffff),
	//		foreground: foreground,
	//		background: background
	//	);
	//}
	#endregion

	#region Cursor Handling
	bool SetCursorPosition (int col, int row)
	{
		if (IsWinPlatform) {
			// Could happens that the windows is still resizing and the col is bigger than Console.WindowWidth.
			try {
				Console.SetCursorPosition (col, row);
				return true;
			} catch (Exception) {
				return false;
			}
		}
		// + 1 is needed because non-Windows is based on 1 instead of 0 and
		// Console.CursorTop/CursorLeft isn't reliable.
		Console.Out.Write (EscSeqUtils.CSI_SetCursorPosition (row + 1, col + 1));
		return true;
	}

	CursorVisibility? _cachedCursorVisibility;

	public override void UpdateCursor ()
	{
		EnsureCursorVisibility ();

		if (Col >= 0 && Col < Cols && Row >= 0 && Row < Rows) {
			SetCursorPosition (Col, Row);
			SetWindowPosition (0, Row);
		}
	}

	public override bool GetCursorVisibility (out CursorVisibility visibility)
	{
		visibility = _cachedCursorVisibility ?? CursorVisibility.Default;
		return visibility == CursorVisibility.Default;
	}

	public override bool SetCursorVisibility (CursorVisibility visibility)
	{
		_cachedCursorVisibility = visibility;
		var isVisible = RunningUnitTests ? visibility == CursorVisibility.Default : Console.CursorVisible = visibility == CursorVisibility.Default;
		Console.Out.Write (isVisible ? EscSeqUtils.CSI_ShowCursor : EscSeqUtils.CSI_HideCursor);
		return isVisible;
	}

	public override bool EnsureCursorVisibility ()
	{
		if (!(Col >= 0 && Row >= 0 && Col < Cols && Row < Rows)) {
			GetCursorVisibility (out var cursorVisibility);
			_cachedCursorVisibility = cursorVisibility;
			SetCursorVisibility (CursorVisibility.Invisible);
			return false;
		}

		SetCursorVisibility (_cachedCursorVisibility ?? CursorVisibility.Default);
		return _cachedCursorVisibility == CursorVisibility.Default;
	}
	#endregion

	#region Mouse Handling
	public void StartReportingMouseMoves ()
	{
		if (!RunningUnitTests) {
			Console.Out.Write (EscSeqUtils.CSI_EnableMouseEvents);
		}
	}

	public void StopReportingMouseMoves ()
	{
		if (!RunningUnitTests) {
			Console.Out.Write (EscSeqUtils.CSI_DisableMouseEvents);
		}
	}

	MouseEvent ToDriverMouse (NetEvents.MouseEvent me)
	{
		//System.Diagnostics.Debug.WriteLine ($"X: {me.Position.X}; Y: {me.Position.Y}; ButtonState: {me.ButtonState}");

		MouseFlags mouseFlag = 0;

		if ((me.ButtonState & MouseButtonState.Button1Pressed) != 0) {
			mouseFlag |= MouseFlags.Button1Pressed;
		}
		if ((me.ButtonState & MouseButtonState.Button1Released) != 0) {
			mouseFlag |= MouseFlags.Button1Released;
		}
		if ((me.ButtonState & MouseButtonState.Button1Clicked) != 0) {
			mouseFlag |= MouseFlags.Button1Clicked;
		}
		if ((me.ButtonState & MouseButtonState.Button1DoubleClicked) != 0) {
			mouseFlag |= MouseFlags.Button1DoubleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button1TripleClicked) != 0) {
			mouseFlag |= MouseFlags.Button1TripleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button2Pressed) != 0) {
			mouseFlag |= MouseFlags.Button2Pressed;
		}
		if ((me.ButtonState & MouseButtonState.Button2Released) != 0) {
			mouseFlag |= MouseFlags.Button2Released;
		}
		if ((me.ButtonState & MouseButtonState.Button2Clicked) != 0) {
			mouseFlag |= MouseFlags.Button2Clicked;
		}
		if ((me.ButtonState & MouseButtonState.Button2DoubleClicked) != 0) {
			mouseFlag |= MouseFlags.Button2DoubleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button2TripleClicked) != 0) {
			mouseFlag |= MouseFlags.Button2TripleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button3Pressed) != 0) {
			mouseFlag |= MouseFlags.Button3Pressed;
		}
		if ((me.ButtonState & MouseButtonState.Button3Released) != 0) {
			mouseFlag |= MouseFlags.Button3Released;
		}
		if ((me.ButtonState & MouseButtonState.Button3Clicked) != 0) {
			mouseFlag |= MouseFlags.Button3Clicked;
		}
		if ((me.ButtonState & MouseButtonState.Button3DoubleClicked) != 0) {
			mouseFlag |= MouseFlags.Button3DoubleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button3TripleClicked) != 0) {
			mouseFlag |= MouseFlags.Button3TripleClicked;
		}
		if ((me.ButtonState & MouseButtonState.ButtonWheeledUp) != 0) {
			mouseFlag |= MouseFlags.WheeledUp;
		}
		if ((me.ButtonState & MouseButtonState.ButtonWheeledDown) != 0) {
			mouseFlag |= MouseFlags.WheeledDown;
		}
		if ((me.ButtonState & MouseButtonState.ButtonWheeledLeft) != 0) {
			mouseFlag |= MouseFlags.WheeledLeft;
		}
		if ((me.ButtonState & MouseButtonState.ButtonWheeledRight) != 0) {
			mouseFlag |= MouseFlags.WheeledRight;
		}
		if ((me.ButtonState & MouseButtonState.Button4Pressed) != 0) {
			mouseFlag |= MouseFlags.Button4Pressed;
		}
		if ((me.ButtonState & MouseButtonState.Button4Released) != 0) {
			mouseFlag |= MouseFlags.Button4Released;
		}
		if ((me.ButtonState & MouseButtonState.Button4Clicked) != 0) {
			mouseFlag |= MouseFlags.Button4Clicked;
		}
		if ((me.ButtonState & MouseButtonState.Button4DoubleClicked) != 0) {
			mouseFlag |= MouseFlags.Button4DoubleClicked;
		}
		if ((me.ButtonState & MouseButtonState.Button4TripleClicked) != 0) {
			mouseFlag |= MouseFlags.Button4TripleClicked;
		}
		if ((me.ButtonState & MouseButtonState.ReportMousePosition) != 0) {
			mouseFlag |= MouseFlags.ReportMousePosition;
		}
		if ((me.ButtonState & MouseButtonState.ButtonShift) != 0) {
			mouseFlag |= MouseFlags.ButtonShift;
		}
		if ((me.ButtonState & MouseButtonState.ButtonCtrl) != 0) {
			mouseFlag |= MouseFlags.ButtonCtrl;
		}
		if ((me.ButtonState & MouseButtonState.ButtonAlt) != 0) {
			mouseFlag |= MouseFlags.ButtonAlt;
		}

		return new MouseEvent {
			X = me.Position.X,
			Y = me.Position.Y,
			Flags = mouseFlag
		};
	}
	#endregion Mouse Handling

	#region Keyboard Handling
	ConsoleKeyInfo FromVKPacketToKConsoleKeyInfo (ConsoleKeyInfo consoleKeyInfo)
	{
		if (consoleKeyInfo.Key != ConsoleKey.Packet) {
			return consoleKeyInfo;
		}

		var mod = consoleKeyInfo.Modifiers;
		var shift = (mod & ConsoleModifiers.Shift) != 0;
		var alt = (mod & ConsoleModifiers.Alt) != 0;
		var control = (mod & ConsoleModifiers.Control) != 0;

		var cKeyInfo = DecodeVKPacketToKConsoleKeyInfo (consoleKeyInfo);

		return new ConsoleKeyInfo (cKeyInfo.KeyChar, cKeyInfo.Key, shift, alt, control);
	}

	KeyCode MapKey (ConsoleKeyInfo keyInfo)
	{
		switch (keyInfo.Key) {
		case ConsoleKey.OemPeriod:
		case ConsoleKey.OemComma:
		case ConsoleKey.OemPlus:
		case ConsoleKey.OemMinus:
		case ConsoleKey.Packet:
		case ConsoleKey.Oem1:
		case ConsoleKey.Oem2:
		case ConsoleKey.Oem3:
		case ConsoleKey.Oem4:
		case ConsoleKey.Oem5:
		case ConsoleKey.Oem6:
		case ConsoleKey.Oem7:
		case ConsoleKey.Oem8:
		case ConsoleKey.Oem102:
			if (keyInfo.KeyChar == 0) {
				// If the keyChar is 0, keyInfo.Key value is not a printable character. 

				return KeyCode.Null; // MapToKeyCodeModifiers (keyInfo.Modifiers, KeyCode)keyInfo.Key);
			}
			if (keyInfo.Modifiers != ConsoleModifiers.Shift) {
				// If Shift wasn't down we don't need to do anything but return the keyInfo.KeyChar
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)keyInfo.KeyChar);
			}

			// Strip off Shift - We got here because they KeyChar from Windows is the shifted char (e.g. "Ç")
			// and passing on Shift would be redundant.
			return MapToKeyCodeModifiers (keyInfo.Modifiers & ~ConsoleModifiers.Shift, (KeyCode)keyInfo.KeyChar);
		}

		var key = keyInfo.Key;
		// A..Z are special cased:
		// - Alone, they represent lowercase a...z
		// - With ShiftMask they are A..Z
		// - If CapsLock is on the above is reversed.
		// - If Alt and/or Ctrl are present, treat as upper case
		if (keyInfo.Key is >= ConsoleKey.A and <= ConsoleKey.Z) {
			if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt) || keyInfo.Modifiers.HasFlag (ConsoleModifiers.Control)) {
				return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)(uint)keyInfo.Key);
			}

			if (keyInfo.Modifiers == ConsoleModifiers.Shift) {
				// If ShiftMask is on  add the ShiftMask
				if (char.IsUpper (keyInfo.KeyChar)) {
					return (KeyCode)(uint)keyInfo.Key | KeyCode.ShiftMask;
				}
			}
			return (KeyCode)keyInfo.KeyChar;
		}

		// Handle control keys whose VK codes match the related ASCII value (those below ASCII 33) like ESC
		if (keyInfo.Key != ConsoleKey.None && Enum.IsDefined (typeof (KeyCode), (uint)keyInfo.Key)) {
			return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)keyInfo.Key);
		}

		// Handle control keys (e.g. CursorUp)
		if (keyInfo.Key != ConsoleKey.None && Enum.IsDefined (typeof (KeyCode), (uint)keyInfo.Key + (uint)KeyCode.MaxCodePoint)) {
			return MapToKeyCodeModifiers (keyInfo.Modifiers, (KeyCode)((uint)keyInfo.Key + (uint)KeyCode.MaxCodePoint));
		}


		return (KeyCode)keyInfo.KeyChar;
	}
	#endregion Keyboard Handling
}

/// <summary>
/// Mainloop intended to be used with the .NET System.Console API, and can
/// be used on Windows and Unix, it is cross platform but lacks things like
/// file descriptor monitoring.
/// </summary>
/// <remarks>
/// This implementation is used for NetDriver.
/// </remarks>
class NetMainLoop : IMainLoopDriver {
	readonly ManualResetEventSlim _eventReady = new (false);
	readonly CancellationTokenSource _inputHandlerTokenSource = new ();
	readonly Queue<InputResult?> _resultQueue = new ();
	readonly ManualResetEventSlim _waitForProbe = new (false);
	CancellationTokenSource _eventReadyTokenSource = new ();
	MainLoop _mainLoop;
	internal NetEvents _netEvents;

	/// <summary>
	/// Invoked when a Key is pressed.
	/// </summary>
	internal Action<InputResult> ProcessInput;

	/// <summary>
	/// Initializes the class with the console driver.
	/// </summary>
	/// <remarks>
	/// Passing a consoleDriver is provided to capture windows resizing.
	/// </remarks>
	/// <param name="consoleDriver">The console driver used by this Net main loop.</param>
	/// <exception cref="ArgumentNullException"></exception>
	public NetMainLoop (ConsoleDriver consoleDriver = null)
	{
		if (consoleDriver == null) {
			throw new ArgumentNullException (nameof (consoleDriver));
		}
		_netEvents = new NetEvents (consoleDriver);
	}

	void IMainLoopDriver.Setup (MainLoop mainLoop)
	{
		_mainLoop = mainLoop;
		Task.Run (NetInputHandler, _inputHandlerTokenSource.Token);
	}

	void IMainLoopDriver.Wakeup () => _eventReady.Set ();

	bool IMainLoopDriver.EventsPending ()
	{
		_waitForProbe.Set ();

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
			return _resultQueue.Count > 0 || _mainLoop.CheckTimersAndIdleHandlers (out _);
		}

		_eventReadyTokenSource.Dispose ();
		_eventReadyTokenSource = new CancellationTokenSource ();
		return true;
	}

	void IMainLoopDriver.Iteration ()
	{
		while (_resultQueue.Count > 0) {
			ProcessInput?.Invoke (_resultQueue.Dequeue ().Value);
		}
	}

	void IMainLoopDriver.TearDown ()
	{
		_inputHandlerTokenSource?.Cancel ();
		_inputHandlerTokenSource?.Dispose ();
		_eventReadyTokenSource?.Cancel ();
		_eventReadyTokenSource?.Dispose ();

		_eventReady?.Dispose ();

		_resultQueue?.Clear ();
		_waitForProbe?.Dispose ();
		_netEvents?.Dispose ();
		_netEvents = null;

		_mainLoop = null;
	}

	void NetInputHandler ()
	{
		while (_mainLoop != null) {
			try {
				if (!_inputHandlerTokenSource.IsCancellationRequested) {
					_waitForProbe.Wait (_inputHandlerTokenSource.Token);
				}

			} catch (OperationCanceledException) {
				return;
			} finally {
				if (_waitForProbe.IsSet) {
					_waitForProbe.Reset ();
				}
			}

			if (_inputHandlerTokenSource.IsCancellationRequested) {
				return;
			}
			if (_resultQueue.Count == 0) {
				_resultQueue.Enqueue (_netEvents.DequeueInput ());
			}
			try {
				while (_resultQueue.Peek () == null) {
					_resultQueue.Dequeue ();
				}
				if (_resultQueue.Count > 0) {
					_eventReady.Set ();
				}
			} catch (InvalidOperationException) {
				// Ignore
			}
		}
	}
}