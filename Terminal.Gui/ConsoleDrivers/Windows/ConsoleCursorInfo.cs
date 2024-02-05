#nullable enable
namespace Terminal.Gui.ConsoleDrivers.Windows;

[StructLayout (LayoutKind.Sequential)]
public struct ConsoleCursorInfo {
    public uint dwSize;
    public bool bVisible;
}
