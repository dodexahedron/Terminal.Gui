namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
public enum ButtonState {
    Button1Pressed = 1,
    Button2Pressed = 4,
    Button3Pressed = 8,
    Button4Pressed = 16,
    RightmostButtonPressed = 2
}