//
// Driver.cs: Curses-based Driver
//

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unix.Terminal;

namespace Terminal.Gui;

/// <summary>This is the Curses driver for the gui.cs/Terminal framework.</summary>
internal class CursesDriver : ConsoleDriver
{
    // These might seem silly, but the compiled IL was actually doing type conversions.
    // So I'm pulling out the constants here for now.
    private const KeyCode AltMask = KeyCode.AltMask;
    private const KeyCode AltSpace = KeyCode.AltMask | KeyCode.Space;
    private const KeyCode CtrlAltSpace = KeyCode.CtrlMask | AltSpace;
    public Curses.Window _window;
    private CursorVisibility? _currentCursorVisibility;
    private CursorVisibility? _initialCursorVisibility;
    private MouseFlags _lastMouseFlags;
    private UnixMainLoop _mainLoopDriver;
    private object _processInputToken;

    public override int Cols
    {
        get => Curses.Cols;
        internal set
        {
            Curses.Cols = value;
            ClearContents ();
        }
    }

    public override int Rows
    {
        get => Curses.Lines;
        internal set
        {
            Curses.Lines = value;
            ClearContents ();
        }
    }

    public override bool SupportsTrueColor => false;

    /// <inheritdoc/>
    public override bool EnsureCursorVisibility () { return false; }

    /// <inheritdoc/>
    public override bool GetCursorVisibility (out CursorVisibility visibility)
    {
        visibility = CursorVisibility.Invisible;

        if (!_currentCursorVisibility.HasValue)
        {
            return false;
        }

        visibility = _currentCursorVisibility.Value;

        return true;
    }

    public override string GetVersionInfo () { return $"{Curses.curses_version ()}"; }

    public static bool Is_WSL_Platform ()
    {
        // xclip does not work on WSL, so we need to use the Windows clipboard vis Powershell
        //if (new CursesClipboard ().IsSupported) {
        //	// If xclip is installed on Linux under WSL, this will return true.
        //	return false;
        //}
        (int exitCode, string result) = ClipboardProcessRunner.Bash ("uname -a", waitForOutput: true);

        if (exitCode == 0 && result.Contains ("microsoft") && result.Contains ("WSL"))
        {
            return true;
        }

        return false;
    }

    public override bool IsRuneSupported (Rune rune)
    {
        // See Issue #2615 - CursesDriver is broken with non-BMP characters
        return base.IsRuneSupported (rune) && rune.IsBmp;
    }

    public override void Move (int col, int row)
    {
        base.Move (col, row);

        if (RunningUnitTests)
        {
            return;
        }

        if (IsValidLocation (col, row))
        {
            Curses.move (row, col);
        }
        else
        {
            // Not a valid location (outside screen or clip region)
            // Move within the clip region, then AddRune will actually move to Col, Row
            Curses.move (Clip.Y, Clip.X);
        }
    }

    public override void Refresh ()
    {
        UpdateScreen ();
        UpdateCursor ();
    }

    public override void SendKeys (char keyChar, ConsoleKey consoleKey, bool shift, bool alt, bool control)
    {
        KeyCode key;

        if (consoleKey == ConsoleKey.Packet)
        {
            var mod = new ConsoleModifiers ();

            if (shift)
            {
                mod |= ConsoleModifiers.Shift;
            }

            if (alt)
            {
                mod |= ConsoleModifiers.Alt;
            }

            if (control)
            {
                mod |= ConsoleModifiers.Control;
            }

            var cKeyInfo = new ConsoleKeyInfo (keyChar, consoleKey, shift, alt, control);
            cKeyInfo = ConsoleKeyMapping.DecodeVKPacketToKConsoleKeyInfo (cKeyInfo);
            key = ConsoleKeyMapping.MapConsoleKeyInfoToKeyCode (cKeyInfo);
        }
        else
        {
            key = (KeyCode)keyChar;
        }

        OnKeyDown (new (new (key)));
        OnKeyUp (new (new (key)));

        //OnKeyPressed (new KeyEventArgsEventArgs (key));
    }

