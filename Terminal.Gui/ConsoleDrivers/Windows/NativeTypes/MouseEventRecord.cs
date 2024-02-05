namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

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