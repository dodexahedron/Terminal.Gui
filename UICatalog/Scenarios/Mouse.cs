﻿using System.Collections.Generic;
using Terminal.Gui;

namespace UICatalog.Scenarios;
[ScenarioMetadata (Name: "Mouse", Description: "Demonstrates how to capture mouse events")]
[ScenarioCategory ("Mouse and Keyboard")]
public class Mouse : Scenario {
	public override void Setup ()
	{
		Label ml;
		int count = 0;
		ml = new Label ("Mouse: ") { X = 1, Y = 1, Width = 50, Height = 1 };
		List<string> rme = new List<string> ();

		var test = new Label ("Se iniciará el análisis") { X = 1, Y = 2 };
		Win.Add (test);
		Win.Add (ml);

		var rmeList = new ListView (rme) {
			X = Pos.Right (test) + 25,
			Y = Pos.Top (test) + 1,
			Width = Dim.Fill () - 1,
			Height = Dim.Fill (),
			ColorScheme = Colors.TopLevel
		};
		Win.Add (rmeList);

		Application.MouseEvent += (sender, a) => {
			ml.Text = $"Mouse: ({a.MouseEvent.X},{a.MouseEvent.Y}) - {a.MouseEvent.Flags} {count}";
			rme.Add ($"({a.MouseEvent.X},{a.MouseEvent.Y}) - {a.MouseEvent.Flags} {count++}");
			rmeList.MoveDown ();
		};

		// I have no idea what this was intended to show off in demo.c
		var drag = new Label ("Drag: ") { X = 1, Y = 4 };
		var dragText = new TextField ("") {
			X = Pos.Right (drag),
			Y = Pos.Top (drag),
			Width = 40
		};
		Win.Add (drag, dragText);
	}
}