    /// <inheritdoc/>
    public override bool SetCursorVisibility (CursorVisibility visibility)
    {
        if (_initialCursorVisibility.HasValue == false)
        {
            return false;
        }

        if (!RunningUnitTests)
        {
            Curses.curs_set (((int)visibility >> 16) & 0x000000FF);
        }

        if (visibility != CursorVisibility.Invisible)
        {
            Console.Out.Write (
                               EscSeqUtils.CSI_SetCursorStyle (
                                                               (EscSeqUtils.DECSCUSR_Style)(((int)visibility >> 24)
                                                                                            & 0xFF)
                                                              )
                              );
        }

        _currentCursorVisibility = visibility;

        return true;
    }

    public void StartReportingMouseMoves ()
    {
        if (!RunningUnitTests)
        {
            Console.Out.Write (EscSeqUtils.CSI_EnableMouseEvents);
        }
    }

    public void StopReportingMouseMoves ()
    {
        if (!RunningUnitTests)
        {
            Console.Out.Write (EscSeqUtils.CSI_DisableMouseEvents);
        }
    }

    public override void Suspend ()
    {
        StopReportingMouseMoves ();

        if (!RunningUnitTests)
        {
            Platform.Suspend ();
            Curses.Window.Standard.redrawwin ();
            Curses.refresh ();
        }

        StartReportingMouseMoves ();
    }

    public override void UpdateCursor ()
    {
        EnsureCursorVisibility ();

        if (!RunningUnitTests && Col >= 0 && Col < Cols && Row >= 0 && Row < Rows)
        {
            Curses.move (Row, Col);
        }
    }

    public override void UpdateScreen ()
    {
        for (var row = 0; row < Rows; row++)
        {
            if (!_dirtyLines [row])
            {
                continue;
            }

            _dirtyLines [row] = false;

            for (var col = 0; col < Cols; col++)
            {
                if (Contents [row, col].IsDirty == false)
                {
                    continue;
                }

                if (RunningUnitTests)
                {
                    // In unit tests, we don't want to actually write to the screen.
                    continue;
                }

                Curses.attrset (Contents [row, col].Attribute.GetValueOrDefault ().PlatformColor);

                Rune rune = Contents [row, col].Rune;

                if (rune.IsBmp)
                {
                    // BUGBUG: CursesDriver doesn't render CharMap correctly for wide chars (and other Unicode) - Curses is doing something funky with glyphs that report GetColums() of 1 yet are rendered wide. E.g. 0x2064 (invisible times) is reported as 1 column but is rendered as 2. WindowsDriver & NetDriver correctly render this as 1 column, overlapping the next cell.
                    if (rune.GetColumns () < 2)
                    {
                        Curses.mvaddch (row, col, rune.Value);
                    }
                    else /*if (col + 1 < Cols)*/
                    {
                        Curses.mvaddwstr (row, col, rune.ToString ());
                    }
                }
                else
                {
                    Curses.mvaddwstr (row, col, rune.ToString ());

                    if (rune.GetColumns () > 1 && col + 1 < Cols)
                    {
                        // TODO: This is a hack to deal with non-BMP and wide characters.
                        //col++;
                        Curses.mvaddch (row, ++col, '*');
                    }
                }
            }
        }

        if (!RunningUnitTests)
        {
            Curses.move (Row, Col);
            _window.wrefresh ();
        }
    }

    internal override void End ()
    {
        StopReportingMouseMoves ();
        SetCursorVisibility (CursorVisibility.Default);

        if (_mainLoopDriver is { })
        {
            _mainLoopDriver.RemoveWatch (_processInputToken);
        }

        if (RunningUnitTests)
        {
            return;
        }

        // throws away any typeahead that has been typed by
        // the user and has not yet been read by the program.
        Curses.flushinp ();

        Curses.endwin ();
    }

