#nullable enable
namespace Terminal.Gui.ConsoleDrivers.Windows;

[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
public struct CharUnion {
    [FieldOffset (0)]
    public char UnicodeChar;

    [FieldOffset (0)]
    public byte AsciiChar;
}
