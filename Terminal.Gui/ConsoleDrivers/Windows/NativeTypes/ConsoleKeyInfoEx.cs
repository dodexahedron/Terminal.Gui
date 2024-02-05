namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[StructLayout (LayoutKind.Sequential)]
public struct ConsoleKeyInfoEx {
    public ConsoleKeyInfo ConsoleKeyInfo;
    public bool CapsLock;
    public bool NumLock;
    public bool ScrollLock;

    public ConsoleKeyInfoEx (ConsoleKeyInfo consoleKeyInfo, bool capslock, bool numlock, bool scrolllock) {
        ConsoleKeyInfo = consoleKeyInfo;
        CapsLock = capslock;
        NumLock = numlock;
        ScrollLock = scrolllock;
    }

    /// <summary>Prints a ConsoleKeyInfoEx structure</summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    public readonly string ToString (ConsoleKeyInfoEx ex) {
        var ke = new Key ((KeyCode)ex.ConsoleKeyInfo.KeyChar);
        var sb = new StringBuilder ();
        sb.Append ($"Key: {(KeyCode)ex.ConsoleKeyInfo.Key} ({ex.ConsoleKeyInfo.Key})");
        sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Shift ) != 0 ? " | Shift" : string.Empty);
        sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Control ) != 0 ? " | Control" : string.Empty);
        sb.Append (( ex.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Alt ) != 0 ? " | Alt" : string.Empty);
        sb.Append ($", KeyChar: {ke.AsRune.MakePrintable ()} ({(uint)ex.ConsoleKeyInfo.KeyChar}) ");
        sb.Append (ex.CapsLock ? "caps," : string.Empty);
        sb.Append (ex.NumLock ? "num," : string.Empty);
        sb.Append (ex.ScrollLock ? "scroll," : string.Empty);
        string s = sb.ToString ().TrimEnd (',').TrimEnd (' ');

        return $"[ConsoleKeyInfoEx({s})]";
    }
}