namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[StructLayout (LayoutKind.Explicit)]
public struct InputRecord {
    [FieldOffset (0)]
    public EventType EventType;

    [FieldOffset (4)]
    public KeyEventRecord KeyEvent;

    [FieldOffset (4)]
    public MouseEventRecord MouseEvent;

    [FieldOffset (4)]
    public WindowBufferSizeRecord WindowBufferSizeEvent;

    [FieldOffset (4)]
    public MenuEventRecord MenuEvent;

    [FieldOffset (4)]
    public FocusEventRecord FocusEvent;

    public readonly override string ToString () {
        return EventType switch {
                   EventType.Focus => FocusEvent.ToString (),
                   EventType.Key => KeyEvent.ToString (),
                   EventType.Menu => MenuEvent.ToString (),
                   EventType.Mouse => MouseEvent.ToString (),
                   EventType.WindowBufferSize => WindowBufferSizeEvent.ToString (),
                   _ => "Unknown event type: " + EventType
               };
    }
}