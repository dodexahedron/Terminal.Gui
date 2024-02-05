namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
public enum EventFlags {
    MouseMoved = 1,
    DoubleClick = 2,
    MouseWheeled = 4,
    MouseHorizontalWheeled = 8
}