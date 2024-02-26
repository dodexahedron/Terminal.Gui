﻿#nullable enable
using System.Numerics;

namespace Terminal.Gui;

// TODO: Add events that notify when StraightLine changes to enable dynamic layout
// QUESTION: Should the properties be public-settable, as they currently are?
/// <summary>A line between two points on a horizontal or vertical <see cref="Orientation"/> and a given style/color.</summary>
/// <param name="Start">The <see cref="Point" /> at which the <see cref="StraightLine" /> starts</param>
/// <param name="Length">The length of the line</param>
/// <param name="Orientation">The orientation (horizontal or vertical) of the line</param>
/// <param name="Style">The <see cref="LineStyle"/> of the line</param>
/// <param name="Attribute">The color of the line</param>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record StraightLine (
    Point Start,
    int Length,
    Orientation Orientation,
    LineStyle Style,
    Attribute? Attribute = default
    ) : IEqualityOperators<StraightLine, StraightLine, bool>
{

    /// <summary>Gets or sets the color of the line.</summary>
    public Attribute? Attribute { get; set; } = Attribute;

    /// <summary>Gets or sets the length of the line.</summary>
    public int Length { get; set; } = Length;

    /// <summary>Gets or sets the orientation (horizontal or vertical) of the line.</summary>
    public Orientation Orientation { get; set; } = Orientation;

    /// <summary>Gets or sets where the line begins.</summary>
    public Point Start { get; set; } = Start;

    /// <summary>Gets or sets the line style of the line (e.g. dotted, double).</summary>
    public LineStyle Style { get; set; } = Style;

    /// <summary>
    ///     Gets the rectangle that describes the bounds of the canvas. Location is the coordinates of the line that is
    ///     furthest left/top and Size is defined by the line that extends the furthest right/bottom.
    /// </summary>
    // PERF: Probably better to store the rectangle rather than make a new one on every single access to Bounds.
    internal Rectangle Bounds
    {
        get
        {
            // PERF: Improvements likely possible here.
            // We can almost certainly do this more cleanly and/or more efficiently.
            // At minimum, recalculating it on every call seems wasteful.
            // 0 and 1/-1 Length means a size (width or height) of 1
            int size = Math.Max (1, Math.Abs (Length));

            // How much to offset x or y to get the start of the line
            int offset = Math.Abs (Length < 0 ? Length + 1 : 0);
            int x = Start.X - (Orientation == Orientation.Horizontal ? offset : 0);
            int y = Start.Y - (Orientation == Orientation.Vertical ? offset : 0);
            int width = Orientation == Orientation.Horizontal ? size : 1;
            int height = Orientation == Orientation.Vertical ? size : 1;

            return new (x, y, width, height);
        }
    }

    /// <summary>Formats the Line as a string in (Start.X,Start.Y,Length,Orientation) notation.</summary>
    public override string ToString () { return $"({Start.X},{Start.Y},{Length},{Orientation})"; }

    internal IntersectionDefinition? Intersects (int x, int y)
    {
        return Orientation switch
               {
                   Orientation.Horizontal => IntersectsHorizontally (x, y),
                   Orientation.Vertical => IntersectsVertically (x, y),
                   // BUG: This is a bad exception to throw here.
                   // This should be an InvalidOperationException.
                   // ArgumentOutOfRangeException is for arguments, not members.
                   _ => throw new ArgumentOutOfRangeException (nameof (Orientation))
               };
    }

    private bool EndsAt (int x, int y)
    {
        int sub = Length == 0 ? 0 :
                  Length > 0 ? 1 : -1;

        if (Orientation == Orientation.Horizontal)
        {
            return Start.X + Length - sub == x && Start.Y == y;
        }

        return Start.X == x && Start.Y + Length - sub == y;
    }

    private IntersectionType GetTypeByLength (
        IntersectionType typeWhenNegative,
        IntersectionType typeWhenZero,
        IntersectionType typeWhenPositive
    )
    {
        if (Length == 0)
        {
            return typeWhenZero;
        }

        return Length < 0 ? typeWhenNegative : typeWhenPositive;
    }

    private IntersectionDefinition? IntersectsHorizontally (int x, int y)
    {
        if (Start.Y != y)
        {
            return null;
        }

        if (StartsAt (x, y))
        {
            return new (
                        Start,
                        GetTypeByLength (
                                         IntersectionType.StartLeft,
                                         IntersectionType.PassOverHorizontal,
                                         IntersectionType.StartRight
                                        ),
                        this
                       );
        }

        if (EndsAt (x, y))
        {
            return new (
                        Start,
                        Length < 0 ? IntersectionType.StartRight : IntersectionType.StartLeft,
                        this
                       );
        }

        int xMin = Math.Min (Start.X, Start.X + Length);
        int xMax = Math.Max (Start.X, Start.X + Length);

        if (xMin < x && xMax > x)
        {
            return new (
                        new (x, y),
                        IntersectionType.PassOverHorizontal,
                        this
                       );
        }

        return null;
    }

    // INTENT: Why is this nullable?
    // The name implies it's a boolean, but it's not.
    // It's also private and only referenced one time, so maybe we just nuke it?
    // If it stays, I vote it returns a boolean with the IntersectionDefinition provided via out-reference.
    private IntersectionDefinition? IntersectsVertically (int x, int y)
    {
        if (Start.X != x)
        {
            return null;
        }

        if (StartsAt (x, y))
        {
            return new (
                        Start,
                        GetTypeByLength (
                                         IntersectionType.StartUp,
                                         IntersectionType.PassOverVertical,
                                         IntersectionType.StartDown
                                        ),
                        this
                       );
        }

        if (EndsAt (x, y))
        {
            return new (
                        Start,
                        Length < 0 ? IntersectionType.StartDown : IntersectionType.StartUp,
                        this
                       );
        }

        int yMin = Math.Min (Start.Y, Start.Y + Length);
        int yMax = Math.Max (Start.Y, Start.Y + Length);

        if (yMin < y && yMax > y)
        {
            return new (
                        new (x, y),
                        IntersectionType.PassOverVertical,
                        this
                       );
        }

        return null;
    }

    private bool StartsAt (int x, int y) { return Start.X == x && Start.Y == y; }
}
