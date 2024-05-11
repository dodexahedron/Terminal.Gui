﻿using System.ComponentModel;
using Xunit.Abstractions;

namespace Terminal.Gui.ViewsTests;

public class LabelTests
{
    private readonly ITestOutputHelper _output;
    public LabelTests (ITestOutputHelper output) { _output = output; }

    // Test that Title and Text are the same
    [Fact]
    public void Text_Mirrors_Title ()
    {
        var label = new Label ();
        label.Title = "Hello";
        Assert.Equal ("Hello", label.Title);
        Assert.Equal ("Hello", label.TitleTextFormatter.Text);

        Assert.Equal ("Hello", label.Text);
        Assert.Equal ("Hello", label.TextFormatter.Text);
    }

    [Fact]
    public void Title_Mirrors_Text ()
    {
        var label = new Label ();
        label.Text = "Hello";
        Assert.Equal ("Hello", label.Text);
        Assert.Equal ("Hello", label.TextFormatter.Text);

        Assert.Equal ("Hello", label.Title);
        Assert.Equal ("Hello", label.TitleTextFormatter.Text);
    }

    [Fact]
    public void HotKey_Command_SetsFocus_OnNextSubview ()
    {
        var superView = new View () { CanFocus = true };
        var label = new Label ();
        var nextSubview = new View () { CanFocus = true };
        superView.Add (label, nextSubview);
        superView.BeginInit ();
        superView.EndInit ();

        Assert.False (label.HasFocus);
        Assert.False (nextSubview.HasFocus);

        label.InvokeCommand (Command.HotKey);
        Assert.False (label.HasFocus);
        Assert.True (nextSubview.HasFocus);
    }


    [Fact]
    public void MouseClick_SetsFocus_OnNextSubview ()
    {
        var superView = new View () { CanFocus = true, Height = 1, Width = 15 };
        var focusedView = new View () { CanFocus = true, Width = 1, Height = 1 };
        var label = new Label () { X = 2, Title = "_x" };
        var nextSubview = new View () { CanFocus = true, X = 4, Width = 4, Height = 1 };
        superView.Add (focusedView, label, nextSubview);
        superView.BeginInit ();
        superView.EndInit ();

        Assert.False (focusedView.HasFocus);
        Assert.False (label.HasFocus);
        Assert.False (nextSubview.HasFocus);

        label.NewMouseEvent (new MouseEvent () { Position = new (0, 0), Flags = MouseFlags.Button1Clicked });
        Assert.False (label.HasFocus);
        Assert.True (nextSubview.HasFocus);
    }

    [Fact]
    public void HotKey_Command_Does_Not_Accept ()
    {
        var label = new Label ();
        var accepted = false;

        label.Accept += LabelOnAccept;
        label.InvokeCommand (Command.HotKey);

        Assert.False (accepted);

        return;
        void LabelOnAccept (object sender, CancelEventArgs e) { accepted = true; }
    }

    [Fact]
    [AutoInitShutdown]
    public void AutoSize_Stays_True_AnchorEnd ()
    {
        var label = new Label { Y = Pos.Center (), Text = "Say Hello 你" };
        label.X = Pos.AnchorEnd (0) - Pos.Function (() => label.TextFormatter.Text.GetColumns ());

        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (label);
        var top = new Toplevel ();
        top.Add (win);

       

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        var expected = @"
┌────────────────────────────┐
│                            │
│                Say Hello 你│
│                            │
└────────────────────────────┘
";

        TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);

       
        label.Text = "Say Hello 你 changed";
       
        Application.Refresh ();

        expected = @"
┌────────────────────────────┐
│                            │
│        Say Hello 你 changed│
│                            │
└────────────────────────────┘
";

        TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
    }

    [Fact]
    [AutoInitShutdown]
    public void AutoSize_Stays_True_Center ()
    {
        var label = new Label { X = Pos.Center (), Y = Pos.Center (), Text = "Say Hello 你" };

        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (label);
        var top = new Toplevel ();
        top.Add (win);

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        var expected = @"
┌────────────────────────────┐
│                            │
│        Say Hello 你        │
│                            │
└────────────────────────────┘
";

        TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);

       
        label.Text = "Say Hello 你 changed";
       
        Application.Refresh ();

        expected = @"
