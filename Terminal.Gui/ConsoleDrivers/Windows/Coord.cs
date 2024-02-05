#nullable enable
namespace Terminal.Gui.ConsoleDrivers.Windows;

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
