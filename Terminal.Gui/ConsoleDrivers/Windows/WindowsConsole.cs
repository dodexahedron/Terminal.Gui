#nullable enable
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;

namespace Terminal.Gui.ConsoleDrivers.Windows;

internal partial class WindowsConsole {
	private nint _inputHandle;
    private nint _outputHandle;
    private nint _screenBuffer;
    private readonly uint _originalConsoleMode;
    private CursorVisibility? _initialCursorVisibility;
    private CursorVisibility? _currentCursorVisibility;
    private CursorVisibility? _pendingCursorVisibility;
    private readonly StringBuilder _stringBuilder = new ( 256 * 1024 );

    public WindowsConsole () {
        _inputHandle = NativeMethods.GetStdHandle ( NativeMethods.StdInputHandle);
        _outputHandle = NativeMethods.GetStdHandle (NativeMethods.StdOutputHandle);
        _originalConsoleMode = ConsoleMode;
        uint newConsoleMode = _originalConsoleMode;
        newConsoleMode |= (uint)( ConsoleModes.EnableMouseInput | ConsoleModes.EnableExtendedFlags );
        newConsoleMode &= ~(uint)ConsoleModes.EnableQuickEditMode;
        newConsoleMode &= ~(uint)ConsoleModes.EnableProcessedInput;
        ConsoleMode = newConsoleMode;
    }

    private CharInfo[] _originalStdOutChars;

    public bool WriteToConsole (Size size, ExtendedCharInfo[] charInfoBuffer, Coord bufferSize, SmallRect window, bool force16Colors) {
        if (_screenBuffer == nint.Zero) {
            ReadFromConsoleOutput (size, bufferSize, ref window);
        }

        bool result;
        if (force16Colors) {
            var i = 0;
            CharInfo[] ci = new CharInfo [charInfoBuffer.Length];
            foreach ( ExtendedCharInfo info in charInfoBuffer ) {
                ci[ i++ ] = new () {
                                       Char = new () { UnicodeChar = info.Char },
                                       Attributes = (ushort)( (int)info.Attribute.Foreground.GetClosestNamedColor () | ( (int)info.Attribute.Background.GetClosestNamedColor () << 4 ) )
                                   };
            }

            result = NativeMethods.WriteConsoleOutput (_screenBuffer, ci, bufferSize, new () { X = window.Left, Y = window.Top }, ref window);
        }
        else {
            _stringBuilder.Clear ();

            _stringBuilder.Append (EscSeqUtils.CSI_SaveCursorPosition);
            _stringBuilder.Append (EscSeqUtils.CSI_SetCursorPosition (0, 0));

            Attribute? prev = null;
            foreach ( ExtendedCharInfo info in charInfoBuffer ) {
                Attribute attr = info.Attribute;

                if (attr != prev) {
                    prev = attr;
                    _stringBuilder.Append (EscSeqUtils.CSI_SetForegroundColorRGB (attr.Foreground.R, attr.Foreground.G, attr.Foreground.B));
                    _stringBuilder.Append (EscSeqUtils.CSI_SetBackgroundColorRGB (attr.Background.R, attr.Background.G, attr.Background.B));
                }

                if (info.Char != '\x1b') {
                    if (!info.Empty) {
                        _stringBuilder.Append (info.Char);
                    }
                }
                else {
                    _stringBuilder.Append (' ');
                }
            }

            _stringBuilder.Append (EscSeqUtils.CSI_RestoreCursorPosition);

            var s = _stringBuilder.ToString ();

            result = NativeMethods.WriteConsole (_screenBuffer, s, (uint)s.Length, out _, null);
        }

        if (!result) {
            int err = Marshal.GetLastWin32Error ();
            if (err != 0) {
                throw new Win32Exception (err);
            }
        }

        return result;
    }

