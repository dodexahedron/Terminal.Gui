namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
internal enum ShareMode : uint {
    FileShareRead = 1,
    FileShareWrite = 2
}