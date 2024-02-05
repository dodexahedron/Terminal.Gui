namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[Flags]
internal enum DesiredAccess : uint {
    GenericRead = 2147483648,
    GenericWrite = 1073741824
}