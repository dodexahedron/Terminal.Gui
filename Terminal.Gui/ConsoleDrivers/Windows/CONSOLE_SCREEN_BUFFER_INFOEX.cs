#nullable enable
namespace Terminal.Gui.ConsoleDrivers.Windows;


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