┌────────────────────────────┐
│                            │
│    Say Hello 你 changed    │
│                            │
└────────────────────────────┘
";

        TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
    }

    [Fact]
    [AutoInitShutdown]
    public void AutoSize_Stays_True_With_EmptyText ()
    {
        var label = new Label { X = Pos.Center (), Y = Pos.Center () };

        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (label);
        var top = new Toplevel ();
        top.Add (win);

       

        label.Text = "Say Hello 你";

       

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        var expected = @"
┌────────────────────────────┐
│                            │
│        Say Hello 你        │
│                            │
└────────────────────────────┘
";

        TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
    }

    [Fact]
    public void Constructors_Defaults ()
    {
        var label = new Label ();
        Assert.Equal (string.Empty, label.Text);
        Assert.Equal (TextAlignment.Left, label.TextAlignment);
        Assert.False (label.CanFocus);
        Assert.Equal (new Rectangle (0, 0, 0, 0), label.Frame);
        Assert.Equal (KeyCode.Null, label.HotKey);
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_Draw_Fill_Remaining_AutoSize_False ()
    {
        Size tfSize = new Size (80, 1);

        var label = new Label { Text = "This label needs to be cleared before rewritten.", Width = tfSize.Width, Height = tfSize.Height };

        var tf1 = new TextFormatter { Direction = TextDirection.LeftRight_TopBottom, Size = tfSize };
        tf1.Text = "This TextFormatter (tf1) without fill will not be cleared on rewritten.";

        var tf2 = new TextFormatter { Direction = TextDirection.LeftRight_TopBottom, Size = tfSize, FillRemaining = true };
        tf2.Text = "This TextFormatter (tf2) with fill will be cleared on rewritten.";

        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

        Assert.False (label.TextFormatter.AutoSize);
        Assert.False (tf1.AutoSize);
        Assert.False (tf2.AutoSize);
        Assert.False (label.TextFormatter.FillRemaining);
        Assert.False (tf1.FillRemaining);
        Assert.True (tf2.FillRemaining);

        tf1.Draw (new Rectangle (new Point (0, 1), tfSize), label.GetNormalColor (), label.ColorScheme.HotNormal);

        tf2.Draw (new Rectangle (new Point (0, 2), tfSize), label.GetNormalColor (), label.ColorScheme.HotNormal);

        TestHelpers.AssertDriverContentsWithFrameAre (
                                                      @"
This label needs to be cleared before rewritten.                       
This TextFormatter (tf1) without fill will not be cleared on rewritten.
This TextFormatter (tf2) with fill will be cleared on rewritten.       ",
                                                      _output
                                                     );

        Assert.False (label.NeedsDisplay);
        Assert.False (label.LayoutNeeded);
        Assert.False (label.SubViewNeedsDisplay);
        label.Text = "This label is rewritten.";
        Assert.True (label.NeedsDisplay);
        Assert.True (label.LayoutNeeded);
        Assert.False (label.SubViewNeedsDisplay);
        label.Draw ();

        tf1.Text = "This TextFormatter (tf1) is rewritten.";
        tf1.Draw (new Rectangle (new Point (0, 1), tfSize), label.GetNormalColor (), label.ColorScheme.HotNormal);

        tf2.Text = "This TextFormatter (tf2) is rewritten.";
        tf2.Draw (new Rectangle (new Point (0, 2), tfSize), label.GetNormalColor (), label.ColorScheme.HotNormal);

        TestHelpers.AssertDriverContentsWithFrameAre (
                                                      @"
This label is rewritten.                                               
This TextFormatter (tf1) is rewritten.will not be cleared on rewritten.
This TextFormatter (tf2) is rewritten.                                 ",
                                                      _output
                                                     );
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_Draw_Horizontal_Simple_Runes ()
    {
        var label = new Label { Text = "Demo Simple Rune" };
        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

       
        Assert.Equal (new Rectangle (0, 0, 16, 1), label.Frame);

        var expected = @"
Demo Simple Rune
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
        Assert.Equal (new Rectangle (0, 0, 16, 1), pos);
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_Draw_Vertical_Simple_Runes ()
    {
        var label = new Label { TextDirection = TextDirection.TopBottom_LeftRight, Text = "Demo Simple Rune" };
        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

        Assert.NotNull (label.Width);
        Assert.NotNull (label.Height);

        var expected = @"
D
e
m
o
 
S
i
m
p
l
e
 
R
u
n
e
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
        Assert.Equal (new Rectangle (0, 0, 1, 16), pos);
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_Draw_Vertical_Wide_Runes ()
    {
        var label = new Label { TextDirection = TextDirection.TopBottom_LeftRight, Text = "デモエムポンズ" };
        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

        var expected = @"
デ
モ
エ
ム
ポ
ン
ズ
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
        Assert.Equal (new Rectangle (0, 0, 2, 7), pos);
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_HotKeyChanged_EventFires ()
    {
        var label = new Label { Text = "Yar" };
        label.HotKey = 'Y';

        object sender = null;
        KeyChangedEventArgs args = null;

        label.HotKeyChanged += (s, e) =>
                               {
                                   sender = s;
                                   args = e;
                               };

        label.HotKey = Key.R;
        Assert.Same (label, sender);
        Assert.Equal (KeyCode.Y | KeyCode.ShiftMask, args.OldKey);
        Assert.Equal (Key.R, args.NewKey);
    }

    [Fact]
    [AutoInitShutdown]
    public void Label_HotKeyChanged_EventFires_WithNone ()
    {
        var label = new Label ();

        object sender = null;
        KeyChangedEventArgs args = null;

        label.HotKeyChanged += (s, e) =>
                               {
                                   sender = s;
                                   args = e;
                               };

        label.HotKey = KeyCode.R;
        Assert.Same (label, sender);
        Assert.Equal (KeyCode.Null, args.OldKey);
        Assert.Equal (KeyCode.R, args.NewKey);
    }

    [Fact]
    public void TestAssignTextToLabel ()
    {
        View b = new Label { Text = "heya" };
        Assert.Equal ("heya", b.Text);
        Assert.Contains ("heya", b.TextFormatter.Text);
        b.Text = "heyb";
        Assert.Equal ("heyb", b.Text);
        Assert.Contains ("heyb", b.TextFormatter.Text);

        // with cast
        Assert.Equal ("heyb", ((Label)b).Text);
    }

    [Fact]
    [AutoInitShutdown]
    public void Update_Only_On_Or_After_Initialize ()
    {
        var label = new Label { X = Pos.Center (), Y = Pos.Center (), Text = "Say Hello 你" };
        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (label);
        var top = new Toplevel ();
        top.Add (win);

        Assert.False (label.IsInitialized);

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        Assert.True (label.IsInitialized);
        Assert.Equal ("Say Hello 你", label.Text);
        Assert.Equal ("Say Hello 你", label.TextFormatter.Text);
        Assert.Equal (new Rectangle (0, 0, 12, 1), label.Viewport);

        var expected = @"
┌────────────────────────────┐
│                            │
│        Say Hello 你        │
│                            │
└────────────────────────────┘
";
        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
        Assert.Equal (new Rectangle (0, 0, 30, 5), pos);
    }

    [Fact]
    [AutoInitShutdown]
    public void Update_Parameterless_Only_On_Or_After_Initialize ()
    {
        var label = new Label { X = Pos.Center (), Y = Pos.Center (), Text = "Say Hello 你" };
        var win = new Window { Width = Dim.Fill (), Height = Dim.Fill () };
        win.Add (label);
        var top = new Toplevel ();
        top.Add (win);

        Assert.False (label.IsInitialized);

        Application.Begin (top);
        ((FakeDriver)Application.Driver).SetBufferSize (30, 5);

        Assert.True (label.IsInitialized);
        Assert.Equal ("Say Hello 你", label.Text);
        Assert.Equal ("Say Hello 你", label.TextFormatter.Text);
        Assert.Equal (new Rectangle (0, 0, 12, 1), label.Viewport);

        var expected = @"
┌────────────────────────────┐
│                            │
│        Say Hello 你        │
│                            │
└────────────────────────────┘
";

        Rectangle pos = TestHelpers.AssertDriverContentsWithFrameAre (expected, _output);
        Assert.Equal (new Rectangle (0, 0, 30, 5), pos);
    }


    [Fact]
    [SetupFakeDriver]
    public void Full_Border ()
    {
        var label = new Label { BorderStyle = LineStyle.Single , Text = "Test",} ;
        label.BeginInit();
        label.EndInit();
        label.SetRelativeLayout (Application.Driver.Screen.Size);

        Assert.Equal (new (0, 0, 4, 1), label.Viewport);
        Assert.Equal (new (0, 0, 6, 3), label.Frame);

        label.Draw ();

        TestHelpers.AssertDriverContentsWithFrameAre (
                                                      @"
┌┤Te├┐
│Test│
└────┘",
                                                      _output
                                                     );
        label.Dispose ();
    }

    [Fact]
    [AutoInitShutdown]
    public void With_Top_Margin_Without_Top_Border ()
    {
        var label = new Label { Text = "Test", /*Width = 6, Height = 3,*/ BorderStyle = LineStyle.Single };
        label.Margin.Thickness = new Thickness (0, 1, 0, 0);
        label.Border.Thickness = new Thickness (1, 0, 1, 1);
        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

        Assert.Equal (new (0, 0, 6, 3), label.Frame);
        Assert.Equal (new (0, 0, 4, 1), label.Viewport);
        Application.Begin (top);

        TestHelpers.AssertDriverContentsWithFrameAre (
                                                      @"
│Test│
└────┘",
                                                      _output
                                                     );
    }

    [Fact]
    [AutoInitShutdown]
    public void Without_Top_Border ()
    {
        var label = new Label { Text = "Test", /* Width = 6, Height = 3, */BorderStyle = LineStyle.Single };
        label.Border.Thickness = new Thickness (1, 0, 1, 1);
        var top = new Toplevel ();
        top.Add (label);
        Application.Begin (top);

        Assert.Equal (new (0, 0, 6, 2), label.Frame);
        Assert.Equal (new (0, 0, 4, 1), label.Viewport);
        Application.Begin (top);

        TestHelpers.AssertDriverContentsWithFrameAre (
                                                      @"
│Test│
└────┘",
                                                      _output
                                                     );
    }

}
