namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
public enum ControlKeyState {
    RightAltPressed = 1,
    LeftAltPressed = 2,
    RightControlPressed = 4,
    LeftControlPressed = 8,
    ShiftPressed = 16,
    NumlockOn = 32,
    ScrolllockOn = 64,
    CapslockOn = 128,
    EnhancedKey = 256
}