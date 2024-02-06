#nullable enable

using System.ComponentModel;

namespace Terminal.Gui.ConsoleDrivers.Windows;

internal class WindowsConsole {
    private const int StdOutputHandle = -11;
    private const int StdInputHandle = -10;
    private nint _inputHandle;
    private nint _outputHandle;
    private nint _screenBuffer;
    private readonly uint _originalConsoleMode;
    private CursorVisibility? _initialCursorVisibility = null;
    private CursorVisibility? _currentCursorVisibility = null;
    private CursorVisibility? _pendingCursorVisibility = null;
    private readonly StringBuilder _stringBuilder = new ( 256 * 1024 );

    public WindowsConsole () {
        _inputHandle = GetStdHandle (StdInputHandle);
        _outputHandle = GetStdHandle (StdOutputHandle);
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

            result = WriteConsoleOutput (_screenBuffer, ci, bufferSize, new () { X = window.Left, Y = window.Top }, ref window);
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

            result = WriteConsole (_screenBuffer, s, (uint)s.Length, out _, null);
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
        _screenBuffer = CreateConsoleScreenBuffer (
                                                   DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
                                                   ShareMode.FileShareRead | ShareMode.FileShareWrite,
                                                   nint.Zero,
                                                   1,
                                                   nint.Zero
                                                  );
        if (_screenBuffer == INVALID_HANDLE_VALUE) {
            int err = Marshal.GetLastWin32Error ();

            if (err != 0) {
                throw new Win32Exception (err);
            }
        }

        if (!_initialCursorVisibility.HasValue && GetCursorVisibility (out CursorVisibility visibility)) {
            _initialCursorVisibility = visibility;
        }

        if (!SetConsoleActiveScreenBuffer (_screenBuffer)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        _originalStdOutChars = new CharInfo [size.Height * size.Width];

        if (!ReadConsoleOutput (_screenBuffer, _originalStdOutChars, coords, new () { X = 0, Y = 0 }, ref window)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }
    }

    public bool SetCursorPosition (Coord position) { return SetConsoleCursorPosition (_screenBuffer, position); }

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

        if (!GetConsoleCursorInfo (_screenBuffer, out ConsoleCursorInfo info)) {
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

            if (!SetConsoleCursorInfo (_screenBuffer, ref info)) {
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
        if (!SetConsoleActiveScreenBuffer (_outputHandle)) {
            int err = Marshal.GetLastWin32Error ();
            Console.WriteLine ("Error: {0}", err);
        }

        if (_screenBuffer != nint.Zero) {
            CloseHandle (_screenBuffer);
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
        if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
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
        if (!GetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var sz = new Size (
                           csbi.srWindow.Right - csbi.srWindow.Left + 1,
                           csbi.srWindow.Bottom - csbi.srWindow.Top + 1);
        position = new ( csbi.srWindow.Left, csbi.srWindow.Top );

        return sz;
    }

    internal Size SetConsoleWindow (short cols, short rows) {
        var csbi = new CONSOLE_SCREEN_BUFFER_INFOEX ();
        csbi.cbSize = (uint)Marshal.SizeOf (csbi);

        if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        Coord maxWinSize = GetLargestConsoleWindowSize (_screenBuffer);
        short newCols = Math.Min (cols, maxWinSize.X);
        short newRows = Math.Min (rows, maxWinSize.Y);
        csbi.dwSize = new ( newCols, Math.Max (newRows, (short)1) );
        csbi.srWindow = new ( 0, 0, newCols, newRows );
        csbi.dwMaximumWindowSize = new ( newCols, newRows );
        if (!SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var winRect = new SmallRect (0, 0, (short)( newCols - 1 ), (short)Math.Max (newRows - 1, 0));
        if (!SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
            //throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
            return new ( cols, rows );
        }

        SetConsoleOutputWindow (csbi);

        return new ( winRect.Right + 1, newRows - 1 < 0 ? 0 : winRect.Bottom + 1 );
    }

    private void SetConsoleOutputWindow (CONSOLE_SCREEN_BUFFER_INFOEX csbi) {
        if (_screenBuffer != nint.Zero && !SetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
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
        if (!GetConsoleScreenBufferInfoEx (_screenBuffer, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        var sz = new Size (
                           csbi.srWindow.Right - csbi.srWindow.Left + 1,
                           Math.Max (csbi.srWindow.Bottom - csbi.srWindow.Top + 1, 0));
        position = new ( csbi.srWindow.Left, csbi.srWindow.Top );
        SetConsoleOutputWindow (csbi);
        var winRect = new SmallRect (0, 0, (short)( sz.Width - 1 ), (short)Math.Max (sz.Height - 1, 0));
        if (!SetConsoleScreenBufferInfoEx (_outputHandle, ref csbi)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        if (!SetConsoleWindowInfo (_outputHandle, true, ref winRect)) {
            throw new Win32Exception (Marshal.GetLastWin32Error ());
        }

        return sz;
    }

    private uint ConsoleMode {
        get {
            GetConsoleMode (_inputHandle, out uint v);

            return v;
        }
        set => SetConsoleMode (_inputHandle, value);
    }

    [Flags]
    public enum ConsoleModes : uint {
        EnableProcessedInput = 1,
        EnableMouseInput = 16,
        EnableQuickEditMode = 64,
        EnableExtendedFlags = 128
    }

    [StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct KeyEventRecord {
        [FieldOffset (0)]
        [MarshalAs (UnmanagedType.Bool)]
        public bool bKeyDown;

        [FieldOffset (4)]
        [MarshalAs (UnmanagedType.U2)]
        public ushort wRepeatCount;

        [FieldOffset (6)]
        [MarshalAs (UnmanagedType.U2)]
        public ConsoleKeyMapping.VK wVirtualKeyCode;

        [FieldOffset (8)]
        [MarshalAs (UnmanagedType.U2)]
        public ushort wVirtualScanCode;

        [FieldOffset (10)]
        public char UnicodeChar;

        [FieldOffset (12)]
        [MarshalAs (UnmanagedType.U4)]
        public ControlKeyState dwControlKeyState;

        public readonly override string ToString () { return $"[KeyEventRecord({( bKeyDown ? "down" : "up" )},{wRepeatCount},{wVirtualKeyCode},{wVirtualScanCode},{new Rune (UnicodeChar).MakePrintable ()},{dwControlKeyState})]"; }
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

        public readonly override string ToString () { return $"[Mouse({MousePosition},{ButtonState},{ControlKeyState},{EventFlags}"; }
    }

    public struct WindowBufferSizeRecord {
        public Coord _size;
        public WindowBufferSizeRecord (short x, short y) { _size = new ( x, y ); }
        public readonly override string ToString () { return $"[WindowBufferSize{_size}"; }
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

        public readonly override string ToString () {
            return EventType switch {
                       EventType.Focus => FocusEvent.ToString (),
                       EventType.Key => KeyEvent.ToString (),
                       EventType.Menu => MenuEvent.ToString (),
                       EventType.Mouse => MouseEvent.ToString (),
                       EventType.WindowBufferSize => WindowBufferSizeEvent.ToString (),
                       _ => "Unknown event type: " + EventType
                   };
        }
    }

    [Flags]
    private enum ShareMode : uint {
        FileShareRead = 1,
        FileShareWrite = 2
    }

    [Flags]
    private enum DesiredAccess : uint {
        GenericRead = 2147483648,
        GenericWrite = 1073741824
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

        public Coord (short x, short y) {
            X = x;
            Y = y;
        }

        public readonly override string ToString () { return $"({X},{Y})"; }
    }

    [StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct CharUnion {
        [FieldOffset (0)]
        public char UnicodeChar;

        [FieldOffset (0)]
        public byte AsciiChar;
    }

    [StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct CharInfo {
        [FieldOffset (0)]
        public CharUnion Char;

        [FieldOffset (2)]
        public ushort Attributes;
    }

    public struct ExtendedCharInfo {
        public char Char { get; set; }
        public Attribute Attribute { get; set; }
        public bool Empty { get; set; } // TODO: Temp hack until virutal terminal sequences

        public ExtendedCharInfo (char character, Attribute attribute) {
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

        public SmallRect (short left, short top, short right, short bottom) {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public static void MakeEmpty (ref SmallRect rect) { rect.Left = -1; }

        public static void Update (ref SmallRect rect, short col, short row) {
            if (rect.Left == -1) {
                rect.Left = rect.Right = col;
                rect.Bottom = rect.Top = row;

                return;
            }

            if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom) {
                return;
            }

            if (col < rect.Left) {
                rect.Left = col;
            }

            if (col > rect.Right) {
                rect.Right = col;
            }

            if (row < rect.Top) {
                rect.Top = row;
            }

            if (row > rect.Bottom) {
                rect.Bottom = row;
            }
        }

        public readonly override string ToString () { return $"Left={Left},Top={Top},Right={Right},Bottom={Bottom}"; }
    }

    [StructLayout (LayoutKind.Sequential)]
    public struct ConsoleKeyInfoEx {
        public ConsoleKeyInfo ConsoleKeyInfo;
        public bool CapsLock;
        public bool NumLock;
        public bool ScrollLock;

        public ConsoleKeyInfoEx (ConsoleKeyInfo consoleKeyInfo, bool capslock, bool numlock, bool scrolllock) {
            ConsoleKeyInfo = consoleKeyInfo;
            CapsLock = capslock;
            NumLock = numlock;
            ScrollLock = scrolllock;
        }

        /// <summary>Prints a ConsoleKeyInfoEx structure</summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public readonly string ToString (ConsoleKeyInfoEx ex) {
            var ke = new Key ((KeyCode)ex.ConsoleKeyInfo.KeyChar);
            var sb = new StringBuilder ();
            sb.Append ($"Key: {(KeyCode)ex.ConsoleKeyInfo.Key} ({ex.ConsoleKeyInfo.Key})");
            sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Shift ) != 0 ? " | Shift" : string.Empty);
            sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Control ) != 0 ? " | Control" : string.Empty);
            sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Alt ) != 0 ? " | Alt" : string.Empty);
            sb.Append ($", KeyChar: {ke.AsRune.MakePrintable ()} ({(uint)ex.ConsoleKeyInfo.KeyChar}) ");
            sb.Append (ex.CapsLock ? "caps," : string.Empty);
            sb.Append (ex.NumLock ? "num," : string.Empty);
            sb.Append (ex.ScrollLock ? "scroll," : string.Empty);
            string s = sb.ToString ().TrimEnd (',').TrimEnd (' ');

            return $"[ConsoleKeyInfoEx({s})]";
        }
    }

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle (int nStdHandle);
    [DllImport ( "kernel32.dll", SetLastError = true )]
    private static extern bool CloseHandle (nint handle);

    [DllImport ("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
    public static extern bool ReadConsoleInput (
        nint hConsoleInput,
        nint lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead
    );

    [DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ReadConsoleOutput (
        nint hConsoleOutput,
        [Out] CharInfo[] lpBuffer,
        Coord dwBufferSize,
        Coord dwBufferCoord,
        ref SmallRect lpReadRegion
    );

    // TODO: This API is obsolete. See https://learn.microsoft.com/en-us/windows/console/writeconsoleoutput
    [DllImport ("kernel32.dll", EntryPoint = "WriteConsoleOutputW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WriteConsoleOutput (
        nint hConsoleOutput,
        CharInfo[] lpBuffer,
        Coord dwBufferSize,
        Coord dwBufferCoord,
        ref SmallRect lpWriteRegion
    );

    [DllImport ("kernel32.dll", EntryPoint = "WriteConsole", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WriteConsole (
        nint hConsoleOutput,
        string lpbufer,
        uint NumberOfCharsToWriten,
        out uint lpNumberOfCharsWritten,
        object lpReserved
    );

    [DllImport ("kernel32.dll")]
    private static extern bool SetConsoleCursorPosition (nint hConsoleOutput, Coord dwCursorPosition);

    [StructLayout (LayoutKind.Sequential)]
    public struct ConsoleCursorInfo {
        public uint dwSize;
        public bool bVisible;
    }

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCursorInfo (nint hConsoleOutput, [In] ref ConsoleCursorInfo lpConsoleCursorInfo);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleCursorInfo (nint hConsoleOutput, out ConsoleCursorInfo lpConsoleCursorInfo);

    [DllImport ("kernel32.dll")]
    private static extern bool GetConsoleMode (nint hConsoleHandle, out uint lpMode);

    [DllImport ("kernel32.dll")]
    private static extern bool SetConsoleMode (nint hConsoleHandle, uint dwMode);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern nint CreateConsoleScreenBuffer (
        DesiredAccess dwDesiredAccess,
        ShareMode dwShareMode,
        nint secutiryAttributes,
        uint flags,
        nint screenBufferData
    );

    internal static nint INVALID_HANDLE_VALUE = new ( -1 );

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleActiveScreenBuffer (nint Handle);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumberOfConsoleInputEvents (nint handle, out uint lpcNumberOfEvents);

    public InputRecord[] ReadConsoleInput () {
        const int bufferSize = 1;
        nint pRecord = Marshal.AllocHGlobal (Marshal.SizeOf<InputRecord> () * bufferSize);
        try {
            ReadConsoleInput (
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

#if false // Not needed on the constructor. Perhaps could be used on resizing. To study.                                                                                     
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
        public COLORREF[] ColorTable;
    }

    [StructLayout (LayoutKind.Explicit, Size = 4)]
    public struct COLORREF {
        public COLORREF (byte r, byte g, byte b) {
            Value = 0;
            R = r;
            G = g;
            B = b;
        }

        public COLORREF (uint value) {
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
    private static extern bool GetConsoleScreenBufferInfoEx (nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX csbi);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleScreenBufferInfoEx (nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX ConsoleScreenBufferInfo);

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleWindowInfo (
        nint hConsoleOutput,
        bool bAbsolute,
        [In] ref SmallRect lpConsoleWindow
    );

    [DllImport ("kernel32.dll", SetLastError = true)]
    private static extern Coord GetLargestConsoleWindowSize (
        nint hConsoleOutput
    );
}
