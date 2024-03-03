namespace Terminal.Gui.ConsoleDrivers;

/// <summary>Enables diagnostic functions</summary>
[Flags]
public enum DiagnosticFlags : uint
{
    /// <summary>All diagnostics off</summary>
    Off = 0b_0000_0000,

    /// <summary>
    ///     When enabled, <see cref="View.OnDrawAdornments"/> will draw a ruler in the frame for any side with a padding
    ///     value greater than 0.
    /// </summary>
    FrameRuler = 0b_0000_0001,

    /// <summary>
    ///     When enabled, <see cref="View.OnDrawAdornments"/> will draw a 'L', 'R', 'T', and 'B' when clearing
    ///     <see cref="Thickness"/>'s instead of ' '.
    /// </summary>
    FramePadding = 0b_0000_0010
}
