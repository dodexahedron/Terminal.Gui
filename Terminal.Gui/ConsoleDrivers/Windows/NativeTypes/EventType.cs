namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

public enum EventType : ushort {
    Focus = 0x10,
    Key = 0x1,
    Menu = 0x8,
    Mouse = 2,
    WindowBufferSize = 4
}