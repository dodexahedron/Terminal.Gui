#nullable enable
namespace Terminal.Gui;

public partial record Key
{
    /// <summary>
    ///     The key value as a Rune. This is the actual value of the key pressed, and is independent of the modifiers.
    ///     Useful for determining if a key represents is a printable character.
    /// </summary>
    /// <remarks>
    ///     <para>Keys with Ctrl or Alt modifiers will return <see langword="default"/>.</para>
    ///     <para>
    ///         If the key is a letter key (A-Z), the Rune will be the upper or lower case letter depending on whether
    ///         <see cref="KeyCode.ShiftMask"/> is set.
    ///     </para>
    ///     <para>
    ///         If the key is outside of the <see cref="KeyCode.CharMask"/> range, the returned Rune will be
    ///         <see langword="default"/>.
    ///     </para>
    /// </remarks>
    public Rune AsRune ()
    {
        if (KeyCode is KeyCode.Null or KeyCode.SpecialMask
            || KeyCode.HasFlag (KeyCode.CtrlMask)
            || KeyCode.HasFlag (KeyCode.AltMask))
        {
            return default (Rune);
        }

        // Extract the base key code
        KeyCode baseKey = KeyCode;

        if (baseKey.HasFlag (KeyCode.ShiftMask))
        {
            baseKey &= ~KeyCode.ShiftMask;
        }

        switch (baseKey)
        {
            case >= KeyCode.A and <= KeyCode.Z when !KeyCode.HasFlag (KeyCode.ShiftMask):
                return new ((uint)(baseKey + 32));
            case >= KeyCode.A and <= KeyCode.Z when KeyCode.HasFlag (KeyCode.ShiftMask):
                return new ((uint)baseKey);
            case > KeyCode.Null and < KeyCode.A:
                return new ((uint)baseKey);
        }

        if (Enum.IsDefined (typeof (KeyCode), baseKey))
        {
            return default (Rune);
        }

        return new ((uint)baseKey);
    }

    /// <summary>
    ///     Converts a <see cref="KeyCode"/> to a <see cref="Rune"/>. Useful for determining if a key represents is a
    ///     printable character.
    /// </summary>
    /// <remarks>
    ///     <para>Keys with Ctrl or Alt modifiers will return <see langword="default"/>.</para>
    ///     <para>
    ///         If the key is a letter key (A-Z), the Rune will be the upper or lower case letter depending on whether
    ///         <see cref="KeyCode.ShiftMask"/> is set.
    ///     </para>
    ///     <para>
    ///         If the key is outside of the <see cref="KeyCode.CharMask"/> range, the returned Rune will be
    ///         <see langword="default"/>.
    ///     </para>
    /// </remarks>
    /// <param name="key"></param>
    /// <returns>The key converted to a Rune. <see langword="default"/> if conversion is not possible.</returns>
    public static Rune ToRune (KeyCode key)
    {
        if (key is KeyCode.Null or KeyCode.SpecialMask
            || key.HasFlag (KeyCode.CtrlMask)
            || key.HasFlag (KeyCode.AltMask))
        {
            return default (Rune);
        }

        // Extract the base key code
        KeyCode baseKey = key;

        if (baseKey.HasFlag (KeyCode.ShiftMask))
        {
            baseKey &= ~KeyCode.ShiftMask;
        }

        switch (baseKey)
        {
            case >= KeyCode.A and <= KeyCode.Z when !key.HasFlag (KeyCode.ShiftMask):
                return new ((uint)(baseKey + 32));
            case >= KeyCode.A and <= KeyCode.Z when key.HasFlag (KeyCode.ShiftMask):
                return new ((uint)baseKey);
            case > KeyCode.Null and < KeyCode.A:
                return new ((uint)baseKey);
        }

        if (Enum.IsDefined (typeof (KeyCode), baseKey))
        {
            return default (Rune);
        }

        return new ((uint)baseKey);
    }

    /// <summary>
    ///     Explicitly cast a <see cref="Key"/> to a <see cref="Rune"/>. The conversion is lossy because properties such
    ///     as <see cref="Handled"/> are not encoded in <see cref="KeyCode"/>.
    /// </summary>
    /// <remarks>Uses <see cref="AsRune"/>.</remarks>
    /// <param name="key"></param>
    public static explicit operator Rune (Key key) => key.AsRune ();

    /// <summary>
    ///     Explicitly cast <see cref="Key"/> to a <see langword="uint"/>. The conversion is lossy because properties such
    ///     as <see cref="Handled"/> are not encoded in <see cref="KeyCode"/>.
    /// </summary>
    /// <param name="key"></param>
    public static explicit operator uint (Key key) => (uint)key.KeyCode;

    /// <summary>
    ///     Explicitly cast <see cref="Key"/> to a <see cref="KeyCode"/>. The conversion is lossy because properties such
    ///     as <see cref="Handled"/> are not encoded in <see cref="KeyCode"/>.
    /// </summary>
    /// <param name="key"></param>
    public static explicit operator KeyCode (Key key) { return key.KeyCode; }

    /// <summary>Cast <see cref="KeyCode"/> to a <see cref="Key"/>.</summary>
    /// <param name="keyCode"></param>
    public static implicit operator Key (KeyCode keyCode) { return new (keyCode); }

    /// <summary>Cast <see langword="char"/> to a <see cref="Key"/>.</summary>
    /// <remarks>See <see cref="Key(char)"/> for more information.</remarks>
    /// <param name="ch"></param>
    public static implicit operator Key (char ch) { return new (ch); }

    /// <summary>Cast <see langword="string"/> to a <see cref="Key"/>.</summary>
    /// <remarks>See <see cref="Key(string)"/> for more information.</remarks>
    /// <param name="str"></param>
    public static implicit operator Key (string str) { return new (str); }

    /// <summary>Cast a <see cref="Key"/> to a <see langword="string"/>.</summary>
    /// <remarks>See <see cref="Key(string)"/> for more information.</remarks>
    /// <param name="key"></param>
    public static implicit operator string (Key key) { return key.ToString (); }

    /// <inheritdoc/>
    public override int GetHashCode () { return (int)KeyCode; }

    /// <summary>Compares two <see cref="Key"/>s for less-than.</summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator < (Key a, Key b) { return a?.KeyCode < b?.KeyCode; }

    /// <summary>Compares two <see cref="Key"/>s for greater-than.</summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator > (Key a, Key b) { return a?.KeyCode > b?.KeyCode; }

    /// <summary>Compares two <see cref="Key"/>s for greater-than-or-equal-to.</summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator <= (Key a, Key b) { return a?.KeyCode <= b?.KeyCode; }

    /// <summary>Compares two <see cref="Key"/>s for greater-than-or-equal-to.</summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator >= (Key a, Key b) { return a?.KeyCode >= b?.KeyCode; }
}