    public void ReadFromConsoleOutput (Size size, Coord coords, ref SmallRect window) {
        _screenBuffer = NativeMethods.CreateConsoleScreenBuffer (
                                                   DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
                                                   ShareMode.FileShareRead | ShareMode.FileShareWrite,
                                                   nint.Zero,
                                                   1,
                                                   nint.Zero
                                                  );
        if (_screenBuffer == NativeMethods.INVALID_HANDLE_VALUE ) {
            int err = Marshal.GetLastWin32Error ();

            if (err != 0) {
                throw new Win32Exception (err);
            }
        }

        if (!_initialCursorVisibility.HasValue && GetCursorVisibility (out CursorVisibility visibility)) {
            _initialCursorVisibility = visibility;
        }

        if (!NativeMethods.SetConsoleActiveScreenBuffer (_screenBuffer)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        _originalStdOutChars = new CharInfo [size.Height * size.Width];

        if (!NativeMethods.ReadConsoleOutput (_screenBuffer, _originalStdOutChars, coords, new () { X = 0, Y = 0 }, ref window)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }
    }

    public bool SetCursorPosition (Coord position) { return NativeMethods.SetConsoleCursorPosition (_screenBuffer, position); }

    public void SetInitialCursorVisibility () {
        if (_initialCursorVisibility.HasValue == false && GetCursorVisibility (out CursorVisibility visibility)) {
            _initialCursorVisibility = visibility;
        }
    }

    public bool GetCursorVisibility (out CursorVisibility visibility) {
        if (_screenBuffer == nint.Zero) {
            visibility = CursorVisibility.Invisible;

            return false;
        }

        if (!NativeMethods.GetConsoleCursorInfo (_screenBuffer, out ConsoleCursorInfo info)) {
            int err = Marshal.GetLastWin32Error ();
            if (err != 0) {
                throw new Win32Exception (err);
            }

            visibility = CursorVisibility.Default;

            return false;
        }

        if (!info.bVisible) {
            visibility = CursorVisibility.Invisible;
        }
        else if (info.dwSize > 50) {
            visibility = CursorVisibility.Box;
        }
        else {
            visibility = CursorVisibility.Underline;
        }

        return true;
    }

    public bool EnsureCursorVisibility () {
        if (_initialCursorVisibility.HasValue && _pendingCursorVisibility.HasValue && SetCursorVisibility (_pendingCursorVisibility.Value)) {
            _pendingCursorVisibility = null;

            return true;
        }

        return false;
    }

    public void ForceRefreshCursorVisibility () {
        if (_currentCursorVisibility.HasValue) {
            _pendingCursorVisibility = _currentCursorVisibility;
            _currentCursorVisibility = null;
        }
    }

    public bool SetCursorVisibility (CursorVisibility visibility) {
        if (_initialCursorVisibility.HasValue == false) {
            _pendingCursorVisibility = visibility;

            return false;
        }

        if (_currentCursorVisibility.HasValue == false || _currentCursorVisibility.Value != visibility) {
            var info = new ConsoleCursorInfo {
                                                 dwSize = (uint)visibility & 0x00FF,
                                                 bVisible = ( (uint)visibility & 0xFF00 ) != 0
                                             };

            if (!NativeMethods.SetConsoleCursorInfo (_screenBuffer, ref info)) {
                return false;
            }

            _currentCursorVisibility = visibility;
        }

        return true;
    }

    public void Cleanup () {
        if (_initialCursorVisibility.HasValue) {
            SetCursorVisibility (_initialCursorVisibility.Value);
        }

        SetConsoleOutputWindow (out _);

        ConsoleMode = _originalConsoleMode;
        if (!NativeMethods.SetConsoleActiveScreenBuffer (_outputHandle)) {
            int err = Marshal.GetLastWin32Error ();
            Console.WriteLine ("Error: {0}", err);
        }

        if (_screenBuffer != nint.Zero) {
			NativeMethods.CloseHandle (_screenBuffer);
        }

        _screenBuffer = nint.Zero;
    }

    internal Size GetConsoleBufferWindow (out Point position) {
        if (_screenBuffer == nint.Zero) {
            position = Point.Empty;

            return Size.Empty;
        }

        var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
        csbi.cbSize = (uint)Marshal.SizeOf (csbi);
        if (!NativeMethods.GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            //throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
            position = Point.Empty;

            return Size.Empty;
        }

        var sz = new Size (
                           csbi.srWindow.Right - csbi.srWindow.Left + 1,
                           csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
        position = new ( csbi.srWindow.Left, csbi.srWindow.Top );

        return sz;
    }

    internal Size GetConsoleOutputWindow (out Point position) {
        var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
        csbi.cbSize = (uint)Marshal.SizeOf (csbi);
        if (!NativeMethods.GetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var sz = new Size (
                           csbi.srWindow.Right - csbi.srWindow.Left + 1,
                           csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
        position = new ( csbi.srWindow.Left, csbi.srWindow.Top );

        return sz;
    }
    internal void ShowWindow (int state)
    {
        IntPtr thisConsole = NativeMethods.GetConsoleWindow ();
        NativeMethods.ShowWindow (thisConsole, state);
    }

    internal Size SetConsoleWindow (short cols, short rows) {
        var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
        csbi.cbSize = (uint)Marshal.SizeOf (csbi);

        if (!NativeMethods.GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        Coord maxWinSize = NativeMethods.GetLargestConsoleWindowSize (_screenBuffer);
        short newCols = Math.Min (cols, maxWinSize.X);
        short newRows = Math.Min (rows, maxWinSize.Y);
        csbi.dwSize = new ( newCols, Math.Max (newRows, (short)1) );
        csbi.srWindow = new ( 0, 0, newCols, newRows );
        csbi.dwMaximumWindowSize = new ( newCols, newRows );
        if (!NativeMethods.SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var winRect = new SmallRect (0, 0, (short)( newCols - 1 ), (short)Math.Max (newRows - 1, 0));
        if (!NativeMethods.SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
            //throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
            return new ( cols, rows );
        }

        SetConsoleOutputWindow (csbi);

        return new ( winRect.Right + 1, newRows - 1 < 0 ? 0 : winRect.Bottom + 1 );
    }

    private void SetConsoleOutputWindow (CONSOLE_SCREEN_BUFFER_INFOEX csbi) {
        if (_screenBuffer != nint.Zero && !NativeMethods.SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }
    }

    internal Size SetConsoleOutputWindow (out Point position) {
        if (_screenBuffer == nint.Zero) {
            position = Point.Empty;

            return Size.Empty;
        }

        var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
        csbi.cbSize = (uint)Marshal.SizeOf (csbi);
        if (!NativeMethods.GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var sz = new Size (
                           csbi.srWindow.Right - csbi.srWindow.Left + 1,
                           Math.Max (csbi.srWindow.Bottom - csbi.srWindow.Top + 1, 0));
        position = new ( csbi.srWindow.Left, csbi.srWindow.Top );
        SetConsoleOutputWindow (csbi);
        var winRect = new SmallRect (0, 0, (short)( sz.Width - 1 ), (short)Math.Max (sz.Height - 1, 0));
        if (!NativeMethods.SetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        if (!NativeMethods.SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        return sz;
    }

    private uint ConsoleMode {
        get {
			NativeMethods.GetConsoleMode (_inputHandle, out uint v);

            return v;
        }
        set => NativeMethods.SetConsoleMode (_inputHandle, value);
    }

	public InputRecord [ ] ReadConsoleInput () {
        const int bufferSize = 1;
        nint pRecord = Marshal.AllocHGlobal (Marshal.SizeOf<InputRecord> () * bufferSize);
        try {
			NativeMethods.ReadConsoleInput (
                              _inputHandle,
                              pRecord,
                              bufferSize,
                              out uint numberEventsRead);

            return numberEventsRead == 0
                       ? null
                       : new[] { Marshal.PtrToStructure<InputRecord> (pRecord) };
        }
        catch ( Exception ) {
            return null;
        }
        finally {
            Marshal.FreeHGlobal (pRecord);
        }
    }
}
