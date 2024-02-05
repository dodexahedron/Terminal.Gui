namespace Terminal.Gui.ConsoleDrivers.Windows.NativeTypes;

public struct ExtendedCharInfo {
    public char Char { get; set; }
    public Attribute Attribute { get; set; }
    public bool Empty { get; set; } // TODO: Temp hack until virutal terminal sequences

    public ExtendedCharInfo (char character, Attribute attribute) {
        Char = character;
        Attribute = attribute;
        Empty = false;
    }
}