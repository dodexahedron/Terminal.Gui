namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
public struct CharInfo {
    [FieldOffset (0)]
    public CharUnion Char;

    [FieldOffset (2)]
    public ushort Attributes;
}