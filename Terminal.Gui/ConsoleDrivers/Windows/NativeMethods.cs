#nullable enable
static class NativeMethods {
	internal const int StdOutputHandle = -11;
	internal const int StdInputHandle = -10;
	internal static nint INVALID_HANDLE_VALUE = new ( -1 );

	[DllImport ("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
	public static extern bool ReadConsoleInput (
	    nint hConsoleInput,
	    nint lpBuffer,
	    uint nLength,
	    out uint lpNumberOfEventsRead
	);
	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool CloseHandle (nint handle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern nint CreateConsoleScreenBuffer (
	    DesiredAccess dwDesiredAccess,
	    ShareMode dwShareMode,
	    nint secutiryAttributes,
	    uint flags,
	    nint screenBufferData
	);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool GetConsoleCursorInfo (nint hConsoleOutput, out ConsoleCursorInfo lpConsoleCursorInfo);

	[DllImport ("kernel32.dll")]
	internal static extern bool GetConsoleMode (nint hConsoleHandle, out uint lpMode);
                [DllImport ("kernel32.dll", ExactSpelling = true)]
                internal static extern IntPtr GetConsoleWindow ();

                [DllImport ("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                internal static extern bool ShowWindow (IntPtr hWnd, int nCmdShow);

                public const int HIDE = 0;
                public const int MAXIMIZE = 3;
                public const int MINIMIZE = 6;
                public const int RESTORE = 9;

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool GetConsoleScreenBufferInfoEx (nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX csbi);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern Coord GetLargestConsoleWindowSize (
	    nint hConsoleOutput
	);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern nint GetStdHandle (int nStdHandle);

	[DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	internal static extern bool ReadConsoleOutput (
	    nint hConsoleOutput,
	    [Out] CharInfo [ ] lpBuffer,
	    Coord dwBufferSize,
	    Coord dwBufferCoord,
	    ref SmallRect lpReadRegion
	);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool SetConsoleActiveScreenBuffer (nint Handle);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool SetConsoleCursorInfo (nint hConsoleOutput, [In] ref ConsoleCursorInfo lpConsoleCursorInfo);

	[DllImport ("kernel32.dll")]
	internal static extern bool SetConsoleCursorPosition (nint hConsoleOutput, Coord dwCursorPosition);

	[DllImport ("kernel32.dll")]
	internal static extern bool SetConsoleMode (nint hConsoleHandle, uint dwMode);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool SetConsoleScreenBufferInfoEx (nint hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFOEX ConsoleScreenBufferInfo);

	[DllImport ("kernel32.dll", SetLastError = true)]
	internal static extern bool SetConsoleWindowInfo (
	    nint hConsoleOutput,
	    bool bAbsolute,
	    [In] ref SmallRect lpConsoleWindow
	);

	[DllImport ("kernel32.dll", EntryPoint = "WriteConsole", SetLastError = true, CharSet = CharSet.Unicode)]
	internal static extern bool WriteConsole (
	    nint hConsoleOutput,
	    string lpbufer,
	    uint NumberOfCharsToWriten,
	    out uint lpNumberOfCharsWritten,
	    object lpReserved
	);

	// TODO: This API is obsolete. See https://learn.microsoft.com/en-us/windows/console/writeconsoleoutput
	[DllImport ("kernel32.dll", EntryPoint = "WriteConsoleOutputW", SetLastError = true, CharSet = CharSet.Unicode)]
	internal static extern bool WriteConsoleOutput (
	    nint hConsoleOutput,
	    CharInfo [ ] lpBuffer,
	    Coord dwBufferSize,
	    Coord dwBufferCoord,
	    ref SmallRect lpWriteRegion
	);
}