namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
public enum ConsoleModes : uint {
    EnableProcessedInput = 1,
    EnableMouseInput = 16,
    EnableQuickEditMode = 64,
    EnableExtendedFlags = 128
}