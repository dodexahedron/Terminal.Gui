using System.ComponentModel;
using Xunit.Abstractions;

namespace Terminal.Gui.ViewsTests;

public class ButtonTests (ITestOutputHelper output)
{
    // Test that Title and Text are the same
    [Fact]
    public void Text_Mirrors_Title ()
    {
        var view = new Button ();
        view.Title = "Hello";
        Assert.Equal ("Hello", view.Title);
        Assert.Equal ("Hello", view.TitleTextFormatter.Text);

        Assert.Equal ("Hello", view.Text);
        Assert.Equal ($"{CM.Glyphs.LeftBracket} Hello {CM.Glyphs.RightBracket}", view.TextFormatter.Text);
        view.Dispose ();
    }

    [Fact]
    public void Title_Mirrors_Text ()
    {
        var view = new Button ();
        view.Text = "Hello";
        Assert.Equal ("Hello", view.Text);
        Assert.Equal ($"{CM.Glyphs.LeftBracket} Hello {CM.Glyphs.RightBracket}", view.TextFormatter.Text);

        Assert.Equal ("Hello", view.Title);
        Assert.Equal ("Hello", view.TitleTextFormatter.Text);
        view.Dispose ();
    }

    [Theory]
    [InlineData ("01234", 0, 0, 0, 0)]
    [InlineData ("01234", 1, 0, 1, 0)]
    [InlineData ("01234", 0, 1, 0, 1)]
    [InlineData ("01234", 1, 1, 1, 1)]
    [InlineData ("01234", 10, 1, 10, 1)]
    [InlineData ("01234", 10, 3, 10, 3)]
    [InlineData ("0_1234", 0, 0, 0, 0)]
    [InlineData ("0_1234", 1, 0, 1, 0)]
    [InlineData ("0_1234", 0, 1, 0, 1)]
    [InlineData ("0_1234", 1, 1, 1, 1)]
    [InlineData ("0_1234", 10, 1, 10, 1)]
    [InlineData ("0_12你", 10, 3, 10, 3)]
    [InlineData ("0_12你", 0, 0, 0, 0)]
    [InlineData ("0_12你", 1, 0, 1, 0)]
    [InlineData ("0_12你", 0, 1, 0, 1)]
    [InlineData ("0_12你", 1, 1, 1, 1)]
    [InlineData ("0_12你", 10, 1, 10, 1)]
    [InlineData ("0_12你", 10, 3, 10, 3)]
    public void Button_AbsoluteSize_Text (string text, int width, int height, int expectedWidth, int expectedHeight)
    {
        var btn1 = new Button
        {
            Text = text,
            Width = width,
            Height = height,
        };

        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.Frame.Size);
        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.Viewport.Size);
        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.ContentSize);
        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.TextFormatter.Size);

        btn1.Dispose ();
    }

    [Theory]
    [InlineData (0, 0, 0, 0)]
    [InlineData (1, 0, 1, 0)]
    [InlineData (0, 1, 0, 1)]
    [InlineData (1, 1, 1, 1)]
    [InlineData (10, 1, 10, 1)]
    [InlineData (10, 3, 10, 3)]
    public void Button_AbsoluteSize_DefaultText (int width, int height, int expectedWidth, int expectedHeight)
    {
        var btn1 = new Button
        {
            Width = width,
            Height = height,
        };

        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.Frame.Size);
        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.Viewport.Size);
        Assert.Equal (new Size (expectedWidth, expectedHeight), btn1.TextFormatter.Size);

        btn1.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Button_HotKeyChanged_EventFires ()
    {
        var btn = new Button { Text = "_Yar" };

        object sender = null;
        KeyChangedEventArgs args = null;

        btn.HotKeyChanged += (s, e) =>
                             {
                                 sender = s;
                                 args = e;
                             };

        btn.HotKeyChanged += (s, e) =>
                             {
                                 sender = s;
                                 args = e;
                             };

        btn.HotKey = KeyCode.R;
        Assert.Same (btn, sender);
        Assert.Equal (KeyCode.Y, args.OldKey);
        Assert.Equal (KeyCode.R, args.NewKey);
        btn.HotKey = KeyCode.R;
        Assert.Same (btn, sender);
        Assert.Equal (KeyCode.Y, args.OldKey);
        Assert.Equal (KeyCode.R, args.NewKey);
        btn.Dispose ();
    }

    [Fact]
    public void Button_HotKeyChanged_EventFires_WithNone ()
    {
        var btn = new Button ();

        object sender = null;
        KeyChangedEventArgs args = null;

        btn.HotKeyChanged += (s, e) =>
                             {
                                 sender = s;
                                 args = e;
                             };

        btn.HotKey = KeyCode.R;
        Assert.Same (btn, sender);
        Assert.Equal (KeyCode.Null, args.OldKey);
        Assert.Equal (KeyCode.R, args.NewKey);
        btn.Dispose ();
    }

    [Fact]
    [SetupFakeDriver]
    public void Constructors_Defaults ()
    {
        var btn = new Button ();
        Assert.Equal (string.Empty, btn.Text);
        btn.BeginInit ();
        btn.EndInit ();
        btn.SetRelativeLayout (new (100, 100));

        Assert.Equal ($"{CM.Glyphs.LeftBracket}  {CM.Glyphs.RightBracket}", btn.TextFormatter.Text);
        Assert.False (btn.IsDefault);
        Assert.Equal (TextAlignment.Centered, btn.TextAlignment);
        Assert.Equal ('_', btn.HotKeySpecifier.Value);
        Assert.True (btn.CanFocus);
        Assert.Equal (new (0, 0, 4, 1), btn.Viewport);
        Assert.Equal (new (0, 0, 4, 1), btn.Frame);
        Assert.Equal ($"{CM.Glyphs.LeftBracket}  {CM.Glyphs.RightBracket}", btn.TextFormatter.Text);
        Assert.False (btn.IsDefault);
        Assert.Equal (TextAlignment.Centered, btn.TextAlignment);
        Assert.Equal ('_', btn.HotKeySpecifier.Value);
        Assert.True (btn.CanFocus);
        Assert.Equal (new (0, 0, 4, 1), btn.Viewport);
        Assert.Equal (new (0, 0, 4, 1), btn.Frame);

        Assert.Equal (string.Empty, btn.Title);
        Assert.Equal (KeyCode.Null, btn.HotKey);

        btn.Draw ();

        var expected = @$"
{CM.Glyphs.LeftBracket}  {CM.Glyphs.RightBracket}
";
        TestHelpers.AssertDriverContentsWithFrameAre (expected, output);
        btn.Dispose ();

        btn = new () { Text = "_Test", IsDefault = true };
        Assert.Equal (new (10, 1), btn.TextFormatter.Size);



        btn.BeginInit ();
        btn.EndInit ();
        Assert.Equal ('_', btn.HotKeySpecifier.Value);
        Assert.Equal (Key.T, btn.HotKey);
        Assert.Equal ("_Test", btn.Text);

        Assert.Equal (
                      $"{CM.Glyphs.LeftBracket}{CM.Glyphs.LeftDefaultIndicator} Test {CM.Glyphs.RightDefaultIndicator}{CM.Glyphs.RightBracket}",
                      btn.TextFormatter.Format ()
                     );
        Assert.True (btn.IsDefault);
        Assert.Equal (TextAlignment.Centered, btn.TextAlignment);
        Assert.True (btn.CanFocus);

        btn.SetRelativeLayout (new (100, 100));
        // 0123456789012345678901234567890123456789
        // [* Test *]
        Assert.Equal ('_', btn.HotKeySpecifier.Value);
        Assert.Equal (10, btn.TextFormatter.Format ().Length);
        Assert.Equal (new (10, 1), btn.TextFormatter.Size);
        Assert.Equal (new (10, 1), btn.ContentSize);
        Assert.Equal (new (0, 0, 10, 1), btn.Viewport);
        Assert.Equal (new (0, 0, 10, 1), btn.Frame);
        Assert.Equal (KeyCode.T, btn.HotKey);

        btn.Dispose ();

        btn = new () { X = 1, Y = 2, Text = "_abc", IsDefault = true };
        btn.BeginInit ();
        btn.EndInit ();
        Assert.Equal ("_abc", btn.Text);
        Assert.Equal (Key.A, btn.HotKey);

        Assert.Equal (
                      $"{CM.Glyphs.LeftBracket}{CM.Glyphs.LeftDefaultIndicator} abc {CM.Glyphs.RightDefaultIndicator}{CM.Glyphs.RightBracket}",
                      btn.TextFormatter.Format ()
                     );
        Assert.True (btn.IsDefault);
        Assert.Equal (TextAlignment.Centered, btn.TextAlignment);
        Assert.Equal ('_', btn.HotKeySpecifier.Value);
        Assert.True (btn.CanFocus);

        Application.Driver.ClearContents ();
        btn.Draw ();

        expected = @$"
 {CM.Glyphs.LeftBracket}{CM.Glyphs.LeftDefaultIndicator} abc {CM.Glyphs.RightDefaultIndicator}{CM.Glyphs.RightBracket}
";
        TestHelpers.AssertDriverContentsWithFrameAre (expected, output);

        Assert.Equal (new (0, 0, 9, 1), btn.Viewport);
        Assert.Equal (new (1, 2, 9, 1), btn.Frame);
        btn.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void HotKeyChange_Works ()
    {
        var clicked = false;
        var btn = new Button { Text = "_Test" };
        btn.Accept += (s, e) => clicked = true;
        var top = new Toplevel ();
        top.Add (btn);
        Application.Begin (top);

        Assert.Equal (KeyCode.T, btn.HotKey);
        Assert.True (btn.NewKeyDownEvent (Key.T));
        Assert.True (clicked);

        clicked = false;
        Assert.True (btn.NewKeyDownEvent (Key.T.WithAlt));
        Assert.True (clicked);

        clicked = false;
        btn.HotKey = KeyCode.E;
        Assert.True (btn.NewKeyDownEvent (Key.E.WithAlt));
        Assert.True (clicked);
        top.Dispose ();
    }

    /// <summary>
    ///     This test demonstrates how to change the activation key for Button as described in the README.md keyboard
    ///     handling section
    /// </summary>
    [Fact]
    [AutoInitShutdown]
    public void KeyBindingExample ()
    {
        var pressed = 0;
        var btn = new Button { Text = "Press Me" };

        btn.Accept += (s, e) => pressed++;

        // The Button class supports the Default and Accept command
        Assert.Contains (Command.HotKey, btn.GetSupportedCommands ());
        Assert.Contains (Command.Accept, btn.GetSupportedCommands ());

        var top = new Toplevel ();
        top.Add (btn);
        Application.Begin (top);

        // default keybinding is Space which results in keypress
        Application.OnKeyDown (new ((KeyCode)' '));
        Assert.Equal (1, pressed);

        // remove the default keybinding (Space)
        btn.KeyBindings.Clear (Command.HotKey);
        btn.KeyBindings.Clear (Command.Accept);

        // After clearing the default keystroke the Space button no longer does anything for the Button
        Application.OnKeyDown (new ((KeyCode)' '));
        Assert.Equal (1, pressed);

        // Set a new binding of b for the click (Accept) event
        btn.KeyBindings.Add (Key.B, Command.HotKey);
        btn.KeyBindings.Add (Key.B, Command.Accept);

        // now pressing B should call the button click event
        Application.OnKeyDown (Key.B);
        Assert.Equal (2, pressed);

        // now pressing Shift-B should NOT call the button click event
        Application.OnKeyDown (Key.B.WithShift);
        Assert.Equal (2, pressed);

        // now pressing Alt-B should NOT call the button click event
        Application.OnKeyDown (Key.B.WithAlt);
        Assert.Equal (2, pressed);

        // now pressing Shift-Alt-B should NOT call the button click event
        Application.OnKeyDown (Key.B.WithAlt.WithShift);
        Assert.Equal (2, pressed);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void KeyBindings_Command ()
    {
        var clicked = false;
        var btn = new Button { Text = "_Test" };
        btn.Accept += (s, e) => clicked = true;
        var top = new Toplevel ();
        top.Add (btn);
        Application.Begin (top);

        // Hot key. Both alone and with alt
        Assert.Equal (KeyCode.T, btn.HotKey);
        Assert.True (btn.NewKeyDownEvent (Key.T));
        Assert.True (clicked);
        clicked = false;

        Assert.True (btn.NewKeyDownEvent (Key.T.WithAlt));
        Assert.True (clicked);
        clicked = false;

        Assert.True (btn.NewKeyDownEvent (btn.HotKey));
        Assert.True (clicked);
        clicked = false;
        Assert.True (btn.NewKeyDownEvent (btn.HotKey));
        Assert.True (clicked);
        clicked = false;

        // IsDefault = false
        // Space and Enter should work
        Assert.False (btn.IsDefault);
        Assert.True (btn.NewKeyDownEvent (Key.Enter));
        Assert.True (clicked);
        clicked = false;

        // IsDefault = true
        // Space and Enter should work
        btn.IsDefault = true;
        Assert.True (btn.NewKeyDownEvent (Key.Enter));
        Assert.True (clicked);
        clicked = false;

        // Toplevel does not handle Enter, so it should get passed on to button
        Assert.True (Application.Top.NewKeyDownEvent (Key.Enter));
        Assert.True (clicked);
        clicked = false;

        // Direct
        Assert.True (btn.NewKeyDownEvent (Key.Enter));
        Assert.True (clicked);
        clicked = false;

        Assert.True (btn.NewKeyDownEvent (Key.Space));
        Assert.True (clicked);
        clicked = false;

        Assert.True (btn.NewKeyDownEvent (new ((KeyCode)'T')));
        Assert.True (clicked);
        clicked = false;

        // Change hotkey:
        btn.Text = "Te_st";
        Assert.True (btn.NewKeyDownEvent (btn.HotKey));
        Assert.True (clicked);
        clicked = false;

        top.Dispose ();
    }

    [Fact]
    public void HotKey_Command_Accepts ()
    {
        var button = new Button ();
        var accepted = false;

        button.Accept += ButtonOnAccept;
        button.InvokeCommand (Command.HotKey);

        Assert.True (accepted);
        button.Dispose ();

        return;

        void ButtonOnAccept (object sender, CancelEventArgs e) { accepted = true; }
    }

    [Fact]
    public void Accept_Cancel_Event_OnAccept_Returns_True ()
    {
        var button = new Button ();
        var acceptInvoked = false;

        button.Accept += ButtonAccept;

        bool? ret = button.InvokeCommand (Command.Accept);
        Assert.True (ret);
        Assert.True (acceptInvoked);

        button.Dispose ();

        return;

        void ButtonAccept (object sender, CancelEventArgs e)
        {
            acceptInvoked = true;
            e.Cancel = true;
        }
    }

    [Fact]
    public void Setting_Empty_Text_Sets_HoKey_To_KeyNull ()
    {
        var super = new View ();
        var btn = new Button { Text = "_Test" };
        super.Add (btn);
        super.BeginInit ();
        super.EndInit ();

        Assert.Equal ("_Test", btn.Text);
        Assert.Equal (KeyCode.T, btn.HotKey);

        btn.Text = string.Empty;
        Assert.Equal ("", btn.Text);
        Assert.Equal (KeyCode.Null, btn.HotKey);
        btn.Text = string.Empty;
        Assert.Equal ("", btn.Text);
        Assert.Equal (KeyCode.Null, btn.HotKey);

        btn.Text = "Te_st";
        Assert.Equal ("Te_st", btn.Text);
        Assert.Equal (KeyCode.S, btn.HotKey);
        super.Dispose ();
    }

    [Fact]
    public void TestAssignTextToButton ()
    {
        View b = new Button { Text = "heya" };
        Assert.Equal ("heya", b.Text);
        Assert.Contains ("heya", b.TextFormatter.Text);
        b.Text = "heyb";
        Assert.Equal ("heyb", b.Text);
        Assert.Contains ("heyb", b.TextFormatter.Text);

        // with cast
        Assert.Equal ("heyb", ((Button)b).Text);
        b.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Update_Only_On_Or_After_Initialize ()
    {
        var btn = new Button { X = Pos.Center (), Y = Pos.Center (), Text = "Say Hello 你" };
        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (btn);
        var top = new Toplevel ();
        top.Add (win);

        Assert.False (btn.IsInitialized);

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        Assert.True (btn.IsInitialized);
        Assert.Equal ("Say Hello 你", btn.Text);
        Assert.Equal ($"{CM.Glyphs.LeftBracket} {btn.Text} {CM.Glyphs.RightBracket}", btn.TextFormatter.Text);
        Assert.Equal (new (0, 0, 16, 1), btn.Viewport);
        var btnTxt = $"{CM.Glyphs.LeftBracket} {btn.Text} {CM.Glyphs.RightBracket}";

        var expected = @$"
┌────────────────────────────┐
│                            │
│      {btnTxt}      │
│                            │
└────────────────────────────┘
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, output);
        Assert.Equal (new (0, 0, 30, 5), pos);
        top.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void Update_Parameterless_Only_On_Or_After_Initialize ()
    {
        var btn = new Button { X = Pos.Center (), Y = Pos.Center (), Text = "Say Hello 你" };
        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (btn);
        var top = new Toplevel ();
        top.Add (win);

        Assert.False (btn.IsInitialized);

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        Assert.True (btn.IsInitialized);
        Assert.Equal ("Say Hello 你", btn.Text);
        Assert.Equal ($"{CM.Glyphs.LeftBracket} {btn.Text} {CM.Glyphs.RightBracket}", btn.TextFormatter.Text);
        Assert.Equal (new (0, 0, 16, 1), btn.Viewport);
        var btnTxt = $"{CM.Glyphs.LeftBracket} {btn.Text} {CM.Glyphs.RightBracket}";

        var expected = @$"
┌────────────────────────────┐
│                            │
│      {btnTxt}      │
│                            │
└────────────────────────────┘
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, output);
        Assert.Equal (new (0, 0, 30, 5), pos);
        top.Dispose ();
    }

    [Theory]
    [InlineData (MouseFlags.Button1Pressed, MouseFlags.Button1Released, MouseFlags.Button1Clicked)]
    [InlineData (MouseFlags.Button2Pressed, MouseFlags.Button2Released, MouseFlags.Button2Clicked)]
    [InlineData (MouseFlags.Button3Pressed, MouseFlags.Button3Released, MouseFlags.Button3Clicked)]
    [InlineData (MouseFlags.Button4Pressed, MouseFlags.Button4Released, MouseFlags.Button4Clicked)]
    public void WantContinuousButtonPressed_True_ButtonClick_Accepts (MouseFlags pressed, MouseFlags released, MouseFlags clicked)
    {
        var me = new MouseEvent ();

        var button = new Button ()
        {
            Width = 1,
            Height = 1,
            WantContinuousButtonPressed = true
        };

        var acceptCount = 0;

        button.Accept += (s, e) => acceptCount++;

        me.Flags = pressed;
        button.NewMouseEvent (me);
        Assert.Equal (1, acceptCount);

        me.Flags = released;
        button.NewMouseEvent (me);
        Assert.Equal (1, acceptCount);

        me.Flags = clicked;
        button.NewMouseEvent (me);
        Assert.Equal (1, acceptCount);

        button.Dispose ();
    }

    [Theory]
    [InlineData (MouseFlags.Button1Pressed, MouseFlags.Button1Released, MouseFlags.Button1Clicked)]
    [InlineData (MouseFlags.Button2Pressed, MouseFlags.Button2Released, MouseFlags.Button2Clicked)]
    [InlineData (MouseFlags.Button3Pressed, MouseFlags.Button3Released, MouseFlags.Button3Clicked)]
    [InlineData (MouseFlags.Button4Pressed, MouseFlags.Button4Released, MouseFlags.Button4Clicked)]
    public void WantContinuousButtonPressed_True_ButtonPressRelease_Accepts (MouseFlags pressed, MouseFlags released, MouseFlags clicked)
    {
        var me = new MouseEvent ();

        var button = new Button ()
        {
            Width = 1,
            Height = 1,
            WantContinuousButtonPressed = true
        };

        var acceptCount = 0;

        button.Accept += (s, e) => acceptCount++;

        me.Flags = pressed;
        button.NewMouseEvent (me);
        Assert.Equal (1, acceptCount);

        me.Flags = released;
        button.NewMouseEvent (me);
        Assert.Equal (1, acceptCount);

        button.Dispose ();
    }

}