using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Terminal.Gui;

namespace UICatalog.Scenarios;

[ScenarioMetadata ("ASCIICustomButtonTest", "ASCIICustomButton sample")]
[ScenarioCategory ("Controls")]
public class ASCIICustomButtonTest : Scenario {
	static bool _smallerWindow;
	MenuItem _miSmallerWindow;
	ScrollViewTestWindow _scrollViewTestWindow;

	public override void Init ()
	{
		Application.Init ();
		_scrollViewTestWindow = new ScrollViewTestWindow ();
		var menu = new MenuBar {
			Menus = [
				new MenuBarItem ("Window Size", new [] {
					_miSmallerWindow = new MenuItem ("Smaller Window", "", ChangeWindowSize) {
						CheckType = MenuItemCheckStyle.Checked
					},
					null,
					new MenuItem ("Quit", "", () => Application.RequestStop (), null, null,
						(KeyCode)Application.QuitKey)
				})
			]
		};
		Application.Top.Add (menu, _scrollViewTestWindow);
		Application.Run ();
	}

	void ChangeWindowSize ()
	{
		_smallerWindow = (bool)(_miSmallerWindow.Checked = !_miSmallerWindow.Checked);
		_scrollViewTestWindow.Dispose ();
		Application.Top.Remove (_scrollViewTestWindow);
		_scrollViewTestWindow = new ScrollViewTestWindow ();
		Application.Top.Add (_scrollViewTestWindow);
	}

	public override void Run ()
	{
	}

	public class ASCIICustomButton : Button {
		FrameView _border;

		Label _fill;
		public string Description => $"Description of: {Id}";

		public event Action<ASCIICustomButton> PointerEnter;

		public void CustomInitialize ()
		{
			_border = new FrameView {
				Width = Width,
				Height = Height
			};

			AutoSize = false;

			var fillText = new StringBuilder ();
			for (var i = 0; i < Bounds.Height; i++) {
				if (i > 0) {
					fillText.AppendLine ("");
				}

				for (var j = 0; j < Bounds.Width; j++) {
					fillText.Append ("█");
				}
			}

			_fill = new Label {
				Visible = false,
				CanFocus = false,
				Text = fillText.ToString ()
			};

			var title = new Label {
				X = Pos.Center (),
				Y = Pos.Center (),
				Text = Text
			};

			_border.MouseClick += This_MouseClick;
			_fill.MouseClick += This_MouseClick;
			title.MouseClick += This_MouseClick;

			Add (_border, _fill, title);
		}

		void This_MouseClick (object sender, MouseEventEventArgs obj) => OnMouseEvent (obj.MouseEvent);

		public override bool OnMouseEvent (MouseEvent mouseEvent)
		{
			Debug.WriteLine ($"{mouseEvent.Flags}");
			if (mouseEvent.Flags == MouseFlags.Button1Clicked) {
				if (!HasFocus && SuperView != null) {
					if (!SuperView.HasFocus) {
						SuperView.SetFocus ();
					}

					SetFocus ();
					SetNeedsDisplay ();
				}

				OnClicked ();
				return true;
			}

				PointerEnter?.Invoke (this);
		        view = this;
			    return base.OnEnter (view);
			return base.OnMouseEvent (mouseEvent);
		}

			public override bool OnEnter (View view)
			{
				_border.SetDesiredVisibility (false);
				_fill.SetDesiredVisibility (true);
				PointerEnter.Invoke (this);
			}