    internal override MainLoop Init ()
    {
        _mainLoopDriver = new UnixMainLoop (this);

        if (!RunningUnitTests)
        {
            _window = Curses.initscr ();
            Curses.set_escdelay (10);

            // Ensures that all procedures are performed at some previous closing.
            Curses.doupdate ();

            // 
            // We are setting Invisible as default so we could ignore XTerm DECSUSR setting
            //
            switch (Curses.curs_set (0))
            {
                case 0:
                    _currentCursorVisibility = _initialCursorVisibility = CursorVisibility.Invisible;

                    break;

                case 1:
                    _currentCursorVisibility = _initialCursorVisibility = CursorVisibility.Underline;
                    Curses.curs_set (1);

                    break;

                case 2:
                    _currentCursorVisibility = _initialCursorVisibility = CursorVisibility.Box;
                    Curses.curs_set (2);

                    break;

                default:
                    _currentCursorVisibility = _initialCursorVisibility = null;

                    break;
            }

            if (!Curses.HasColors)
            {
                throw new InvalidOperationException ("V2 - This should never happen. File an Issue if it does.");
            }

            Curses.raw ();
            Curses.noecho ();

            Curses.Window.Standard.keypad (true);

            Curses.StartColor ();
            Curses.UseDefaultColors ();

            if (!RunningUnitTests)
            {
                Curses.timeout (0);
            }

            _processInputToken = _mainLoopDriver?.AddWatch (
                                                            0,
                                                            UnixMainLoop.Condition.PollIn,
                                                            x =>
                                                            {
                                                                ProcessInput ();

                                                                return true;
                                                            }
                                                           );
        }

        CurrentAttribute = new Attribute (ColorName.White, ColorName.Black);

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Clipboard = new FakeDriver.FakeClipboard ();
        }
        else
        {
            if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX))
            {
                Clipboard = new MacOSXClipboard ();
            }
            else
            {
                if (Is_WSL_Platform ())
                {
                    Clipboard = new WSLClipboard ();
                }
                else
                {
                    Clipboard = new CursesClipboard ();
                }
            }
        }

        ClearContents ();
        StartReportingMouseMoves ();

        if (!RunningUnitTests)
        {
            Curses.CheckWinChange ();
            Curses.refresh ();
        }

        return new MainLoop (_mainLoopDriver);
    }

    internal void ProcessInput ()
    {
        EscapeSequenceData input = new ();
        input.code = Curses.get_wch (out input.wch);

        //System.Diagnostics.Debug.WriteLine ($"code: {code}; input.wch: {input.wch}");
        if (input.code == Curses.ERR)
        {
            return;
        }

        input.k = KeyCode.Null;

        if (input.code == Curses.KEY_CODE_YES)
        {
            while (input is { code: Curses.KEY_CODE_YES, wch: Curses.KeyResize })
            {
                ProcessWinChange ();
                input.code = Curses.get_wch (out input.wch);
            }

            if (input.wch == 0)
            {
                return;
            }

            if (input.wch == Curses.KeyMouse)
            {
                while (input.wch == Curses.KeyMouse)
                {
                    input.cki =
                    [
                        new ((char)KeyCode.Esc, 0, false, false, false),
                        new ('[', 0, false, false, false),
                        new ('<', 0, false, false, false)
                    ];

                    input.code = 0;
                    input = HandleEscapeSeqResponse (ref input);
                }

                return;
            }

            input.k = MapCursesKey (input.wch);

            switch (input.wch)
            {
                case >= 277 and <= 288:
                    // Shift+(F1 - F12)
                    input.wch -= 12;
                    input.k = KeyCode.ShiftMask | MapCursesKey (input.wch);

                    break;
                case <= 300:
                    // Ctrl+(F1 - F12)
                    input.wch -= 24;
                    input.k = KeyCode.CtrlMask | MapCursesKey (input.wch);

                    break;
                case <= 312:
                    // Ctrl+Shift+(F1 - F12)
                    input.wch -= 36;
                    input.k = KeyCode.CtrlMask | KeyCode.ShiftMask | MapCursesKey (input.wch);

                    break;
                case <= 324:
                    // Alt+(F1 - F12)
                    input.wch -= 48;
                    input.k = AltMask | MapCursesKey (input.wch);

                    break;
                case <= 327:
                    // Shift+Alt+(F1 - F3)
                    input.wch -= 60;
                    input.k = KeyCode.ShiftMask | AltMask | MapCursesKey (input.wch);

                    break;
            }

            OnKeyDown (new (input.k));
            OnKeyUp (new (input.k));

            return;
        }

        // Special handling for ESC, we want to try to catch ESC+letter to simulate alt-letter as well as Alt-Fkey
        const uint keyCodeUInt32A = (uint)KeyCode.A;
        const uint keyCodeUInt32Z = (uint)KeyCode.Z;

        const uint keyCodeUInt32AMinus64 = keyCodeUInt32A - 64;

        if (input.wch == 27)
        {
            Curses.timeout (10);

            input.code = Curses.get_wch (out int wch2);
            KeyCode kc = Unsafe.As<int,KeyCode>(ref wch2) ;
            if (input.code == Curses.KEY_CODE_YES)
            {
                input.k = AltMask | MapCursesKey (input.wch);
            }

            if (input.code == 0)
            {
                // The ESC-number handling, debatable.
                // Simulates the AltMask itself by pressing Alt + Space.
                if (kc == KeyCode.Space)
                {
                    input.k = AltMask;
                }
                else if ((kc - KeyCode.Space) is >= keyCodeUInt32A and <= keyCodeUInt32Z)
                {
                    input.k = (KeyCode)((uint)AltMask + (wch2 - (int)KeyCode.Space));
                }
                else if (wch2 >= keyCodeUInt32AMinus64 && wch2 <= keyCodeUInt32Z - 64)
                {
                    input.k = (KeyCode)((uint)(AltMask | KeyCode.CtrlMask) + (wch2 + 64));
                }
                else if (wch2 >= (uint)KeyCode.D0 && wch2 <= (uint)KeyCode.D9)
                {
                    input.k = (KeyCode)((uint)AltMask + (uint)KeyCode.D0 + (wch2 - (uint)KeyCode.D0));
                }
                else if (wch2 == Curses.KeyCSI)
                {
                    input.cki =
                    [
                        new ((char)KeyCode.Esc, 0, false, false, false), new ('[', 0, false, false, false)
                    ];

                    input = HandleEscapeSeqResponse (ref input);

                    return;
                }
                else
                {
                    // Unfortunately there are no way to differentiate Ctrl+Alt+alfa and Ctrl+Shift+Alt+alfa.
                    if (((KeyCode)wch2 & KeyCode.CtrlMask) != 0)
                    {
                        input.k = (KeyCode)((uint)KeyCode.CtrlMask + (wch2 & ~(int)KeyCode.CtrlMask));
                    }

                    if (wch2 == 0)
                    {
                        input.k = CtrlAltSpace;
                    }
                    else
                    {
                        const int keyCodeAInt = (int)KeyCode.A; // 65
                        const int keyCodeZInt = (int)KeyCode.Z; // 90

                        if (input is {wch: >= keyCodeAInt and <= keyCodeZInt})
                        {
                            input.k = KeyCode.ShiftMask | AltSpace;
                        }
                        else if (wch2 < 256)
                        {
                            input.k = (KeyCode)wch2; // | KeyCode.AltMask;
                        }
                        else
                        {
                            input.k = (KeyCode)((uint)(AltMask | KeyCode.CtrlMask) + wch2);
                        }
                    }
                }

                input.Key = new (input.k);
            }
            else
            {
                input.Key = Key.Esc;
            }

            OnKeyDown (new(input.Key));
            OnKeyUp (new(input.Key));
        }
        else if (input.wch == Curses.KeyTab)
        {
            input.k = MapCursesKey (input.wch);
            OnKeyDown (new (input.Key));
            OnKeyUp (new (input.Key));
        }
        else
        {
            // Unfortunately there are no way to differentiate Ctrl+alfa and Ctrl+Shift+alfa.
            input.k = (KeyCode)input.wch;

            if (input.wch == 0)
            {
                input.k = KeyCode.CtrlMask | KeyCode.Space;
            }
            else if (input.wch >= keyCodeUInt32AMinus64 && input.wch <= keyCodeUInt32Z - 64)
            {
                if ((KeyCode)(input.wch + 64) != KeyCode.J)
                {
                    input.k = KeyCode.CtrlMask | (KeyCode)(input.wch + 64);
                }
            }
            else if (input.wch >= keyCodeUInt32A && input.wch <= keyCodeUInt32Z)
            {
                input.k = (KeyCode)input.wch | KeyCode.ShiftMask;
            }

            if (input.wch == '\n' || input.wch == '\r')
            {
                input.k = KeyCode.Enter;
            }

            OnKeyDown (new (input.k));
            OnKeyUp (new (input.k));
        }
    }

    internal void ProcessWinChange ()
    {
        if (!RunningUnitTests && Curses.CheckWinChange ())
        {
            ClearContents ();
            OnSizeChanged (new SizeChangedEventArgs (new (Cols, Rows)));
        }
    }

    private ref EscapeSequenceData HandleEscapeSeqResponse (ref EscapeSequenceData data)
    {
        ConsoleKey ck = 0;
        ConsoleModifiers mod = 0;

        while (data.code == 0)
        {
            data.code = Curses.get_wch (out data.wch);
            var consoleKeyInfo = new ConsoleKeyInfo ((char)data.wch, 0, false, false, false);

            if (data.wch == 0 || data.wch == 27 || data.wch == Curses.KeyMouse)
            {
                EscSeqUtils.DecodeEscSeq (
                                          null,
                                          ref consoleKeyInfo,
                                          ref ck,
                                          data.cki,
                                          ref mod,
                                          out _,
                                          out _,
                                          out _,
                                          out _,
                                          out bool isKeyMouse,
                                          out List<MouseFlags> mouseFlags,
                                          out Point pos,
                                          out _,
                                          ProcessMouseEvent
                                         );

                if (isKeyMouse)
                {
                    foreach (MouseFlags mf in mouseFlags)
                    {
                        ProcessMouseEvent (mf, pos);
                    }

                    data.cki = null;

                    if (data.wch == 27)
                    {
                        data.cki = EscSeqUtils.ResizeArray (
                                                       new ConsoleKeyInfo (
                                                                           (char)KeyCode.Esc,
                                                                           0,
                                                                           false,
                                                                           false,
                                                                           false
                                                                          ),
                                                       data.cki
                                                      );
                    }
                }
                else
                {
                    data.k = ConsoleKeyMapping.MapConsoleKeyInfoToKeyCode (consoleKeyInfo);
                    data.Key = data.Key;
                    OnKeyDown (new (data.Key));
                }
            }
            else
            {
                data.cki = EscSeqUtils.ResizeArray (consoleKeyInfo, data.cki);
            }
        }
        return ref data;
    }

    private static KeyCode MapCursesKey (int cursesKey)
    {
        return cursesKey switch
               {
                   Curses.KeyF1 => KeyCode.F1,
                   Curses.KeyF2 => KeyCode.F2,
                   Curses.KeyF3 => KeyCode.F3,
                   Curses.KeyF4 => KeyCode.F4,
                   Curses.KeyF5 => KeyCode.F5,
                   Curses.KeyF6 => KeyCode.F6,
                   Curses.KeyF7 => KeyCode.F7,
                   Curses.KeyF8 => KeyCode.F8,
                   Curses.KeyF9 => KeyCode.F9,
                   Curses.KeyF10 => KeyCode.F10,
                   Curses.KeyF11 => KeyCode.F11,
                   Curses.KeyF12 => KeyCode.F12,
                   Curses.KeyUp => KeyCode.CursorUp,
                   Curses.KeyDown => KeyCode.CursorDown,
                   Curses.KeyLeft => KeyCode.CursorLeft,
                   Curses.KeyRight => KeyCode.CursorRight,
                   Curses.KeyHome => KeyCode.Home,
                   Curses.KeyEnd => KeyCode.End,
                   Curses.KeyNPage => KeyCode.PageDown,
                   Curses.KeyPPage => KeyCode.PageUp,
                   Curses.KeyDeleteChar => KeyCode.Delete,
                   Curses.KeyInsertChar => KeyCode.Insert,
                   Curses.KeyTab => KeyCode.Tab,
                   Curses.KeyBackTab => KeyCode.Tab | KeyCode.ShiftMask,
                   Curses.KeyBackspace => KeyCode.Backspace,
                   Curses.ShiftKeyUp => KeyCode.CursorUp | KeyCode.ShiftMask,
                   Curses.ShiftKeyDown => KeyCode.CursorDown | KeyCode.ShiftMask,
                   Curses.ShiftKeyLeft => KeyCode.CursorLeft | KeyCode.ShiftMask,
                   Curses.ShiftKeyRight => KeyCode.CursorRight | KeyCode.ShiftMask,
                   Curses.ShiftKeyHome => KeyCode.Home | KeyCode.ShiftMask,
                   Curses.ShiftKeyEnd => KeyCode.End | KeyCode.ShiftMask,
                   Curses.ShiftKeyNPage => KeyCode.PageDown | KeyCode.ShiftMask,
                   Curses.ShiftKeyPPage => KeyCode.PageUp | KeyCode.ShiftMask,
                   Curses.AltKeyUp => KeyCode.CursorUp | KeyCode.AltMask,
                   Curses.AltKeyDown => KeyCode.CursorDown | KeyCode.AltMask,
                   Curses.AltKeyLeft => KeyCode.CursorLeft | KeyCode.AltMask,
                   Curses.AltKeyRight => KeyCode.CursorRight | KeyCode.AltMask,
                   Curses.AltKeyHome => KeyCode.Home | KeyCode.AltMask,
                   Curses.AltKeyEnd => KeyCode.End | KeyCode.AltMask,
                   Curses.AltKeyNPage => KeyCode.PageDown | KeyCode.AltMask,
                   Curses.AltKeyPPage => KeyCode.PageUp | KeyCode.AltMask,
                   Curses.CtrlKeyUp => KeyCode.CursorUp | KeyCode.CtrlMask,
                   Curses.CtrlKeyDown => KeyCode.CursorDown | KeyCode.CtrlMask,
                   Curses.CtrlKeyLeft => KeyCode.CursorLeft | KeyCode.CtrlMask,
                   Curses.CtrlKeyRight => KeyCode.CursorRight | KeyCode.CtrlMask,
                   Curses.CtrlKeyHome => KeyCode.Home | KeyCode.CtrlMask,
                   Curses.CtrlKeyEnd => KeyCode.End | KeyCode.CtrlMask,
                   Curses.CtrlKeyNPage => KeyCode.PageDown | KeyCode.CtrlMask,
                   Curses.CtrlKeyPPage => KeyCode.PageUp | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyUp => KeyCode.CursorUp | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyDown => KeyCode.CursorDown | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyLeft => KeyCode.CursorLeft | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyRight => KeyCode.CursorRight | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyHome => KeyCode.Home | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyEnd => KeyCode.End | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyNPage => KeyCode.PageDown | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftCtrlKeyPPage => KeyCode.PageUp | KeyCode.ShiftMask | KeyCode.CtrlMask,
                   Curses.ShiftAltKeyUp => KeyCode.CursorUp | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyDown => KeyCode.CursorDown | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyLeft => KeyCode.CursorLeft | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyRight => KeyCode.CursorRight | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyNPage => KeyCode.PageDown | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyPPage => KeyCode.PageUp | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyHome => KeyCode.Home | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.ShiftAltKeyEnd => KeyCode.End | KeyCode.ShiftMask | KeyCode.AltMask,
                   Curses.AltCtrlKeyNPage => KeyCode.PageDown | KeyCode.AltMask | KeyCode.CtrlMask,
                   Curses.AltCtrlKeyPPage => KeyCode.PageUp | KeyCode.AltMask | KeyCode.CtrlMask,
                   Curses.AltCtrlKeyHome => KeyCode.Home | KeyCode.AltMask | KeyCode.CtrlMask,
                   Curses.AltCtrlKeyEnd => KeyCode.End | KeyCode.AltMask | KeyCode.CtrlMask,
                   _ => KeyCode.Null
               };
    }

    private void ProcessMouseEvent (MouseFlags mouseFlag, Point pos)
    {
        bool WasButtonReleased (MouseFlags flag)
        {
            return flag.HasFlag (MouseFlags.Button1Released)
                   || flag.HasFlag (MouseFlags.Button2Released)
                   || flag.HasFlag (MouseFlags.Button3Released)
                   || flag.HasFlag (MouseFlags.Button4Released);
        }

        bool IsButtonNotPressed (MouseFlags flag)
        {
            return !flag.HasFlag (MouseFlags.Button1Pressed)
                   && !flag.HasFlag (MouseFlags.Button2Pressed)
                   && !flag.HasFlag (MouseFlags.Button3Pressed)
                   && !flag.HasFlag (MouseFlags.Button4Pressed);
        }

        bool IsButtonClickedOrDoubleClicked (MouseFlags flag)
        {
            return flag.HasFlag (MouseFlags.Button1Clicked)
                   || flag.HasFlag (MouseFlags.Button2Clicked)
                   || flag.HasFlag (MouseFlags.Button3Clicked)
                   || flag.HasFlag (MouseFlags.Button4Clicked)
                   || flag.HasFlag (MouseFlags.Button1DoubleClicked)
                   || flag.HasFlag (MouseFlags.Button2DoubleClicked)
                   || flag.HasFlag (MouseFlags.Button3DoubleClicked)
                   || flag.HasFlag (MouseFlags.Button4DoubleClicked);
        }

        if ((WasButtonReleased (mouseFlag) && IsButtonNotPressed (_lastMouseFlags)) || (IsButtonClickedOrDoubleClicked (mouseFlag) && _lastMouseFlags == 0))
        {
            return;
        }

        _lastMouseFlags = mouseFlag;

        var me = new MouseEvent { Flags = mouseFlag, X = pos.X, Y = pos.Y };
        OnMouseEvent (new MouseEventEventArgs (me));
    }

    #region Color Handling

    /// <summary>Creates an Attribute from the provided curses-based foreground and background color numbers</summary>
    /// <param name="foreground">Contains the curses color number for the foreground (color, plus any attributes)</param>
    /// <param name="background">Contains the curses color number for the background (color, plus any attributes)</param>
    /// <returns></returns>
    private static Attribute MakeColor (short foreground, short background)
    {
        var v = (short)(foreground | (background << 4));

        // TODO: for TrueColor - Use InitExtendedPair
        Curses.InitColorPair (v, foreground, background);

        return new Attribute (
                              Curses.ColorPair (v),
                              CursesColorNumberToColorName (foreground),
                              CursesColorNumberToColorName (background)
                             );
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     In the CursesDriver, colors are encoded as an int. The foreground color is stored in the most significant 4
    ///     bits, and the background color is stored in the least significant 4 bits. The Terminal.GUi Color values are
    ///     converted to curses color encoding before being encoded.
    /// </remarks>
    public override Attribute MakeColor (in Color foreground, in Color background)
    {
        if (!RunningUnitTests)
        {
            return MakeColor (
                              ColorNameToCursesColorNumber (foreground.GetClosestNamedColor ()),
                              ColorNameToCursesColorNumber (background.GetClosestNamedColor ())
                             );
        }

        return new Attribute (
                              0,
                              foreground,
                              background
                             );
    }

    private static short ColorNameToCursesColorNumber (ColorName color)
    {
        switch (color)
        {
            case ColorName.Black:
                return Curses.COLOR_BLACK;
            case ColorName.Blue:
                return Curses.COLOR_BLUE;
            case ColorName.Green:
                return Curses.COLOR_GREEN;
            case ColorName.Cyan:
                return Curses.COLOR_CYAN;
            case ColorName.Red:
                return Curses.COLOR_RED;
            case ColorName.Magenta:
                return Curses.COLOR_MAGENTA;
            case ColorName.Yellow:
                return Curses.COLOR_YELLOW;
            case ColorName.Gray:
                return Curses.COLOR_WHITE;
            case ColorName.DarkGray:
                return Curses.COLOR_GRAY;
            case ColorName.BrightBlue:
                return Curses.COLOR_BLUE | Curses.COLOR_GRAY;
            case ColorName.BrightGreen:
                return Curses.COLOR_GREEN | Curses.COLOR_GRAY;
            case ColorName.BrightCyan:
                return Curses.COLOR_CYAN | Curses.COLOR_GRAY;
            case ColorName.BrightRed:
                return Curses.COLOR_RED | Curses.COLOR_GRAY;
            case ColorName.BrightMagenta:
                return Curses.COLOR_MAGENTA | Curses.COLOR_GRAY;
            case ColorName.BrightYellow:
                return Curses.COLOR_YELLOW | Curses.COLOR_GRAY;
            case ColorName.White:
                return Curses.COLOR_WHITE | Curses.COLOR_GRAY;
        }

        throw new ArgumentException ("Invalid color code");
    }

    private static ColorName CursesColorNumberToColorName (short color)
    {
        switch (color)
        {
            case Curses.COLOR_BLACK:
                return ColorName.Black;
            case Curses.COLOR_BLUE:
                return ColorName.Blue;
            case Curses.COLOR_GREEN:
                return ColorName.Green;
            case Curses.COLOR_CYAN:
                return ColorName.Cyan;
            case Curses.COLOR_RED:
                return ColorName.Red;
            case Curses.COLOR_MAGENTA:
                return ColorName.Magenta;
            case Curses.COLOR_YELLOW:
                return ColorName.Yellow;
            case Curses.COLOR_WHITE:
                return ColorName.Gray;
            case Curses.COLOR_GRAY:
                return ColorName.DarkGray;
            case Curses.COLOR_BLUE | Curses.COLOR_GRAY:
                return ColorName.BrightBlue;
            case Curses.COLOR_GREEN | Curses.COLOR_GRAY:
                return ColorName.BrightGreen;
            case Curses.COLOR_CYAN | Curses.COLOR_GRAY:
                return ColorName.BrightCyan;
            case Curses.COLOR_RED | Curses.COLOR_GRAY:
                return ColorName.BrightRed;
            case Curses.COLOR_MAGENTA | Curses.COLOR_GRAY:
                return ColorName.BrightMagenta;
            case Curses.COLOR_YELLOW | Curses.COLOR_GRAY:
                return ColorName.BrightYellow;
            case Curses.COLOR_WHITE | Curses.COLOR_GRAY:
                return ColorName.White;
        }

        throw new ArgumentException ("Invalid curses color code");
    }

    #endregion

    /// <summary>
    /// A private byRefLike struct to simplify calls and keep everything really on the stack.
    /// </summary>
    /// <remarks>
    /// This is a ref struct. It cannot escape the stack and the compiler will enforce that if you try.
    /// </remarks>
    private ref struct EscapeSequenceData
    {
        public ref int code;
        public ref KeyCode k;
        public ref int wch;
        public Key Key;
        public ConsoleKeyInfo [] cki;
    }
}