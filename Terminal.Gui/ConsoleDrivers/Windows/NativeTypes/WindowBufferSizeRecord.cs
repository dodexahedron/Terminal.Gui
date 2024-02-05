namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

public struct WindowBufferSizeRecord {
    public Coord _size;
    public WindowBufferSizeRecord (short x, short y) { _size = new ( x, y ); }
    public readonly override string ToString () { return $"[WindowBufferSize{_size}"; }
}