			public override bool OnLeave (View view)
			{
				_border.SetDesiredVisibility (true);
				_fill.SetDesiredVisibility (false);
				if (view == null)
					view = this;
				return base.OnLeave (view);
			}
		}
	}

	public class ScrollViewTestWindow : Window {
		const int BUTTONS_ON_PAGE = 7;
		const int BUTTON_WIDTH = 25;
		const int BUTTON_HEIGHT = 3;
		readonly List<Button> _buttons;

		readonly ScrollView _scrollView;
		ASCIICustomButton _selected;

		public ScrollViewTestWindow ()
		{
			Title = "ScrollViewTestWindow";

			Label titleLabel = null;
			if (_smallerWindow) {
				Width = 80;
				Height = 25;

				_scrollView = new ScrollView {
					X = 3,
					Y = 1,
					Width = 24,
					Height = BUTTONS_ON_PAGE * BUTTON_HEIGHT,
					ShowVerticalScrollIndicator = true,
					ShowHorizontalScrollIndicator = false
				};
			} else {
				Width = Dim.Fill ();
				Height = Dim.Fill ();

				titleLabel = new Label {
					X = 0,
					Y = 0,
					Text = "DOCUMENTS"
				};

				_scrollView = new ScrollView {
					X = 0,
					Y = 1,
					Width = 27,
					Height = BUTTONS_ON_PAGE * BUTTON_HEIGHT,
					ShowVerticalScrollIndicator = true,
					ShowHorizontalScrollIndicator = false
				};
			}

			_scrollView.KeyBindings.Clear ();

			_buttons = new List<Button> ();
			Button prevButton = null;
			var count = 20;
			for (var j = 0; j < count; j++) {
				var yPos = prevButton == null ? 0 : Pos.Bottom (prevButton);
				var button = new ASCIICustomButton {
					Id = j.ToString (),
					Text = $"section {j}",
					Y = yPos,
					Width = BUTTON_WIDTH,
					Height = BUTTON_HEIGHT
				};
				button.CustomInitialize ();
				button.Clicked += Button_Clicked;
				button.PointerEnter += Button_PointerEnter;
				button.MouseClick += Button_MouseClick;
				button.KeyDown += Button_KeyPress;
				_scrollView.Add (button);
				_buttons.Add (button);
				prevButton = button;
			}

			var closeButton = new ASCIICustomButton {
				Id = "close",
				Text = "Close",
				Y = Pos.Bottom (prevButton),
				Width = BUTTON_WIDTH,
				Height = BUTTON_HEIGHT
			};
			closeButton.CustomInitialize ();
			closeButton.Clicked += Button_Clicked;
			closeButton.PointerEnter += Button_PointerEnter;
			closeButton.MouseClick += Button_MouseClick;
			closeButton.KeyDown += Button_KeyPress;
			_scrollView.Add (closeButton);
			_buttons.Add (closeButton);

			var pages = _buttons.Count / BUTTONS_ON_PAGE;
			if (_buttons.Count % BUTTONS_ON_PAGE > 0) {
				pages++;
			}

			_scrollView.ContentSize = new Size (25, pages * BUTTONS_ON_PAGE * BUTTON_HEIGHT);
			if (_smallerWindow) {
				Add (_scrollView);
			} else {
				Add (titleLabel, _scrollView);
			}
		}

		void Button_KeyPress (object sender, Key obj)
		{
			switch (obj.KeyCode) {
			case KeyCode.End:
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					-(_scrollView.ContentSize.Height - _scrollView.Frame.Height
					  + (_scrollView.ShowHorizontalScrollIndicator ? 1 : 0)));
				obj.Handled = true;
				return;
			case KeyCode.Home:
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X, 0);
				obj.Handled = true;
				return;
			case KeyCode.PageDown:
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					Math.Max (_scrollView.ContentOffset.Y - _scrollView.Frame.Height,
						-(_scrollView.ContentSize.Height - _scrollView.Frame.Height
						  + (_scrollView.ShowHorizontalScrollIndicator ? 1 : 0))));
				obj.Handled = true;
				return;
			case KeyCode.PageUp:
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					Math.Min (_scrollView.ContentOffset.Y + _scrollView.Frame.Height, 0));
				obj.Handled = true;
				return;
			}
		}

		void Button_MouseClick (object sender, MouseEventEventArgs obj)
		{
			if (obj.MouseEvent.Flags == MouseFlags.WheeledDown) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					_scrollView.ContentOffset.Y - BUTTON_HEIGHT);
				obj.Handled = true;
			} else if (obj.MouseEvent.Flags == MouseFlags.WheeledUp) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					Math.Min (_scrollView.ContentOffset.Y + BUTTON_HEIGHT, 0));
				obj.Handled = true;
			}
		}

		void Button_Clicked (object sender, EventArgs e)
		{
			MessageBox.Query ("Button clicked.", $"'{_selected.Text}' clicked!", "Ok");
			if (_selected.Text == "Close") {
				Application.RequestStop ();
			}
		}

		void Button_PointerEnter (ASCIICustomButton obj)
		{
			bool? moveDown;
			if (obj.Frame.Y > _selected?.Frame.Y) {
				moveDown = true;
			} else if (obj.Frame.Y < _selected?.Frame.Y) {
				moveDown = false;
			} else {
				moveDown = null;
			}

			var offSet = _selected != null
				? obj.Frame.Y - _selected.Frame.Y + (-_scrollView.ContentOffset.Y % BUTTON_HEIGHT)
				: 0;
			_selected = obj;
			if (moveDown == true &&
			    _selected.Frame.Y + _scrollView.ContentOffset.Y + BUTTON_HEIGHT >=
			    _scrollView.Frame.Height && offSet != BUTTON_HEIGHT) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					Math.Min (_scrollView.ContentOffset.Y - BUTTON_HEIGHT,
						-(_selected.Frame.Y - _scrollView.Frame.Height + BUTTON_HEIGHT)));
			} else if (moveDown == true &&
				   _selected.Frame.Y + _scrollView.ContentOffset.Y >= _scrollView.Frame.Height) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					_scrollView.ContentOffset.Y - BUTTON_HEIGHT);
			} else if (moveDown == true && _selected.Frame.Y + _scrollView.ContentOffset.Y < 0) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					-_selected.Frame.Y);
			} else if (moveDown == false && _selected.Frame.Y < -_scrollView.ContentOffset.Y) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					Math.Max (_scrollView.ContentOffset.Y + BUTTON_HEIGHT, _selected.Frame.Y));
			} else if (moveDown == false &&
				   _selected.Frame.Y + _scrollView.ContentOffset.Y > _scrollView.Frame.Height) {
				_scrollView.ContentOffset = new Point (_scrollView.ContentOffset.X,
					-(_selected.Frame.Y - _scrollView.Frame.Height + BUTTON_HEIGHT));
			}
		}
	}
}