namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[StructLayout (LayoutKind.Sequential)]
public struct ConsoleScreenBufferInfo {
    public Coord dwSize;
    public Coord dwCursorPosition;
    public ushort wAttributes;
    public SmallRect srWindow;
    public Coord dwMaximumWindowSize;
}