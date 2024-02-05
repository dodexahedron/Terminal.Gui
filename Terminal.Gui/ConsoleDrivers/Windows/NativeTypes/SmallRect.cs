namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

[StructLayout (LayoutKind.Sequential)]
public struct SmallRect {
    public short Left;
    public short Top;
    public short Right;
    public short Bottom;

    public SmallRect (short left, short top, short right, short bottom) {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static void MakeEmpty (ref SmallRect rect) { rect.Left = -1; }

    public static void Update (ref SmallRect rect, short col, short row) {
        if (rect.Left == -1) {
            rect.Left = rect.Right = col;
            rect.Bottom = rect.Top = row;

            return;
        }

        if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom) {
            return;
        }

        if (col < rect.Left) {
            rect.Left = col;
        }

        if (col > rect.Right) {
            rect.Right = col;
        }

        if (row < rect.Top) {
            rect.Top = row;
        }

        if (row > rect.Bottom) {
            rect.Bottom = row;
        }
    }

    public readonly override string ToString () { return $"Left={Left},Top={Top},Right={Right},Bottom={Bottom}"; }
}