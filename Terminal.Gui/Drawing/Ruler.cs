#nullable enable
namespace Terminal.Gui;

/// <summary>Draws a ruler on the screen.</summary>
/// <remarks>
///     <para></para>
/// </remarks>
public class Ruler
{
    /// <summary>Gets or sets the foreground and background color to use.</summary>
    public Attribute Attribute { get; set; } = new ();

    /// <summary>Gets or sets the length of the ruler. The default is 0.</summary>
    public int Length { get; set; }

    /// <summary>Gets or sets whether the ruler is drawn horizontally or vertically. The default is horizontally.</summary>
    public Orientation Orientation { get; set; }

    private const string HTemplate = "|123456789";
    private const string VTemplate = "-123456789";

    /// <summary>Draws the <see cref="Ruler"/>.</summary>
    /// <param name="location">The location to start drawing the ruler, in screen-relative coordinates.</param>
    /// <param name="start">The start value of the ruler.</param>
    /// <remarks>Immediately returns without performing any work if the <see cref="Length" /> property is less than 1.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="start" /> is less than 0</exception>
    public void Draw (in Point location, int start = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan (start, 0);

        if (Length < 1)
        {
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            // PERF: Looks like we can probably avoid some work and garbage here.
            // See detailed comment below.
            string hRule =
                HTemplate.Repeat ((int)Math.Ceiling (Length + 2 / (double)HTemplate.Length))? [start..(Length + start)] ?? string.Empty;

            // Top
            Application.Driver.Move (location.X, location.Y);
            Application.Driver.AddStr (hRule);
        }
        else
        {
            // PERF: Looks like we can probably avoid some work and garbage here.
            // An idea for both of these, though, is that we can just define a static or constant template string at startup that
            // is larger than the console can be (in worst case, it's limited to short.MaxValue),
            // and then just slice it when we need it, rather than doing this computation on every call to Draw.
            // Alternatively, we could still compute it fewer times by only computing the string at construction or modification
            // of the Ruler.
            // We could even force a string.Intern on it at that point so it's not considered garbage after use.
            // This applies to HTemplate, as well.
            string vRule =
                VTemplate.Repeat ((int)Math.Ceiling ((Length + 2) / (double)VTemplate.Length))? [start..(Length + start)] ?? string.Empty;

            for (int r = location.Y; r < location.Y + Length; r++)
            {
                Application.Driver.Move (location.X, r);
                Application.Driver.AddRune ((Rune)vRule [r - location.Y]);
            }
        }
    }
}
