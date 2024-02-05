#nullable enable
namespace Terminal.Gui.ConsoleDrivers.Windows;

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
